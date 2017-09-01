using System.Threading.Tasks;
using System.Reflection;
using Discord.Commands;
using Discord.WebSocket;
using Discord;
using System;
using Microsoft.Extensions.DependencyInjection;
using PokemonGoRaidBot.Config;
using System.Collections.Generic;
using PokemonGoRaidBot.Objects;
using System.Linq;
using Discord.Rest;
using PokemonGoRaidBot.Parsing;
using System.Net;
using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;

namespace PokemonGoRaidBot
{
    public class CommandHandler
    {
        private CommandService commands;
        private DiscordSocketClient bot;
        private IServiceProvider map;

        public RaidLogger Logger;
        public readonly BotConfig Config;

        private List<MessageParser> ParserCache = new List<MessageParser>();
        private List<GuildConfig> GuildConfigCache = new List<GuildConfig>();

        Dictionary<ulong, ISocketMessageChannel> channelCache;

        public CommandHandler(IServiceProvider provider, BotConfig botconfig, RaidLogger logger)
        {
            map = provider;
            bot = map.GetService<DiscordSocketClient>();

            Logger = logger;
            Config = botconfig;
            
            bot.MessageReceived += HandleCommand;
            bot.ReactionAdded += ReactionAdded;
            bot.ReactionRemoved += ReactionRemoved;
            //bot.ReactionsCleared += ReactionsCleared;
            commands = map.GetService<CommandService>();
            channelCache = new Dictionary<ulong, ISocketMessageChannel>();
        }

        public async Task ConfigureAsync()
        {
            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        //private async Task ReactionsCleared(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2)
        //{
        //    if (!arg1.HasValue) return;
        //    IUserMessage message = arg1.Value;
        //}

        private async Task ReactionRemoved(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            var message = await arg2.GetMessageAsync(arg1.Id);
            if (message == null || string.IsNullOrEmpty(message.Content) || !(arg2 is SocketGuildChannel) || arg3.Emote.Name == "👎") return;
            var guildConfig = Config.GetGuildConfig(((SocketGuildChannel)arg2).Guild.Id);
            var channel = (SocketGuildChannel)arg2;

            var parser = GetParser(guildConfig);
            var post = parser.ParsePostFromPostMessage(message.Embeds.First().Description, guildConfig);
            var user = channel.Guild.GetUser(arg3.UserId);

            if (post != null && user != null)
            {
                var removeUser = post.JoinedUsers.FirstOrDefault(x => x.Id == user.Id);
                if(removeUser != null)
                {
                    removeUser.PeopleCount--;

                    if(removeUser.PeopleCount <= 0)
                        post.JoinedUsers.Remove(removeUser);

                    Config.Save();
                    await MakePost(post, parser);
                }
            }
        }

        public async Task ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            var message = await arg2.GetMessageAsync(arg1.Id);
            if (message == null || string.IsNullOrEmpty(message.Content) || !(arg2 is SocketGuildChannel)) return;
            var guildConfig = Config.GetGuildConfig(((SocketGuildChannel)arg2).Guild.Id);
            //var message = arg1.Value;
            var channel = (SocketGuildChannel)arg2;
            var user = channel.Guild.GetUser(arg3.UserId);

            var parser = GetParser(guildConfig);
            var post = parser.ParsePostFromPostMessage(message.Embeds.First().Description, guildConfig);

            if(post != null && user != null)
            {
                if(arg3.Emote.Name == "👎")//thumbs down will be quick way to delete a raid by poster/admin
                {
                    if(user.Id == post.UserId || user.GuildPermissions.Administrator || user.GuildPermissions.ManageGuild)
                        DeletePost(post);
                }
                else
                {
                    var joinedUser = post.JoinedUsers.FirstOrDefault(x => x.Id == user.Id);
                    if (joinedUser != null)
                        joinedUser.PeopleCount++;
                    else
                    {
                        post.JoinedUsers.Add(new PokemonRaidJoinedUser(user.Id, guildConfig.Id, post.UniqueId, user.Username, 1));
                    }
                    Config.Save();
                    await MakePost(post, parser);
                }
            }
        }

