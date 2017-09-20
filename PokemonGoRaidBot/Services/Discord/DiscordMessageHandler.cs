using System.Threading.Tasks;
using System.Reflection;
using Discord.Commands;
using Discord.WebSocket;
using Discord;
using System;
using Microsoft.Extensions.DependencyInjection;
using PokemonGoRaidBot.Configuration;
using System.Collections.Generic;
using PokemonGoRaidBot.Objects;
using System.Linq;
using System.Linq.Expressions;
using PokemonGoRaidBot.Services.Parsing;
using PokemonGoRaidBot.Data;
using PokemonGoRaidBot.Data.Entities;
using PokemonGoRaidBot.Objects.Interfaces;

namespace PokemonGoRaidBot.Services.Discord
{
    public class DiscordMessageHandler : IChatMessageHandler
    {
        private CommandService commands;
        private DiscordSocketClient bot;
        private IServiceProvider map;
        private IStatMapper Mapper;

        private List<string> DeleteEmojis = new List<string>(new string[] { "👎", "👎🏻", "👎🏽", "👎🏾", "👎🏿" });

        public ConsoleLogger Logger;
        public BotConfiguration Config { get; private set; }

        private PokemonRaidBotDbContext dbContext
        {
            get
            {
                return map.GetService<PokemonRaidBotDbContext>();
            }
        }

        private List<MessageParser> ParserCache = new List<MessageParser>();
        private List<ServerConfiguration> GuildConfigCache = new List<ServerConfiguration>();

        Dictionary<ulong, ISocketMessageChannel> channelCache;

        public DiscordMessageHandler(IServiceProvider provider)
        {
            map = provider;

            bot = map.GetService<DiscordSocketClient>();
            Mapper = map.GetService<IStatMapper>();
            Logger = map.GetService<ConsoleLogger>();
            Config = map.GetService<BotConfiguration>();
            commands = map.GetService<CommandService>();

            bot.MessageReceived += HandleCommand;
            bot.ReactionAdded += ReactionAdded;
            bot.ReactionRemoved += ReactionRemoved;
            

            bot.JoinedGuild += JoinedGuild;
            bot.ChannelCreated += ChannelCreated;

            channelCache = new Dictionary<ulong, ISocketMessageChannel>();
        }

        private async Task ChannelCreated(SocketChannel channel)
        {
            if (!(channel is SocketGuildChannel)) return;

            await dbContext.AddOrUpdateChannel((SocketGuildChannel)channel);
        }

        private async Task JoinedGuild(SocketGuild guild)
        {
            await dbContext.AddOrUpdateGuild(guild);
        }

        public async Task ConfigureAsync()
        {
            await commands.AddModulesAsync(Assembly.GetEntryAssembly());

            foreach (var g in bot.Guilds)
            {
                await dbContext.AddOrUpdateGuild(g);
            }
        }

        private async Task ReactionRemoved(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (arg3.UserId == bot.CurrentUser.Id) return;

            var message = await arg2.GetMessageAsync(arg1.Id);
            if (message == null || string.IsNullOrEmpty(message.Content) || !(arg2 is SocketGuildChannel) || DeleteEmojis.Contains(arg3.Emote.Name)) return;
            var guildConfig = Config.GetServerConfig(((SocketGuildChannel)arg2).Guild.Id, ChatTypes.Discord);
            var channel = (SocketGuildChannel)arg2;

            var parser = GetParser(guildConfig);
            var post = parser.ParsePostFromPostMessage(message.Embeds.First().Description, guildConfig);
            var user = channel.Guild.GetUser(arg3.UserId);

            if (post != null && user != null)
            {
                var removeUser = post.JoinedUsers.FirstOrDefault(x => x.Id == user.Id);
                if (removeUser != null)
                {
                    removeUser.PeopleCount--;

                    if (removeUser.PeopleCount <= 0)
                        post.JoinedUsers.Remove(removeUser);

                    var messages = await MakePost(post, parser);
                    var tasks = new List<Task>();
                    foreach (var resultmessage in messages.Where(x => x.Channel.Id != message.Channel.Id))
                    {
                        tasks.Add(resultmessage.RemoveReactionAsync(arg3.Emote.Name, new DiscordChatUser(bot.CurrentUser)));
                    }

                    Task.WaitAll(tasks.ToArray());
                    Config.Save();
                }
            }
        }

        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (arg3.UserId == bot.CurrentUser.Id) return;

