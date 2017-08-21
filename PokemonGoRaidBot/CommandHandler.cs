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

namespace PokemonGoRaidBot
{
    public class CommandHandler
    {
        private CommandService commands;
        private DiscordSocketClient bot;
        private IServiceProvider map;
        public readonly BotConfig Config;
        public readonly List<PokemonRaidPost> Posts;

        public CommandHandler(IServiceProvider provider, BotConfig botconfig)
        {
            Config = botconfig;
            map = provider;
            bot = map.GetService<DiscordSocketClient>();

            //Send user message to get handled
            bot.MessageReceived += HandleCommand;
            commands = map.GetService<CommandService>();
            Posts = new List<PokemonRaidPost>();
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

                ISocketMessageChannel outputchannel = null;
                var pin = false;

                if (Config.PinChannels.Contains(message.Channel.Id))
                {
                    pin = true;
                    //outputchannel = message.Channel;
                }

                if (Config.ServerChannels.ContainsKey(guild.Id))
                    outputchannel = (ISocketMessageChannel)guild.Channels.FirstOrDefault(x => x.Id == Config.ServerChannels[guild.Id]);
                else
                    outputchannel = (ISocketMessageChannel)guild.Channels.FirstOrDefault(x => x.Name == Config.OutputChannel);

                var context = new SocketCommandContext(bot, message);

                var lang = Config.ServerLanguages.ContainsKey(guild.Id) ? Config.ServerLanguages[guild.Id] : "en-us";
                var botTimezone = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).Hours - (TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) ? 1 : 0);
                var serverTimezone = Config.ServerTimezones.ContainsKey(guild.Id) ? Config.ServerTimezones[guild.Id] : botTimezone;

                var parser = new MessageParser(lang, serverTimezone - botTimezone);

