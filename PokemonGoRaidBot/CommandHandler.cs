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

            var guild = ((SocketGuildChannel)message.Channel).Guild;

            ISocketMessageChannel outputchannel = null;

            if (config.ServerChannels.ContainsKey(guild.Id))
                outputchannel = (ISocketMessageChannel)guild.Channels.FirstOrDefault(x => x.Id == config.ServerChannels[guild.Id]);

            if (outputchannel == null)
                outputchannel = (ISocketMessageChannel)guild.Channels.FirstOrDefault(x => x.Name == config.OutputChannel);

            //do nothing if output channel is not available.
            //if (outputchannel == null) outputchannel = message.Channel;
            
            var context = new SocketCommandContext(bot, message);
            
            var argPos = 0;
            if (message.HasStringPrefix(config.Prefix, ref argPos))
            {//Someone is issuing a command, respond in their channel
                await DoCommand(message);
            }
            else if (outputchannel != null && message.MentionedUsers.Count() > 0)
            {//possibly a response to someone who posted a raid
                await DoResponse(message);
            }
            else if(outputchannel != null)
            {//try to see if a raid was posted
                await DoPost(message, outputchannel);
            }
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
                foreach(var messageId in post.MessageIds)
                {
                    var m = new IMessage[] { await post.OutputChannel.GetMessageAsync(messageId) };
                    deleteTasks.Add(post.OutputChannel.DeleteMessagesAsync(m));
                }
            }
            if (deleteTasks.Count() > 0)
                Task.WaitAll(deleteTasks.ToArray());
        }
        private async Task DoCommand(SocketUserMessage message)
        {
            var command = message.Content.Substring(config.Prefix.Length).Split(' ');
            var user = (SocketGuildUser)message.Author;
            var guild = ((SocketGuildChannel)message.Channel).Guild;

            var isAdmin = user.GuildPermissions.Administrator;
            var isMod = user.Roles.Where(x => x.Name.ToLowerInvariant().StartsWith("mod")).Count() > 0;

            switch (command[0].ToLowerInvariant())
            {
                case "info":
                    if (command.Length > 1 && command[1].Length > 2)
                    {
                        var info = MessageParser.ParsePokemon(command[1], config, guild.Id);
                        var response = "```";

                        if (info != null)
                            response += MessageParser.MakeInfoLine(info, guild.Id);
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

                        if (command.Length > 0 && Int32.TryParse(command[1], out tierCommand))
                        {
                            list = list.Where(x => x.Tier == tierCommand);
                        }

                        var orderedList = list.OrderByDescending(x => x.Id).OrderByDescending(x => x.Tier);


                        var maxBossLength = orderedList.Select(x => x.BossNameFormatted.Length).Max();
                        strings.Add("```");
                        foreach (var info in orderedList)
                        {
                            var lineStr = MessageParser.MakeInfoLine(info, guild.Id, maxBossLength);

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
                case "channel":
                    if(isMod || isAdmin)
                    { 
                        if(command.Length > 1 && !string.IsNullOrEmpty(command[1]))
                        {
                            var channel = guild.Channels.FirstOrDefault(x => x.Name.ToLowerInvariant() == command[1].ToLowerInvariant());
                            if (channel != null)
                            {
                                config.ServerChannels[guild.Id] = channel.Id;
                                config.Save();
                                await message.Channel.SendMessageAsync($"```Output channel for {guild.Name} changed to {channel.Name}```");
                            }
                            else
                            {
                                await message.Channel.SendMessageAsync($"```{guild.Name} does not contain a channel named \"{command[1]}\"```");
                            }
                        }
                        else
                        {
                            config.ServerChannels.Remove(guild.Id);
                            config.Save();
                            await message.Channel.SendMessageAsync($"```Output channel override for {guild.Name} removed, default value {config.OutputChannel} will be used.```");
                        }
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"```You do not have the necessary permissions to change this setting.  You must be a server moderator or administrator to make this change.```");
                    }
                    break;
                case "alias"://todo restrict access to certain users/roles
                    if (isMod || isAdmin)
                    {
                        PokemonInfo foundInfo = null;
                        foreach (var info in config.PokemonInfoList)
                        {
                            if (info.Name.Equals(command[1], StringComparison.OrdinalIgnoreCase))
                            {
                                info.ServerAliases.Add(new KeyValuePair<ulong, string>(guild.Id, command[2].ToLowerInvariant()));
                                foundInfo = info;
                                config.Save();
                                break;
                            }
                        }
                        var resp = $"```Pokemon matching '{command[1]}' not found.```";

                        if (foundInfo != null)
                            resp = $"```Alias '{command[2].ToLowerInvariant()}' added to '{foundInfo.Name}'```";

                        await message.Channel.SendMessageAsync(resp);
                        break;
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"```You do not have the necessary permissions to change this setting.  You must be a server moderator or administrator to make this change.```");
                    }
                    break;
                case "removealias":
                    if (isMod || isAdmin)
                    {
                        var resp = "";
                        foreach (var info in config.PokemonInfoList)
                        {
                            if (info.Name.Equals(command[1], StringComparison.OrdinalIgnoreCase))
                            {
                                var alias = info.ServerAliases.FirstOrDefault(x => x.Key == guild.Id && x.Value == command[2].ToLowerInvariant());
                                
                                if(!alias.Equals(default(KeyValuePair<ulong, string>)))
                                {
                                    info.ServerAliases.Remove(alias);
                                    config.Save();
                                    resp = $"```Alias {alias.Value} removed from pokemon {info.Name}```";
                                }
                                else
                                {
                                    resp = $"```Alias \"{alias.Value}\" not found on pokemon {info.Name}.  ";
                                    var aliases = info.ServerAliases.Where(x => x.Key == guild.Id);
                                    resp += aliases.Count() > 0 ? "Aliases that can be removed are: " + string.Join(", ", aliases.Select(x => x.Value)) : $"No aliases found for {info.Name}.";
                                    resp += "```";
                                }
                                break;
                            }
                        }
                        await message.Channel.SendMessageAsync(resp);
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"```You do not have the necessary permissions to change this setting.  You must be a server moderator or administrator to make this change.```");
                    }
                    break;
                case "help":
                    var helpmessage = $"```This bot parses discord chat messages to see if a raid is being mentioned.  If a text channel exists on the discord server called '{config.OutputChannel}', it posts the raids there and combines any responses to the raid in a cleaner fashion.\n\n";
                    helpmessage += "``````css\n       #Commands:\n";
                    helpmessage += $"  {config.Prefix}info [boss name (optional)] - Displays information about the selected raid, or all of the raids above rank 3.  Information was taken from https://pokemongo.gamepress.gg.\n";
                    helpmessage += $"  {config.Prefix}channel [channel name (optional)] - Changes the bot output channel on this server to the value passed in for [channel name].  If none, the override is removed and the default value '{config.OutputChannel}' is used.  Moderator or admin privileges required.\n";
                    helpmessage += $"  {config.Prefix}alias [pokemon] [alias] - Adds an alias for a pokemon.  Moderator or admin privileges required.\n";
                    helpmessage += $"  {config.Prefix}removealias [pokemon] [alias] - Removes an alias for a pokemon.  Moderator or admin privileges required.\n";
                    helpmessage += $"  {config.Prefix}help - Shows this message.";
                    helpmessage += "```";
                    await message.Channel.SendMessageAsync(helpmessage);
                    break;
                default:
                    await message.Channel.SendMessageAsync($"```Unknown command \"{command[0]}\".  Type {config.Prefix}help to see valid commands for this bot.```");
                    break;
            }
        }
        private async Task DoResponse(SocketUserMessage message)
        {
            foreach (var mentionedUser in message.MentionedUsers)
            {
                var post = MessageParser.ParseMessage(message, config);
                var pokemon = post?.Pokemon;

                var mentionPost = posts.OrderByDescending(x => x.EndDate)
                    .FirstOrDefault(x => x.FromChannel.Id == message.Channel.Id
                        && x.Responses.Where(xx => xx.Username == mentionedUser.Username).Count() > 0
                        && x.Pokemon.Name == (pokemon ?? x.Pokemon).Name);

                if (mentionPost != null)
                {
                    mentionPost.Responses.Add(new PokemonMessage(mentionedUser.Username, message.Content));
                    await MakePost(mentionPost);
                }
            }
        }
        private async Task DoPost(SocketUserMessage message, ISocketMessageChannel outputchannel)
        {
            var post = MessageParser.ParseMessage(message, config);
            post.OutputChannel = outputchannel;

            if (post != null)
            {
                post = AddPost(post);

                if (post.Pokemon != null)
                    await MakePost(post);
            }
        }
        
        /// <summary>
        /// Creates the text string and outputs the post message into the channel.
        /// If post.MessageId is populated, will delete and recreate the message.
        /// </summary>
        /// <param name="post"></param>
        /// <param name="outputchannel"></param>
        private async Task MakePost(PokemonRaidPost post)
        {
            var messages = MessageParser.MakePostStrings(post);

            var newMessageIds = new List<ulong>();

            foreach(var messageId in post.MessageIds)
            {
                if (messageId != default(ulong))
                {
                    var m = await post.OutputChannel.GetMessageAsync(messageId);
                    await post.OutputChannel.DeleteMessagesAsync(new IMessage[] { m });
                }
            }

            post.MessageIds.Clear();

            foreach(var message in messages)
            {
                var messageResult = await post.OutputChannel.SendMessageAsync(message);
                post.MessageIds.Add(messageResult.Id);
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
        private PokemonRaidPost AddPost(PokemonRaidPost post)
        {
            var existing = posts.OrderBy(x => x.Pokemon.Name == (post.Pokemon == null ? "" : post.Pokemon.Name) ? 0 : 1)//pokemon name match takes priority if the user responded to multiple raids in the channel
                .FirstOrDefault(x => 
                    x.FromChannel.Id == post.FromChannel.Id//Posted in the same channel
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