        public async Task HandleCommand(SocketMessage pMsg)
        {
            try
            {
                if (pMsg.Source == MessageSource.System && pMsg.Author.Id == bot.CurrentUser.Id)
                {//this is the annoying "x pinned a message to this channel" system message.
                    await pMsg.Channel.DeleteMessagesAsync(new IMessage[] { pMsg });
                    return;
                }
                var message = pMsg as SocketUserMessage;

                if (message == null || message.Author == null || message.Author.IsBot)
                    return;

                if(message.Channel is SocketDMChannel)
                {
                    var lang = new ParserLanguage(Config.DefaultLanguage);

                    if(message.Content == lang.Strings["stop"])
                    { 
                        await DirectMessageUser(message.Author, lang.Strings["dmStart"]);
                        Config.NoDMUsers.Add(message.Author.Id);
                        Config.Save();
                    }
                    else if (message.Content == lang.Strings["start"])
                    {
                        Config.NoDMUsers.RemoveAll(x => x == message.Author.Id);
                        Config.Save();
                        await DirectMessageUser(message.Author, lang.Strings["dmReStart"]);
                    }
                    else
                        await DirectMessageUser(message.Author, lang.Strings["dmResp"] + lang.Strings["dmStop"]);

                    return;
                }
                if(message.Channel is SocketGuildChannel)
                {
                    var guild = ((SocketGuildChannel)message.Channel).Guild;

                    var firstLoad = !Config.HasGuildConfig(guild.Id);

                    var guildConfig = Config.GetGuildConfig(guild.Id);

                    if (firstLoad)//pin all on first load
                    {
                        foreach (var channel in ((SocketGuildChannel)message.Channel).Guild.Channels)
                        {
                            guildConfig.PinChannels.Add(channel.Id);
                        }
                    }

                    ISocketMessageChannel outputchannel = null;

                    //get output channel
                    if (guildConfig.OutputChannelId.HasValue)
                        outputchannel = (ISocketMessageChannel)guild.Channels.FirstOrDefault(x => x.Id == guildConfig.OutputChannelId.Value);
                    else
                    {
                        outputchannel = (ISocketMessageChannel)guild.Channels.FirstOrDefault(x => x.Name == Config.OutputChannel);
                        guildConfig.OutputChannelId = outputchannel.Id;
                        Config.Save();
                    }

                    var context = new SocketCommandContext(bot, message);

                    //get configured guild language or default "en-us"
                    var lang = guildConfig.Language ?? Config.DefaultLanguage ?? "en-us";
                    //timezone of the bot machine
                    //get configured timezone

                    var parser = GetParser(guildConfig);

                    var doPost = (outputchannel != null ||
                            guildConfig.PinChannels.Contains(message.Channel.Id))
                        && !guildConfig.MuteChannels.Contains(message.Channel.Id);

                    var argPos = 0;

                    //begin parsing/execution
                    //Someone is issuing a command, respond in their channel
                    if (message.HasStringPrefix(Config.Prefix, ref argPos))
                    {
                        await DoCommand(message, parser);
                    }
                    //try to see if a raid was posted
                    else if (doPost)
                    {
                        var post = parser.ParsePost(message, Config);
                        await DoPost(post, message, parser, outputchannel);
                    }
                }
            }
            catch (Exception e)
            {
                DoError(e);
            }
        }
        /// <summary>
        /// Used as a recurring method which will remove old posts to keep the output channel clean of expired raids.
        /// </summary>
        /// <param name="stateInfo"></param>
        public async void PurgePosts()
        {
            try
            {
                var now = DateTime.Now;
                int remaining = 0, deleted = 0;
                foreach (var guild in bot.Guilds)
                {
                    var deletedPosts = new List<PokemonRaidPost>();
                    var posts = Config.GetGuildConfig(guild.Id).Posts;
                    foreach (var post in posts)
                    {
                        if (post.EndDate < now)
                        {
                            deletedPosts.Add(post);
                            deleted++;
                        }
                        else remaining++;
                    }

                    foreach (var post in deletedPosts)
                    {
                        var outputChannel = GetChannel(post.OutputChannelId);
                        var fromChannel = GetChannel(post.FromChannelId);

                        var messages = new List<IMessage>();

                        if (post.OutputMessageId != default(ulong))
                        {
                            try
                            {

                                var m = new IMessage[] { await outputChannel.GetMessageAsync(post.OutputMessageId) };
                                messages.AddRange(m.Where(x => x != null));
                            }
                            catch (Exception e)
                            {
                                DoError(e);
                            }
                        }
                        try
                        {
                            if (messages.Count() > 0) await outputChannel.DeleteMessagesAsync(messages);
                            posts.Remove(post);
                        }
                        catch (Exception e)
                        {
                            DoError(e);
                        }

                        if (post.MessageId > 0)
                        {
                            var m1 = new IMessage[] { await fromChannel.GetMessageAsync(post.MessageId) };

                            if (m1.Count() > 0 && m1[0] != null)
                            {
                                try
                                {
                                    if (m1[0] is SocketUserMessage && ((SocketUserMessage)m1[0]).IsPinned)
                                        await ((SocketUserMessage)m1[0]).UnpinAsync();

                                    await m1[0].DeleteAsync();
                                }
                                catch (Exception e)
                                {
                                    DoError(e);
                                }
                            }
                        }
                    }
                }
                Config.Save();
                await Logger.Log(new LogMessage(LogSeverity.Debug, "Handler", string.Format("deleted:{0}; remaining:{1}", deleted, remaining)));
            }
            catch (Exception e)
            {
                DoError(e);
            }
        }
        /// <summary>
        /// Output an error to the bot console.
        /// </summary>
        /// <param name="e"></param>
        public void DoError(Exception e, string source = "Handler")
        {
            Logger.Log(new LogMessage(LogSeverity.Error, source, null, e));
        }
        /// <summary>
        /// Executes a explicit bot command
        /// </summary>
        /// <param name="message"></param>
        /// <param name="parser"></param>
        /// <returns></returns>
        private async Task DoCommand(SocketUserMessage message, MessageParser parser)
        {
            var executor = new CommandExecutor(this, message, parser);
            await executor.Execute();
        }
        /// <summary>
        /// Performs the raid post behavior
        /// </summary>
        /// <param name="message"></param>
        /// <param name="parser"></param>
        /// <param name="outputchannel"></param>
        /// <param name="pin"></param>
        /// <returns></returns>
        public async Task DoPost(PokemonRaidPost post, SocketUserMessage message, MessageParser parser, ISocketMessageChannel outputchannel, bool force = false)
        {
            //var post = await parser.ParsePost(message, Config);//await DoResponse(message, parser);//returns null if 

            if (post != null)
            {
                post.OutputChannelId = outputchannel?.Id ?? 0;
                post = AddPost(post, parser, message, true, force);

                if (post.IsValid)
                {
                    IDisposable d = null;

                    if (!post.IsExisting)//it's going to post something and google geocode can take a few secs so we can do the "typing" behavior
                        d = message.Channel.EnterTypingState();

                    if (!post.LatLong.HasValue && !string.IsNullOrWhiteSpace(post.Location))
                        post.LatLong = await parser.GetLocationLatLong(post.FullLocation, (SocketGuildChannel)message.Channel, Config);

                    await MakePost(post, parser);

                    if (d != null) d.Dispose();
                }
                //else TODO maybe DM user to see if it's valid? Tricky because hard to hold on to post ID reference...
            }

            Config.Save();
        }
        /// <summary>
        /// Creates the text string and outputs the post message into the channel.
        /// If post.MessageId is populated, will delete and recreate the message.
        /// 403 Error is output to console if bot user doesn't have role access to manage messages.
        /// </summary>
        /// <param name="post"></param>
        /// <param name="outputchannel"></param>
        public async Task MakePost(PokemonRaidPost post, MessageParser parser)
        {
            try
            {
                var fromChannel = GetChannel(post.FromChannelId);
                var outputChannel = GetChannel(post.OutputChannelId);

                //var messages = parser.MakePostStrings(post);
                Embed headerEmbed, responsesEmbed;
                string mentionString;

                parser.MakePostWithEmbed(post, out headerEmbed, out responsesEmbed, out mentionString);

                IUserMessage messageResult;
                if (post.Pin && fromChannel != null)
                {
                    var fromChannelMessage = headerEmbed.Description;
                    if (post.MessageId != default(ulong))
                    {
                        messageResult = (IUserMessage)await fromChannel.GetMessageAsync(post.MessageId);
                        
                        //only modify post if something changed
                        if (!messageResult?.Embeds.FirstOrDefault()?.Description.Equals(fromChannelMessage, StringComparison.OrdinalIgnoreCase) ?? true)
                            await messageResult.ModifyAsync(x => { x.Content = mentionString; x.Embed = headerEmbed; });
                    }
                    else
                    {
                        messageResult = await fromChannel.SendMessageAsync(mentionString, false, headerEmbed);
                        try
                        {
                            await messageResult.PinAsync();
                        }
                        catch (Exception e)
                        {
                            DoError(e);
                        }
                        post.MessageId = messageResult.Id;

                    }

                }
                if(outputChannel != null)
                {
                    if(post.OutputMessageId != default(ulong))
                    {
                        messageResult = (IUserMessage)await outputChannel.GetMessageAsync(post.OutputMessageId);
                        await messageResult.ModifyAsync(x => { x.Embed = responsesEmbed; x.Content = mentionString; });
                    }
                    else
                    {

                        messageResult = await outputChannel.SendMessageAsync(mentionString, false, responsesEmbed);

                        post.OutputMessageId = messageResult.Id;
                    }
                }
            }
            catch (Exception e)
            {
                DoError(e);
            }
        }
        /// <summary>
        /// Use this to notify users when join count changes
        /// </summary>
        /// <param name="user"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task DirectMessageUser(SocketUser user, string message)
        {
            if (Config.NoDMUsers.Contains(user.Id)) return;
            var channel = await user.GetOrCreateDMChannelAsync();
            try
            {
                await channel.SendMessageAsync(message);
            }
            catch (Exception e)
            {
                DoError(e);
            }
        }
        /// <summary>
        /// Adds new post or updates the existing post in the raid post array.
        /// Matches existing posts with the following logic:
        ///     - Message in the same original channel
        ///     - Pokemon name matches (first priority)
        ///     - User already in thread (second priority)
        /// </summary>
        /// <param name="post"></param>
        /// <returns>Returns either the new post or the one it matched with.</returns>
        public PokemonRaidPost AddPost(PokemonRaidPost post, MessageParser parser, SocketUserMessage message, bool add = true, bool force = false)
        {
            var guildConfig = Config.GetGuildConfig(post.GuildId);
            var channelPosts = guildConfig.Posts.Where(x => x.FromChannelId == post.FromChannelId);
            PokemonRaidPost existing = null;
            if (!force)
            {
                //Check if user in existing post is mentioned, get latest post associated with user
                foreach (var mentionedUser in message.MentionedUsers)
                {
                    existing = guildConfig.Posts.OrderByDescending(x => x.Responses.Max(xx => xx.MessageDate))
                        .FirstOrDefault(x => x.FromChannelId == message.Channel.Id
                            && (x.Responses.Where(xx => xx.UserId == mentionedUser.Id).Count() > 0 || x.JoinedUsers.Where(xx => xx.Id == mentionedUser.Id).Count() > 0)
                            && x.PokemonId == (post.PokemonId == 0 ? x.PokemonId : post.PokemonId));

                    if (existing != null)
                        break;
                }
                //if location matches, must be same.
                if(existing == null)
                    existing = channelPosts.FirstOrDefault(x => x.PokemonId == (post.PokemonId > 0 ? post.PokemonId : x.PokemonId) 
                        && parser.CompareLocationStrings(x.Location, post.Location));

                //Lat long comparison, within 30 meters is treated as same
                if(existing == null && post.LatLong.HasValue && channelPosts.Where(x => x.LatLong.HasValue).Count() > 0)
                    existing = channelPosts.Where(x => x.LatLong.HasValue && x.PokemonId == (post.PokemonId > 0 ? post.PokemonId : x.PokemonId))
                        .FirstOrDefault(x => parser.CompareLocationLatLong(x.LatLong.Value, post.LatLong.Value));

                //Final fall through, gets latest post in channel either matching pokemon name or user was involved with
                if (existing == null)//if location exists and doesn't match, not a match
                    existing = channelPosts
                        .Where(x => string.IsNullOrWhiteSpace(post.Location) || x.UserId == post.UserId)
                        .OrderByDescending(x => x.PostDate)
                        .OrderBy(x => x.PokemonId == post.PokemonId ? 0 : 1)//pokemon name match takes priority if the user responded to multiple raids in the channel
                        .FirstOrDefault(x =>
                            x.FromChannelId == post.FromChannelId//Posted in the same channel
                            && ((post.PokemonId != default(int) && x.PokemonId == post.PokemonId)//Either pokemon matches OR
                                || (post.PokemonId == default(int) && (x.Responses.Where(xx => xx.UserId == post.UserId).Count() > 0) 
                                    || post.PokemonId == default(int) && x.JoinedUsers.Where(xx => xx.Id == post.UserId).Count() > 0))//User already in the thread
                        );
            }

            if (existing != null)
            {
                if(add) existing = MergePosts(existing, post);
                existing.IsExisting = true;
                return existing;
            }
            else if(add && post.IsValid)
            {
                post.JoinedUsersChanged += JoinCount_Changed;
                guildConfig.Posts.Add(post);
            }
            
            return post;
        }
        /// <summary>
        /// Combines two posts into one.  The second is should be the newer post.
        /// </summary>
        /// <param name="post1"></param>
        /// <param name="post2"></param>
        /// <returns></returns>
        public PokemonRaidPost MergePosts(PokemonRaidPost post1, PokemonRaidPost post2)
        {
            if (post2.HasEndDate && (!post1.HasEndDate || post2.UserId == post1.UserId))
            {
                post1.HasEndDate = true;
                post1.EndDate = post2.EndDate;
            }

            if (string.IsNullOrEmpty(post1.Location) && !string.IsNullOrEmpty(post2.Location))//only merge location if first is blank and second has lat long
            {
                post1.Location = post2.Location;
                post1.FullLocation = post2.FullLocation;
                post1.LatLong = post2.LatLong;
            }

            post1.LastMessageDate = new DateTime(Math.Max(post1.LastMessageDate.Ticks, post2.LastMessageDate.Ticks));

            //overwrite with new values
            foreach (var user in post2.JoinedUsers)
            {
                if (post1.JoinedUsers.FirstOrDefault(x => x.Id == user.Id) == null)
                    post1.JoinedUsers.Add(new PokemonRaidJoinedUser(user.Id, post1.GuildId, post1.UniqueId, user.Name, user.PeopleCount));
                else if (post2.UserId == user.Id)
                {
                    var postUser = post1.JoinedUsers.FirstOrDefault(x => x.Id == post2.UserId);
                    if (postUser != null)
                    {
                        if (user.IsLess)
                        {
                            postUser.PeopleCount -= user.PeopleCount;
                        }
                        else if (user.IsMore)
                        {
                            postUser.PeopleCount += user.PeopleCount;
                        }
                        else if(user.PeopleCount > 0) postUser.PeopleCount = user.PeopleCount;

                        if (user.ArriveTime.HasValue) postUser.ArriveTime = user.ArriveTime;//always update arrive time if present
                    }
                }
            }

            post1.Responses.AddRange(post2.Responses);
            post1.MentionedRoleIds.AddRange(post2.MentionedRoleIds.Where(x => !post1.MentionedRoleIds.Contains(x)));
            
            return post1;
        }
        /// <summary>
        /// Delete a post from both chat and the list.
        /// </summary>
        /// <param name="post"></param>
        public void DeletePost(PokemonRaidPost post)
        {
            post.EndDate = DateTime.MinValue;
            PurgePosts();
        }
        /// <summary>
        /// Posts a message from a command into chat.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task MakeCommandMessage(ISocketMessageChannel channel, string message)
        {
            await channel.SendMessageAsync($"```{message}```");
        }