            var message = await arg2.GetMessageAsync(arg1.Id);
            if (message == null || !(arg2 is SocketGuildChannel)) return;

            var embed = message.Embeds.FirstOrDefault();
            if (embed == null || string.IsNullOrEmpty(embed.Description)) return;

            var guildConfig = Config.GetServerConfig(((SocketGuildChannel)arg2).Guild.Id, ChatTypes.Discord);
            //var message = arg1.Value;
            var channel = (SocketGuildChannel)arg2;
            var user = channel.Guild.GetUser(arg3.UserId);

            var parser = GetParser(guildConfig);
            var post = parser.ParsePostFromPostMessage(message.Embeds.First().Description, guildConfig);

            if (post != null && user != null)
            {
                if (DeleteEmojis.Contains(arg3.Emote.Name))//thumbs down will be quick way to delete a raid by poster/admin
                {
                    await DeletePost(post, user.Id, user.GuildPermissions.Administrator || user.GuildPermissions.ManageGuild);
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

                    var messages = await MakePost(post, parser);
                    var tasks = new List<Task>();
                    foreach (var resultmessage in messages.Where(x => x.Channel.Id != message.Channel.Id))
                    {
                        tasks.Add(resultmessage.AddReactionAsync(arg3.Emote.Name));
                    }

                    Task.WaitAll(tasks.ToArray());
                    Config.Save();
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

                if (message.Channel is SocketDMChannel)
                {
                    var lang = new ParserLanguage(Config.DefaultLanguage);

                    if (message.Content == lang.Strings["stop"])
                    {
                        await DirectMessageUser(new DiscordChatUser(message.Author), lang.Strings["dmStart"]);
                        Config.NoDMUsers.Add(message.Author.Id);
                        Config.Save();
                    }
                    else if (message.Content == lang.Strings["start"])
                    {
                        Config.NoDMUsers.RemoveAll(x => x == message.Author.Id);
                        Config.Save();
                        await DirectMessageUser(new DiscordChatUser(message.Author), lang.Strings["dmReStart"]);
                    }
                    else
                        await DirectMessageUser(new DiscordChatUser(message.Author), lang.Strings["dmResp"] + lang.Strings["dmStop"]);

                    return;
                }
                if (message.Channel is SocketGuildChannel)
                {
                    var channel = (SocketGuildChannel)message.Channel;
                    var guild = channel.Guild;


                    var guildConfig = Config.GetServerConfig(guild.Id, ChatTypes.Discord);

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
                        await DoCommand(new DiscordChatMessage(message), parser);
                    }
                    //try to see if a raid was posted
                    else if (doPost)
                    {
                        var post = parser.ParsePost(new DiscordChatMessage(message), Config);
                        await DoPost(post, new DiscordChatMessage(message), parser, new DiscordChatChannel(outputchannel));
                    }


                    await dbContext.AddOrUpdateGuild(guild, guildConfig.City);
                    await dbContext.AddOrUpdateChannel(channel, guildConfig.ChannelCities.ContainsKey(channel.Id) ? guildConfig.ChannelCities[channel.Id] : null);
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
        public async Task PurgePosts()
        {
            try
            {
                var now = DateTime.Now;
                int remaining = 0, deleted = 0;
                foreach (var guild in bot.Guilds)
                {
                    var deletedPosts = new List<PokemonRaidPost>();
                    var posts = Config.GetServerConfig(guild.Id, ChatTypes.Discord).Posts;
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
                        var messages = new List<IMessage>();

                        if (post.OutputMessageId != default(ulong))
                        {
                            var outputChannel = GetChannel(post.OutputChannelId);
                            try
                            {
                                var m = new IMessage[] { await outputChannel.GetMessageAsync(post.OutputMessageId) };
                                messages.AddRange(m.Where(x => x != null));
                            }
                            catch (Exception e)
                            {
                                DoError(e);
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
                        }

                        foreach (var channelMessage in post.ChannelMessages)
                        {
                            var fromChannel = GetChannel(channelMessage.Key);


                            if (channelMessage.Value.MessageId != default(ulong))
                            {
                                var m1 = new IMessage[] { await fromChannel.GetMessageAsync(channelMessage.Value.MessageId) };

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
        private async Task DoCommand(IChatMessage message, MessageParser parser)
        {
            var executor = new BotCommandHandler(this, message, parser);
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
        public async Task DoPost(PokemonRaidPost post, IChatMessage message, MessageParser parser, IChatChannel outputchannel, bool force = false)
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
                    try
                    {
                        if ((post.LatLong == null || !post.LatLong.HasValue) && !string.IsNullOrWhiteSpace(post.Location))
                        {
                            var guildConfig = Config.GetServerConfig(message.Channel.Server.Id, ChatTypes.Discord);

                            if (guildConfig.Places.ContainsKey(post.Location.ToLower()))
                                post.LatLong = guildConfig.Places[post.Location.ToLower()];
                            else
                                post.LatLong = await parser.GetLocationLatLong(post.FullLocation, message.Channel, Config);
                        }

                        await MakePost(post, parser);
                        Config.Save();
                    }
                    catch (Exception e)
                    {
                        DoError(e);
                    }
                    finally
                    {
                        if (d != null) d.Dispose();
                    }

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
        /// <param name="parser"></param>
        public async Task<List<IChatMessage>> MakePost(PokemonRaidPost post, MessageParser parser)
        {
            var results = new List<IChatMessage>();
            var output = new DiscordMessageOutput(parser.Lang, parser.TimeOffset);
            try
            {
                post = await dbContext.AddOrUpdatePost(post);
            }
            catch (Exception e)
            {
                DoError(e);
            }

            try
            {
                var newMessages = new Dictionary<ulong, ulong>();
                var channelNum = 0;
                var outputChannel = GetChannel(post.OutputChannelId);
                //IUserMessage messageResult;
                IChatEmbed headerEmbed, responsesEmbed;
                string mentionString, channelString;
                var guildConfig = Config.GetServerConfig(post.GuildId, ChatTypes.Discord);

                output.MakePostWithEmbed(post, guildConfig, out headerEmbed, out responsesEmbed, out channelString, out mentionString);

                foreach (var channelMessage in post.ChannelMessages)
                {
                    var fromChannel = GetChannel(channelMessage.Key);

                    if (fromChannel != null && fromChannel is SocketGuildChannel)
                    {
                        if (!guildConfig.PinChannels.Contains(fromChannel.Id)) continue;

                        var fromChannelMessage = headerEmbed.Description;

                        if (channelMessage.Value.MessageId != default(ulong))
                        {
                            var messageResult1 = (SocketUserMessage)await fromChannel.GetMessageAsync(channelMessage.Value.MessageId);
                            if (messageResult1 != null)
                            {
                                results.Add(new DiscordChatMessage(messageResult1));

                                //only modify post if something changed
                                if (!messageResult1.Embeds.FirstOrDefault()?.Description.Equals(fromChannelMessage, StringComparison.OrdinalIgnoreCase) ?? true)
                                    await messageResult1.ModifyAsync(x => { x.Content = channelNum == 0 ? mentionString : " "; x.Embed = (Embed)headerEmbed.GetEmbed(); });
                            }
                        }
                        else
                        {
                            var messageResult2 = await fromChannel.SendMessageAsync(channelNum == 0 ? mentionString : " ", false, (Embed)headerEmbed.GetEmbed());
                            results.Add(new DiscordChatMessage(messageResult2));
                            try
                            {
                                var options = new RequestOptions();
                                await messageResult2.PinAsync();
                            }
                            catch (Exception e)
                            {
                                DoError(e);
                            }
                            newMessages[channelMessage.Key] = messageResult2.Id;
                        }
                    }
                    channelNum++;
                }

                foreach (var newMessage in newMessages)
                {
                    post.ChannelMessages[newMessage.Key].MessageId = newMessage.Value;
                }

                if (outputChannel != null)
                {

                    if (post.OutputMessageId != default(ulong))
                    {
                        var messageResult3 = (IUserMessage)await outputChannel.GetMessageAsync(post.OutputMessageId);
                        if (messageResult3 != null)
                        {
                            results.Add(new DiscordChatMessage(messageResult3));
                            await messageResult3.ModifyAsync(x => { x.Embed = (Embed)responsesEmbed.GetEmbed(); x.Content = channelString; });
                        }
                    }
                    else
                    {

                        var messageResult4 = await outputChannel.SendMessageAsync(channelString, false, (Embed)responsesEmbed.GetEmbed());
                        results.Add(new DiscordChatMessage(messageResult4));

                        post.OutputMessageId = messageResult4.Id;
                    }
                }
            }
            catch (Exception e)
            {
                DoError(e);
            }
            return results;
        }
        /// <summary>
        /// Use this to notify users when join count changes
        /// </summary>
        /// <param name="user"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task DirectMessageUser(IChatUser user, string message)
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
        public PokemonRaidPost AddPost(PokemonRaidPost post, MessageParser parser, IChatMessage message, bool add = true, bool force = false)
        {
            var guildConfig = Config.GetServerConfig(post.GuildId, ChatTypes.Discord);
            var channelPosts = guildConfig.Posts.Where(x => x.ChannelMessages.Keys.Where(xx => post.ChannelMessages.Keys.Contains(xx)).Count() > 0);
            PokemonRaidPost existing = null;
            if (!force)
            {
                //Check if user in existing post is mentioned, get latest post associated with user
                foreach (var mentionedUser in message.MentionedUsers)
                {
                    existing = channelPosts.OrderByDescending(x => x.Responses.Max(xx => xx.MessageDate))
                        .FirstOrDefault(x => x.ChannelMessages.Keys.Contains(message.Channel.Id)
                            && (x.Responses.Where(xx => xx.UserId == mentionedUser.Id).Count() > 0 || x.JoinedUsers.Where(xx => xx.Id == mentionedUser.Id).Count() > 0)
                            && x.PokemonId == (post.PokemonId == 0 ? x.PokemonId : post.PokemonId)
                            && (string.IsNullOrWhiteSpace(x.Location) || string.IsNullOrWhiteSpace(post.Location) || parser.CompareLocationStrings(x.Location, post.Location)));

                    if (existing != null)
                        break;
                }
                //if location matches, must be same.
                if (existing == null)
                    existing = channelPosts.FirstOrDefault(x => x.PokemonId == (post.PokemonId > 0 ? post.PokemonId : x.PokemonId)
                        && parser.CompareLocationStrings(x.Location, post.Location));

                //Lat long comparison, within 30 meters is treated as same
                //if(existing == null && post.LatLong != null && post.LatLong.HasValue && channelPosts.Where(x => x.LatLong != null && x.LatLong.HasValue).Count() > 0)
                //    existing = channelPosts.Where(x => x.LatLong != null && x.LatLong.HasValue && x.PokemonId == (post.PokemonId > 0 ? post.PokemonId : x.PokemonId))
                //        .FirstOrDefault(x => parser.CompareLocationLatLong(x.LatLong, post.LatLong));

                //Seeing if location and pokemon matches another channel's
                if (existing == null && !string.IsNullOrEmpty(post.Location))
                    existing = guildConfig.Posts.FirstOrDefault(x => x.PokemonId == post.PokemonId
                        && (parser.CompareLocationStrings(x.Location, post.Location)/* || parser.CompareLocationLatLong(x.LatLong, post.LatLong)*/));

                //Final fall through, gets latest post in channel either matching pokemon name or user was involved with
                if (existing == null && string.IsNullOrEmpty(post.Location))//if location exists and doesn't match, not a match
                    existing = channelPosts
                        .Where(x => string.IsNullOrWhiteSpace(post.Location) || x.UserId == post.UserId)
                        .OrderByDescending(x => x.PostDate)
                        .OrderBy(x => x.PokemonId == post.PokemonId ? 0 : 1)//pokemon name match takes priority if the user responded to multiple raids in the channel
                        .FirstOrDefault(x =>
                            x.ChannelMessages.Keys.Intersect(post.ChannelMessages.Keys).Count() > 0//Posted in the same channel
                            && ((post.PokemonId != default(int) && x.PokemonId == post.PokemonId)//Either pokemon matches OR
                                || (post.PokemonId == default(int) && (x.Responses.Where(xx => xx.UserId == post.UserId).Count() > 0)
                                    || post.PokemonId == default(int) && x.JoinedUsers.Where(xx => xx.Id == post.UserId).Count() > 0))//User already in the thread
                        );
            }

            if (existing != null)
            {
                if (add) existing = MergePosts(existing, post);
                existing.IsValid = existing.IsExisting = true;
                return existing;
            }
            else if (add && post.IsValid)
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
                    post1.JoinedUsers.Add(new PokemonRaidJoinedUser(user.Id, post1.GuildId, post1.UniqueId, user.Name, user.PeopleCount, false, false, user.ArriveTime));
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
                        else if (user.PeopleCount > 0) postUser.PeopleCount = user.PeopleCount;

                        if (user.ArriveTime.HasValue) postUser.ArriveTime = user.ArriveTime;//always update arrive time if present
                    }
                }
            }

            post1.ChannelMessages = post1.ChannelMessages.Concat(post2.ChannelMessages.Where(x => !post1.ChannelMessages.ContainsKey(x.Key))).ToDictionary(x => x.Key, y => y.Value);

            //foreach (var key in post1.ChannelMessages.Keys.Where(x => post2.ChannelMessages.Keys.Contains(x)))
            //{
            //    post2.ChannelMessages.Remove(key);
            //}

            post1.Responses.AddRange(post2.Responses);
            post1.MentionedRoleIds.AddRange(post2.MentionedRoleIds.Where(x => !post1.MentionedRoleIds.Contains(x)));

            return post1;
        }
        /// <summary>
        /// Delete a post from both chat and the list.
        /// </summary>
        /// <param name="post"></param>
        public async Task<bool> DeletePost(PokemonRaidPost post, ulong userId, bool isAdmin, bool purge = true)
        {
            if (isAdmin)
            {
                post.EndDate = DateTime.MinValue;

                await dbContext.MarkPostDeleted(post);

                if (purge) await PurgePosts();
                return true;
            }

            var delChannels = new List<ulong>();
            var delMessages = new List<IMessage>();

            foreach (var channelMessage in post.ChannelMessages)
            {
                if (channelMessage.Value.UserId != userId)
                    continue;

                var channel = bot.GetChannel(channelMessage.Key);

                if (channel != null && channel is ISocketMessageChannel)
                {
                    var message = await ((ISocketMessageChannel)channel).GetMessageAsync(channelMessage.Value.MessageId);

                    delChannels.Add(channelMessage.Key);
                    delMessages.Add(message);
                }
            }

            if (delChannels.Count() == post.ChannelMessages.Count())//poster was the only one posting, get rid of all
            {
                post.EndDate = DateTime.MinValue;

                await dbContext.MarkPostDeleted(post);

                if (purge) await PurgePosts();
                return true;
            }

            foreach (var channel in delChannels)
            {
                post.ChannelMessages.Remove(channel);
            }

            var tasks = new List<Task>();
            foreach (var message in delMessages)
            {
                tasks.Add(message.DeleteAsync());
            }
            Task.WaitAll(tasks.ToArray());

            return delChannels.Count() > 0;
        }
        /// <summary>
        /// Posts a message from a command into chat.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task MakeCommandMessage(IChatChannel channel, string message)
        {
            await channel.SendMessageAsync($"```{message}```");
        }

        public int GetPostCount(int days, ulong serverId)
        {
            return dbContext.Posts.Where(x => x.PostedDate > DateTime.Now.AddDays(days * -1) && x.ServerId == serverId).Count();
        }

        public List<IGrouping<PokemonEntity, RaidPostEntity>> GetBossAggregates(int count = 5, Expression <Func<RaidPostEntity, bool>> where = null)
        {
            if (where == null) where = x => x.PostedDate > DateTime.Now.AddDays(-7);
            return dbContext.Posts.Where(where).GroupBy(x => x.Pokemon).OrderByDescending(x => x.Count()).Take(count).ToList();
        }
        

        private void JoinCount_Changed(object sender, JoinedCountChangedEventArgs e)
        {
            PokemonRaidPost post = null;
            ServerConfiguration guildConfig = null;
            PokemonRaidJoinedUser joinedUser = null;

            if (sender is PokemonRaidPost)
            {
                post = (PokemonRaidPost)sender;
                guildConfig = Config.GetServerConfig(post.GuildId, ChatTypes.Discord);
            }
            else if (sender is PokemonRaidJoinedUser)
            {
                joinedUser = (PokemonRaidJoinedUser)sender;
                guildConfig = Config.GetServerConfig(joinedUser.GuildId, ChatTypes.Discord);
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
                        tasks.Add(DirectMessageUser(new DiscordChatUser(user), $"{message}\n{parser.Language.Strings["dmStop"]}"));//TODO
                }
                Task.WaitAll(tasks.ToArray());
            }
        }

        private ISocketMessageChannel GetChannel(ulong id)
        {
            if (channelCache.ContainsKey(id)) return channelCache[id];
            
            return channelCache[id] = (ISocketMessageChannel)bot.GetChannel(id);
        }
        private MessageParser GetParser(ServerConfiguration guildConfig)
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
            return Config.GetServerConfig(guildId, ChatTypes.Discord).Posts.FirstOrDefault(x => x.UniqueId == uid);
        }
    }
}