                var argPos = 0;
                if (message.HasStringPrefix(Config.Prefix, ref argPos))
                {//Someone is issuing a command, respond in their channel
                    await DoCommand(message, parser);
                }
                else if ((outputchannel != null || pin) && message.MentionedUsers.Count() > 0)
                {//possibly a response to someone who posted a raid
                    await DoResponse(message, parser);
                }
                else if (outputchannel != null || pin)
                {//try to see if a raid was posted
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
            var deletedPosts = new List<PokemonRaidPost>();
            var now = DateTime.Now;

            foreach (var post in Posts)
            {
                if (post.EndDate < now) deletedPosts.Add(post);
            }
            Posts.RemoveAll(x => deletedPosts.Contains(x));

            foreach (var post in deletedPosts)
            {
                var messages = new List<IMessage>();
                foreach(var messageId in post.OutputMessageIds)
                {
                    var m = new IMessage[] { await post.OutputChannel.GetMessageAsync(messageId) };
                    messages.AddRange(m.Where(x => x != null));
                }

                try
                {
                    await post.OutputChannel.DeleteMessagesAsync(messages);
                }
                catch (Exception e)
                {
                    DoError(e);
                }

                var m1 = new IMessage[] { await post.FromChannel.GetMessageAsync(post.MessageId) };

                if(m1.Count() > 0 && m1[0] != null)
                {
                    if (m1[0] is RestUserMessage && ((RestUserMessage)m1[0]).IsPinned)
                        await ((RestUserMessage)m1[0]).UnpinAsync();

                    await m1[0].DeleteAsync();
                }

            }
            
            //if (deleteTasks.Count() > 0)
            //    Task.WaitAll(deleteTasks.ToArray());

            //Clean up old messages
            foreach (var guild in bot.Guilds)
            {
                SocketGuildChannel outputchannel;

                if (Config.ServerChannels.ContainsKey(guild.Id))
                    outputchannel = guild.GetChannel(Config.ServerChannels[guild.Id]);
                else
                    outputchannel = guild.Channels.FirstOrDefault(x => x.Name == Config.OutputChannel);

                if(outputchannel != null)
                {
                    var messagesAsync = ((ISocketMessageChannel)outputchannel).GetMessagesAsync();
                    using(var enumerator = messagesAsync.GetEnumerator())
                    {
                        while (await enumerator.MoveNext())
                        {
                            var messages = enumerator.Current;
                            foreach(var message in messages)
                            {
                                if (message.CreatedAt < DateTimeOffset.Now.AddHours(2))
                                {
                                    try
                                    { 
                                        await message.DeleteAsync();
                                    }
                                    catch(Exception e)
                                    {
                                        DoError(e);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Output an error to the bot console.
        /// </summary>
        /// <param name="e"></param>
        private void DoError(Exception e)
        {
            Console.BackgroundColor = ConsoleColor.Yellow;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("Error: {0}", e.Message);
            Console.Write(e.StackTrace);
            Console.WriteLine("---");
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
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
                var post = parser.ParsePost(message, Config);
                var pokemon = post?.Pokemon;

                var mentionPost = Posts.OrderByDescending(x => x.EndDate)
                    .FirstOrDefault(x => x.FromChannel.Id == message.Channel.Id
                        && x.Responses.Where(xx => xx.UserId == mentionedUser.Id).Count() > 0
                        && x.Pokemon.Name == (pokemon ?? x.Pokemon).Name);

                if (mentionPost != null)
                {
                    mentionPost.Responses.Add(new PokemonMessage(mentionedUser.Id, mentionedUser.Username, message.Content, DateTime.Now));
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
            var post = parser.ParsePost(message, Config);

            if (post != null)
            {
                post.OutputChannel = outputchannel;
                post.Pin = pin;
                post = AddPost(post, parser);

                if (post.Pokemon != null)
                    await MakePost(post, parser);
            }
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
                if (post.Pin && post.MessageId != default(ulong))
                {
                    deleteMessage = await post.FromChannel.GetMessageAsync(post.MessageId);
                    if (deleteMessage.IsPinned)
                    {
                        await ((RestUserMessage)deleteMessage).UnpinAsync();
                    }
                    await deleteMessage.DeleteAsync();
                }

                foreach (var messageId in post.OutputMessageIds)
                {
                    deleteMessage = await post.OutputChannel.GetMessageAsync(messageId);
                    await deleteMessage.DeleteAsync();
                }
            }
            catch (Exception e)
            {
                DoError(e);
            }

            RestUserMessage messageResult;

            var messages = parser.MakePostStrings(post);

            if (post.Pin)
            {
                messageResult = await post.FromChannel.SendMessageAsync(messages[0]);
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

            post.OutputMessageIds.Clear();
            if (post.OutputChannel != null)
            {
                foreach (var message in messages)
                {
                    messageResult = await post.OutputChannel.SendMessageAsync(message);
                    post.OutputMessageIds.Add(messageResult.Id);
                }
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
            var channelPosts = Posts.Where(x => x.FromChannel.Id == post.FromChannel.Id);

            //if location matches, must be same.
            var existing = channelPosts.FirstOrDefault(x => parser.CompareLocations(x.Location, post.Location));

            if (existing == null)
                existing = channelPosts
                    .Where(x => string.IsNullOrEmpty(x.Location) || string.IsNullOrEmpty(post.Location))//either location must be unidentified at this point
                    .OrderBy(x => x.Pokemon.Name == (post.Pokemon == null ? "" : post.Pokemon.Name) ? 0 : 1)//pokemon name match takes priority if the user responded to multiple raids in the channel
                    .FirstOrDefault(x =>
                        x.FromChannel.Id == post.FromChannel.Id//Posted in the same channel
                        && ((post.Pokemon != null && x.Pokemon.Name == post.Pokemon.Name)//Either pokemon matches OR
                            || (post.Pokemon == null && x.Responses.Where(xx => xx.UserId == post.UserId).Count() > 0))//User already in the thread
                    );

            if (existing != null)
            {
                if (post.HasEndDate)
                {
                    existing.HasEndDate = true;
                    existing.EndDate = post.EndDate;
                }

                if (!string.IsNullOrEmpty(post.Location) && string.IsNullOrEmpty(existing.Location)) existing.Location = post.Location;

                //overwrite with new values
                foreach(var user in post.JoinedUsers)
                    existing.JoinedUsers[user.Key] = user.Value;

                existing.Responses.Add(post.Responses[0]);

                foreach(var joinuser in post.JoinedUsers)
                {
                    if(!existing.JoinedUsers.ContainsKey(joinuser.Key) || existing.JoinedUsers[joinuser.Key] == 1)
                        existing.JoinedUsers[joinuser.Key] = joinuser.Value;
                }

                return existing;
            }
            else if(post.Pokemon != null) Posts.Add(post);
            return post;
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
    }
}