        public void JoinCount_Changed(object sender, JoinedCountChangedEventArgs e)
        {
            PokemonRaidPost post = null;
            GuildConfig guildConfig = null;
            PokemonRaidJoinedUser joinedUser = null;

            if (sender is PokemonRaidPost)
            {
                post = (PokemonRaidPost)sender;
                guildConfig = Config.GetGuildConfig(post.GuildId);
            }
            else if (sender is PokemonRaidJoinedUser)
            {
                joinedUser = (PokemonRaidJoinedUser)sender;
                guildConfig = Config.GetGuildConfig(joinedUser.GuildId);
                //var guild = bot.Guil
                post = guildConfig.Posts.FirstOrDefault(x => x.UniqueId == joinedUser.PostId);
            }
            
            if(post != null)
            {
                List<Task> tasks = new List<Task>();
                var parser = GetParser(guildConfig);

                var joinstr = parser.Language.Strings["joining"];

                if (e.ChangeType == JoinCountChangeType.Remove || (e.ChangeType == JoinCountChangeType.Change && e.Count < 0))
                    joinstr = parser.Language.Strings["quitting"];

                string message = string.Format(parser.Language.Formats["directMessage"],
                    e.Count,
                    joinstr,
                    post.PokemonName,
                    e.ArriveTime.HasValue && !joinstr.Equals(parser.Language.Strings["quitting"]) ? string.Format(" *at {0:hh:mmt}*", e.ArriveTime.Value) : "",
                    e.UserName);

                var usersToDM = new List<ulong>();
                if (post.UserId != e.UserId) usersToDM.Add(post.UserId);
                usersToDM.AddRange(post.JoinedUsers.Where(x => x.Id != e.UserId && x.Id != post.UserId).Select(x => x.Id));

                foreach (var id in usersToDM)
                {
                    var user = bot.GetUser(id);

                    if(user != default(SocketUser))
                        tasks.Add(DirectMessageUser(user, $"{message}\n{parser.Language.Strings["dmStop"]}"));//TODO
                }
                Task.WaitAll(tasks.ToArray());
            }
        }

