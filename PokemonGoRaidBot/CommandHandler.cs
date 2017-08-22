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

namespace PokemonGoRaidBot
{
    public class CommandHandler
    {
        private CommandService commands;
        private DiscordSocketClient bot;
        private IServiceProvider map;

        public RaidLogger Logger;
        public readonly BotConfig Config;
        //public readonly List<PokemonRaidPost> Posts;

        Dictionary<ulong, ISocketMessageChannel> channelCache;

        public CommandHandler(IServiceProvider provider, BotConfig botconfig, RaidLogger logger)
        {
            map = provider;
            bot = map.GetService<DiscordSocketClient>();

            Logger = logger;
            Config = botconfig;

            //Send user message to get handled
            bot.MessageReceived += HandleCommand;
            commands = map.GetService<CommandService>();
            //Posts = new List<PokemonRaidPost>();
            channelCache = new Dictionary<ulong, ISocketMessageChannel>();
        }
        
        public async Task ConfigureAsync()
        {
            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
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

                var guild = ((SocketGuildChannel)message.Channel).Guild;
                var guildConfig = Config.GetGuildConfig(guild.Id);

                ISocketMessageChannel outputchannel = null;
                var pin = guildConfig.PinChannels.Contains(message.Channel.Id);

                //get output channel
                if (guildConfig.OutputChannelId.HasValue)
                    outputchannel = (ISocketMessageChannel)guild.Channels.FirstOrDefault(x => x.Id == guildConfig.OutputChannelId.Value);
                else
                    outputchannel = (ISocketMessageChannel)guild.Channels.FirstOrDefault(x => x.Name == Config.OutputChannel);

                var context = new SocketCommandContext(bot, message);
                //get configured guild language or default "en-us"
                var lang = guildConfig.Language ?? "en-us";
                //timezone of the bot machine
                var botTimezone = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).Hours - (TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) ? 1 : 0);
                //get configured timezone
                var serverTimezone = guildConfig.Timezone ?? botTimezone;

                var parser = new MessageParser(lang, serverTimezone - botTimezone);

                var doPost = (outputchannel != null || pin) && !guildConfig.MuteChannels.Contains(message.Channel.Id);

                var argPos = 0;

