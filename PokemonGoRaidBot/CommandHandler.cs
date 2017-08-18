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

namespace PokemonGoRaidBot
{
    public class CommandHandler
    {
        private CommandService commands;
        private DiscordSocketClient bot;
        private IServiceProvider map;
        private BotConfig config;
        private List<PokemonRaidPost> posts;
        private ISocketMessageChannel outputChannel;

        public CommandHandler(IServiceProvider provider, BotConfig botconfig)
        {
            config = botconfig;
            map = provider;
            bot = map.GetService<DiscordSocketClient>();

            //Send user message to get handled
            bot.MessageReceived += HandleCommand;
            commands = map.GetService<CommandService>();
            posts = new List<PokemonRaidPost>();
        }
        
        public async Task ConfigureAsync()
        {
            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        public async Task HandleCommand(SocketMessage pMsg)
        {
            var message = pMsg as SocketUserMessage;

            if (message == null || message.Author == null || message.Author.IsBot)
                return;

            var outputchannel = (SocketGuildChannel)outputChannel ?? ((SocketGuildChannel)message.Channel).Guild.Channels.FirstOrDefault(x => x.Name == config.OutputChannel);

            if(outputchannel == null)//Specifically named channel doesn't exist, send message to same channel as user
                outputchannel = (SocketGuildChannel)message.Channel;
            else if (outputChannel == null)
                outputChannel = (ISocketMessageChannel)outputchannel;

            var context = new SocketCommandContext(bot, message);
            
            var argPos = 0;
            if (message.HasStringPrefix(config.Prefix, ref argPos))
            {//Someone is issuing a command, respond in their channel
                await DoCommand(message);
            }
            else if (message.MentionedUsers.Count() > 0)
            {//possibly a response to someone who posted a raid
                await DoResponse(message, outputchannel);
            }
            else
            {//try to see if a raid was posted
                await DoPost(message, outputchannel);
            }
            PurgePosts();
        }
        /// <summary>
        /// Used as a recurring method which will remove old posts to keep the channel clean of expired raids.
        /// </summary>
        /// <param name="stateInfo"></param>
        public async void PurgePosts(Object stateInfo = null)
        {
            var deletedPosts = new List<PokemonRaidPost>();
            var deleteTasks = new List<Task>();
            var now = DateTime.Now;

            foreach (var post in posts)
            {
                if (post.EndDate < now) deletedPosts.Add(post);
            }
            posts.RemoveAll(x => deletedPosts.Contains(x));

            foreach (var post in deletedPosts)
            {
                var channel = outputChannel ?? post.Channel;
                var m = new IMessage[] { await channel.GetMessageAsync(post.MessageId) };
                deleteTasks.Add(channel.DeleteMessagesAsync(m));
            }
            if (deleteTasks.Count() > 0)
                Task.WaitAll(deleteTasks.ToArray());
        }
        private async Task DoCommand(SocketUserMessage message)
        {
            var command = message.Content.Substring(config.Prefix.Length).Split(' ');

            switch (command[0].ToLowerInvariant())
            {
                case "info":
                    if (command.Length > 1 && command[1].Length > 2)
                    {
                        var info = config.PokemonInfoList.FirstOrDefault(x => x.Name.ToLowerInvariant().StartsWith(command[1]) || x.Aliases.Contains(command[1]));
                        var response = "```";

                        if (info != null)
                            response += MessageParser.MakeInfoLine(info);
                        else
                            response += $"'{command[1]}' did not match any raid boss names or aliases.";

                        response += "```";
                        await message.Channel.SendMessageAsync(response);
                    }
                    else
                    {
                        var strings = new List<string>();
                        var strInd = 0;

                        var tierCommand = 0;

                        var list = config.PokemonInfoList.Where(x => x.CatchRate > 0);

                        if (Int32.TryParse(command[1], out tierCommand))
                        {
                            list = list.Where(x => x.Tier == tierCommand);
                        }
                        else
                        {
                            list = list.Where(x => x.Tier > 2);
                        }

                        var orderedList = list.OrderByDescending(x => x.Id).OrderByDescending(x => x.Tier);


                        var maxBossLength = orderedList.Select(x => x.BossNameFormatted.Length).Max();
                        strings.Add("```");
                        foreach (var info in orderedList)
                        {
                            var lineStr = MessageParser.MakeInfoLine(info, maxBossLength);

                            if (strings[strInd].Length + lineStr.Length + 3 < 2000)
                                strings[strInd] += lineStr;
                            else
                            {
                                strings[strInd] += "```";
                                strings.Add("```" + lineStr);
                                strInd++;
                            }

                        }
                        strings[strInd] += "```";
                        foreach (var str in strings)
                            await message.Channel.SendMessageAsync(str);
                    }
                    break;
                case "help":
                    var helpmessage = $"```This bot parses discord chat messages to see if a raid is being mentioned.  If a text channel exists on the discord server called '{config.OutputChannel}', it posts the raids there and combines any responses to the raid in a cleaner fashion.\n\n";
                    helpmessage += "Commands:\n";
                    helpmessage += "  !info [boss name (optional)] - Displays information about the selected raid, or all of the raids above rank 3.  Information was taken from https://pokemongo.gamepress.gg.\n";
                    helpmessage += "  !help - Shows this message.";
                    helpmessage += "```";
                    await message.Channel.SendMessageAsync(helpmessage);
                    break;
                    /*case "alias"://todo restrict access to certain users/roles
                        PokemonInfo foundInfo = null;
                        foreach (var info in config.PokemonInfoList)
                        {
                            if (info.Name.Equals(command[1], StringComparison.OrdinalIgnoreCase))
                            {
                                info.Aliases.Add(command[2].ToLowerInvariant());
                                foundInfo = info;
                                break;
                            }
                        }
                        config.Save();
                        var resp = $"```Pokemon matching '{command[1]}' not found.```";

                        if(foundInfo != null)
                            resp = $"```Alias '{command[2].ToLowerInvariant()}' added to '{foundInfo.Name}'```";

                        await message.Channel.SendMessageAsync(resp);
                        break;*/
            }
        }
        private async Task DoResponse(SocketUserMessage message, SocketGuildChannel outputchannel)
        {
            foreach (var mentionedUser in message.MentionedUsers)
            {
                var post = MessageParser.ParseMessage(message, config);
                var pokemon = post?.Pokemon;

                var mentionPost = posts.OrderByDescending(x => x.EndDate)
                    .FirstOrDefault(x => x.Channel.Name == message.Channel.Name
                        && x.Responses.Where(xx => xx.Username == mentionedUser.Username).Count() > 0
                        && x.Pokemon.Name == (pokemon ?? x.Pokemon).Name);

                if (mentionPost != null)
                {
                    mentionPost.Responses.Add(new PokemonMessage(mentionedUser.Username, message.Content));
                    await MakePost(mentionPost, outputchannel);
                }
            }
        }
        private async Task DoPost(SocketUserMessage message, SocketGuildChannel outputchannel)
        {
            var post = MessageParser.ParseMessage(message, config);

            if (post != null)
            {
                post = AddPost(post);

                if (post.Pokemon != null)
                    await MakePost(post, outputchannel);
            }
        }
        
        /// <summary>
        /// Creates the text string and outputs the post message into the channel.
        /// If post.MessageId is populated, will delete and recreate the message.
        /// </summary>
        /// <param name="post"></param>
        /// <param name="outputchannel"></param>
        private async Task MakePost(PokemonRaidPost post, SocketGuildChannel outputchannel)
        {
            string response = string.Format("__**{0}**__ posted by {1}{2}{3}",
                        post.Pokemon.Name,
                        post.User,
                        outputChannel == null ? "" : $" in <#{post.Channel.Id}>",
                        !post.HasEndDate ? "" : string.Format(", ends around {0:h: mm tt}", post.EndDate));

            response += MessageParser.MakeResponseString(post);

            if (post.MessageId != default(ulong))
            {
                var m = await ((ISocketMessageChannel)outputchannel).GetMessageAsync(post.MessageId);
                await ((ISocketMessageChannel)outputchannel).DeleteMessagesAsync(new IMessage[] { m });
            }
            var result = await ((ISocketMessageChannel)outputchannel).SendMessageAsync(response);
            post.MessageId = result.Id;
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
        private PokemonRaidPost AddPost(PokemonRaidPost post)
        {
            var existing = posts.OrderBy(x => x.Pokemon.Name == (post.Pokemon == null ? "" : post.Pokemon.Name) ? 0 : 1)//pokemon name match takes priority if the user responded to multiple raids in the channel
                .FirstOrDefault(x => 
                    x.Channel.Name == post.Channel.Name//Posted in the same channel
                    && ((post.Pokemon != null && x.Pokemon.Name == post.Pokemon.Name)//Either pokemon matches OR
                        || (post.Pokemon == null && x.Responses.Where(xx => xx.Username == post.User).Count() > 0))//User already in the thread
                );

            if (existing != null)
            {
                if (post.HasEndDate)
                {
                    existing.HasEndDate = true;
                    existing.EndDate = post.EndDate;
                }
                existing.Responses.Add(post.Responses[0]);
                return existing;
            }
            else if(post.Pokemon != null) posts.Add(post);
            return post;
        }
    }
}