        private ISocketMessageChannel GetChannel(ulong id)
        {
            if (channelCache.ContainsKey(id)) return channelCache[id];
            
            return channelCache[id] = (ISocketMessageChannel)bot.GetChannel(id);
        }
        private MessageParser GetParser(GuildConfig guildConfig)
        {
            var botTimezone = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).Hours - (TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) ? 1 : 0);
            var serverTimezone = guildConfig.Timezone ?? botTimezone;
            var timeOffset = serverTimezone - botTimezone;

            return GetParser(guildConfig.Language ?? "en-us", timeOffset);
        }
        private MessageParser GetParser(string lang, int offset)
        {
            var parser = ParserCache.FirstOrDefault(x => x.Lang == lang && x.TimeOffset == offset);
            if (parser != null) return parser;

            parser = new MessageParser(lang, offset);
            ParserCache.Add(parser);
            return parser;
        }
        private PokemonRaidPost GetPost(string uid, ulong guildId)
        {
            return Config.GetGuildConfig(guildId).Posts.FirstOrDefault(x => x.UniqueId == uid);
        }
        //private GuildConfig Config.GetGuildConfig(ulong Id)
        //{
        //    var config = GuildConfigCache.FirstOrDefault(x => x.Id == Id);
        //    if(config == null)
        //    {
        //        config = Config.GetGuildConfig(Id);
        //        if(config != null)
        //        {
        //            GuildConfigCache.Add(config);
        //        }
        //    }
        //    return config;
        //}
    }
}