                //begin parsing/execution
                //Someone is issuing a command, respond in their channel
                if (message.HasStringPrefix(Config.Prefix, ref argPos))
                {
                    await DoCommand(message, parser);
                }
                //possibly a response to someone who posted a raid
                else if (doPost && message.MentionedUsers.Count() > 0)
                {
                    await DoResponse(message, parser);
                }
                //try to see if a raid was posted
                else if (doPost)
                {
                    await DoPost(message, parser, outputchannel, pin);
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
        public async void PurgePosts(Object stateInfo = null)
        {
            var now = DateTime.Now;
            int remaining = 0, deleted = 0;
            foreach(var guild in bot.Guilds)
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

                    foreach (var messageId in post.OutputMessageIds)
                    {
                        var m = new IMessage[] { await outputChannel.GetMessageAsync(messageId) };
                        messages.AddRange(m.Where(x => x != null));
                    }

                    try
                    {
                        await outputChannel.DeleteMessagesAsync(messages);
                        posts.Remove(post);
                    }
                    catch (Exception e)
                    {
                        DoError(e);
                    }

                    if(post.MessageId > 0)
                    {
                        var m1 = new IMessage[] { await fromChannel.GetMessageAsync(post.MessageId) };

                        if (m1.Count() > 0 && m1[0] != null)
                        {
                            if (m1[0] is RestUserMessage && ((RestUserMessage)m1[0]).IsPinned)
                                await ((RestUserMessage)m1[0]).UnpinAsync();

                            await m1[0].DeleteAsync();
                        }
                    }
                }
            }
            Config.Save();
            await Logger.Log(new LogMessage(LogSeverity.Debug, "handler", string.Format("deleted:{0}; remaining:{1}", deleted, remaining)));
        }
        /// <summary>
        /// Output an error to the bot console.
        /// </summary>
        /// <param name="e"></param>
        public void DoError(Exception e, string source = "handler")
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
        /// Adds a message to an existing raid post.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="parser"></param>
        /// <returns></returns>
        private async Task DoResponse(SocketUserMessage message, MessageParser parser)
        {
            foreach (var mentionedUser in message.MentionedUsers)
            {
                var post = await parser.ParsePost(message, Config);
                var pokemon = Config.PokemonInfoList.FirstOrDefault(x=> x.Id == post?.PokemonId);

                var mentionPost = Config.GetGuildConfig(((SocketGuildChannel)message.Channel).Guild.Id).Posts.OrderByDescending(x => x.Responses.Max(xx => xx.MessageDate))
                    .FirstOrDefault(x => x.FromChannelId == message.Channel.Id
                        && x.Responses.Where(xx => xx.UserId == mentionedUser.Id).Count() > 0
                        && x.PokemonId == (pokemon != null ? pokemon.Id : x.PokemonId));

                if (mentionPost != null)
                {
                    mentionPost = MergePosts(mentionPost, post);
                    await MakePost(mentionPost, parser);
                }
            }
        }
        /// <summary>
        /// Performs the raid post behavior
        /// </summary>
        /// <param name="message"></param>
        /// <param name="parser"></param>
        /// <param name="outputchannel"></param>
        /// <param name="pin"></param>
        /// <returns></returns>
        private async Task DoPost(SocketUserMessage message, MessageParser parser, ISocketMessageChannel outputchannel, bool pin)
        {
            var post = await parser.ParsePost(message, Config);

            if (post != null)
            {
                post.OutputChannelId = outputchannel?.Id ?? 0;
                post.Pin = pin;
                post.GuildId = ((SocketGuildChannel)message.Channel).Guild.Id;
                post = AddPost(post, parser);

                if (post.PokemonId != default(int))
                { 
                    await MakePost(post, parser);
                }
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
                IMessage deleteMessage;
                var fromChannel = GetChannel(post.FromChannelId);
                var outputChannel = GetChannel(post.OutputChannelId);

                if (post.Pin && post.MessageId != default(ulong))
                {
                    if(fromChannel != null)
                    {
                        deleteMessage = await fromChannel.GetMessageAsync(post.MessageId);
                        if (deleteMessage.IsPinned)
                        {
                            await ((RestUserMessage)deleteMessage).UnpinAsync();
                        }
                        await deleteMessage.DeleteAsync();
                    }
                }
                if(outputChannel != null)
                {
                    foreach (var messageId in post.OutputMessageIds)
                    {
                        deleteMessage = await outputChannel.GetMessageAsync(messageId);
                        await deleteMessage.DeleteAsync();
                    }
                }

                RestUserMessage messageResult;

                var messages = parser.MakePostStrings(post);

                if (post.Pin)
                {
                    if(fromChannel != null)
                    {
                        messageResult = await fromChannel.SendMessageAsync(messages[0]);
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

                post.OutputMessageIds.Clear();
                if (outputChannel != null)
                {
                    foreach (var message in messages)
                    {
                        messageResult = await outputChannel.SendMessageAsync(message);
                        post.OutputMessageIds.Add(messageResult.Id);
                    }
                }
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
        public PokemonRaidPost AddPost(PokemonRaidPost post, MessageParser parser)
        {
            var guildConfig = Config.GetGuildConfig(post.GuildId);
            var channelPosts = guildConfig.Posts.Where(x => x.FromChannelId == post.FromChannelId);

            //if location matches, must be same.
            var existing = channelPosts.FirstOrDefault(x => parser.CompareLocationStrings(x.Location, post.Location));

            if(existing == null && post.LatLong.HasValue && channelPosts.Where(x => x.LatLong.HasValue).Count() > 0)
                existing = channelPosts.Where(x => x.LatLong.HasValue && x.PokemonId == (post.PokemonId > 0 ? post.PokemonId : x.PokemonId))
                    .FirstOrDefault(x => parser.CompareLocationLatLong(x.LatLong.Value, post.LatLong.Value));

            if (existing == null)
                existing = channelPosts
                    .Where(x => string.IsNullOrEmpty(x.Location) || string.IsNullOrEmpty(post.Location))//either location must be unidentified at this point
                    .OrderBy(x => x.PokemonId == post.PokemonId ? 0 : 1)//pokemon name match takes priority if the user responded to multiple raids in the channel
                    .FirstOrDefault(x =>
                        x.FromChannelId == post.FromChannelId//Posted in the same channel
                        && ((post.PokemonId != default(int) && x.PokemonId == post.PokemonId)//Either pokemon matches OR
                            || (post.PokemonId == default(int) && x.Responses.Where(xx => xx.UserId == post.UserId).Count() > 0))//User already in the thread
                    );

            if (existing != null)
            {
                existing = MergePosts(existing, post);
                return existing;
            }
            else if(post.PokemonId != default(int))
                guildConfig.Posts.Add(post);
            
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
            if (post2.HasEndDate)
            {
                post1.HasEndDate = true;
                post1.EndDate = post2.EndDate;
            }

            if (string.IsNullOrEmpty(post1.Location) && post2.LatLong.HasValue)//only merge location if first is blank and second has lat long
            {
                post1.Location = post2.Location;
                post1.LatLong = post2.LatLong;
            }

            //overwrite with new values
            foreach (var user in post2.JoinedUsers)
                post1.JoinedUsers[user.Key] = user.Value;

            post1.Responses.AddRange(post2.Responses);

            foreach (var joinuser in post2.JoinedUsers)
            {
                if (!post1.JoinedUsers.ContainsKey(joinuser.Key) || post1.JoinedUsers[joinuser.Key] == 1)
                    post1.JoinedUsers[joinuser.Key] = joinuser.Value;
            }
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

        private ISocketMessageChannel GetChannel(ulong id)
        {
            if (channelCache.ContainsKey(id)) return channelCache[id];
            
            return channelCache[id] = (ISocketMessageChannel)bot.GetChannel(id);
        }
    }
}