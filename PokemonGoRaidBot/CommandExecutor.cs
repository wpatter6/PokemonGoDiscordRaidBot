using Discord.WebSocket;
using PokemonGoRaidBot.Config;
using PokemonGoRaidBot.Objects;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using PokemonGoRaidBot.Parsing;

namespace PokemonGoRaidBot
{
    public class CommandExecutor
    {
        private CommandHandler Handler;
        private SocketUserMessage Message;
        private SocketGuildUser User;
        private SocketGuild Guild;
        private MessageParser Parser;
        private bool IsAdmin;
        private BotConfig Config;
        private GuildConfig GuildConfig;

        private string[] Command;
        //private string noAccessMessage = "You do not have the necessary permissions to change this setting.  You must be a server moderator or administrator to make this change.";

        public CommandExecutor(CommandHandler handler, SocketUserMessage message, MessageParser parser)
        {
            Handler = handler;
            Message = message;
            Parser = parser;

            Config = Handler.Config;
            Command = Message.Content.ToLowerInvariant().Substring(Config.Prefix.Length).Split(' ');

            User = (SocketGuildUser)Message.Author;
            Guild = ((SocketGuildChannel)Message.Channel).Guild;
            GuildConfig = Config.GetGuildConfig(Guild.Id);

            IsAdmin = User.GuildPermissions.Administrator || User.GuildPermissions.ManageGuild;
        }

        public async Task Execute()
        {
            try
            {
                MethodInfo[] methodInfos = GetType()
                               .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
                bool found = false;

                foreach (var method in methodInfos)
                {
                    var attr = method.GetCustomAttribute<RaidBotCommandAttribute>();
                    if (attr != null && attr.Command == Command[0])
                    {
                        Task result = (Task)method.Invoke(this, new object[] { });
                        await result;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandUnknown"], Command[0], Config.Prefix));//$"Unknown Command \"{Command[0]}\".  Type {Config.Prefix}help to see valid Commands for this bot.");
                }
            }
            catch (Exception e)
            {
                Handler.DoError(e, "executor");
            }
        }

        [RaidBotCommand("join")] 
        private async Task Join()
        {
            var post = GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if(post == null)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));// $"Raid post with Id \"{Command[1]}\" does not exist.");
                return;
            }

            int num;
            if(!Int32.TryParse(Command[2], out num))
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandInvalidNumber"], Command[2]));//$"Invalid number of raid joiners \"{Command[2]}\".");
                return;
            }

            post.JoinedUsers[Message.Author.Id] = num;
            await Handler.MakePost(post, Parser);
        }

        [RaidBotCommand("unjoin")]
        private async Task UnJoin()
        {

            var post = GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (post == null)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));
                return;
            }

            post.JoinedUsers.Remove(Message.Author.Id);
            await Handler.MakePost(post, Parser);
        }

        [RaidBotCommand("info")]
        private async Task Info()
        {
            if (Command.Length > 1 && Command[1].Length > 2)
            {
                var info = Parser.ParsePokemon(Command[1], Config, Guild.Id);
                var response = "";

                if (info != null) {
                    response += "```" + Parser.MakeInfoLine(info, Config, Guild.Id) + "```";
                    response += string.Format(Config.LinkFormat, info.Id);
                } else
                    response += "```" + string.Format(Parser.Language.Formats["commandPokemonNotFound"], Command[1]) + "```";//$"'{Command[1]}' did not match any raid boss names or aliases.";

                await Message.Channel.SendMessageAsync(response);
            }
            else
            {
                var strings = new List<string>();
                var strInd = 0;

                var tierCommand = 0;

                var list = Config.PokemonInfoList.Where(x => x.CatchRate > 0);

                if (Command.Length > 1 && Int32.TryParse(Command[1], out tierCommand))
                {
                    list = list.Where(x => x.Tier == tierCommand);
                }

                var orderedList = list.OrderByDescending(x => x.Id).OrderByDescending(x => x.Tier);


                var maxBossLength = orderedList.Select(x => x.BossNameFormatted.Length).Max();
                strings.Add("```");
                foreach (var info in orderedList)
                {
                    var lineStr = Parser.MakeInfoLine(info, Config, Guild.Id, maxBossLength);

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
                    await Message.Channel.SendMessageAsync(str);
            }
        }

        [RaidBotCommand("language")]
        private async Task Language()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }
            
            Config.GetGuildConfig(Guild.Id).Language = Command[1];

            //Config.ServerLanguages[Guild.Id] = Command[1];
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandLanguageSuccess"], Guild.Name, Command[1]));
        }

        [RaidBotCommand("timezone")]
        private async Task Timezone()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }
            int timezoneOut = int.MinValue;
            var isvalid = Int32.TryParse(Command[1], out timezoneOut);

            if(!isvalid || timezoneOut > 12 || timezoneOut < -11)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandInvalidNumber"], Command[1]));
                return;
            }
            Config.GetGuildConfig(Guild.Id).Timezone = timezoneOut;
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandTimezoneSuccess"], Guild.Name, timezoneOut > -1 ? "+" + timezoneOut.ToString() : timezoneOut.ToString()));
        }

        [RaidBotCommand("channel")]
        private async Task Channel()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }
            if (Command.Length > 1 && !string.IsNullOrEmpty(Command[1]))
            {
                var channel = Guild.Channels.FirstOrDefault(x => x.Name.ToLowerInvariant() == Command[1].ToLowerInvariant());
                if (channel != null)
                {
                    Config.GetGuildConfig(Guild.Id).OutputChannelId = channel.Id;
                    //Config.ServerChannels[Guild.Id] = channel.Id;
                    Config.Save();
                    await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelSuccess"], Guild.Name, channel.Name));//// $"Output channel for {Guild.Name} changed to {channel.Name}");
                }
                else
                {
                    await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandGuildNoChannel"], Guild.Name, Command[1]));//$"{Guild.Name} does not contain a channel named \"{Command[1]}\"");
                }
            }
            else
            {
                Config.GetGuildConfig(Guild.Id).OutputChannelId = null;
                //Config.ServerChannels.Remove(Guild.Id);
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelCleared"], Guild.Name, Config.OutputChannel));//$"Output channel override for {Guild.Name} removed, default value \"{Config.OutputChannel}\" will be used.");
            }
        }

        [RaidBotCommand("nochannel")]
        private async Task NoChannel()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }
            Config.GetGuildConfig(Guild.Id).OutputChannelId = 0;
            //Config.ServerChannels[Guild.Id] = 0;
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoChannelSuccess"]);
        }

        [RaidBotCommand("alias")]
        private async Task Alias()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }

            PokemonInfo foundInfo = Parser.ParsePokemon(Command[1], Config, Guild.Id);

            if (foundInfo == null)
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPokemonNotFound"], Command[1]));
            
            if (GuildConfig.PokemonAliases.ContainsKey(foundInfo.Id))
                GuildConfig.PokemonAliases[foundInfo.Id].Add(Command[2].ToLowerInvariant());

            Config.Save();

            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandAliasSuccess"], Command[2].ToLowerInvariant(), foundInfo.Name));
        }
        
        [RaidBotCommand("removealias")]
        private async Task RemoveAlias()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }
            var aresp = "";

            var info = Parser.ParsePokemon(Command[1], Config, Guild.Id);

            //foreach (var info in Config.PokemonInfoList)
            //{
                //if (info.Name.Equals(Command[1], StringComparison.OrdinalIgnoreCase))
                //{
                    var existing = GuildConfig.PokemonAliases.FirstOrDefault(x => x.Value.Contains(Command[2].ToLowerInvariant()));
                    if(!existing.Equals(default(KeyValuePair<int, List<string>>)))
                    {
                        existing.Value.RemoveAll(x => x.Equals(Command[2], StringComparison.OrdinalIgnoreCase));

                        Config.Save();

                        aresp = string.Format(Parser.Language.Formats["commandRemoveAliasSuccess"], existing.Value, info.Name);
                    }
                    else
                    {
                        aresp = string.Format(Parser.Language.Formats["commandRemoveAliasNotFound"], Command[2], info.Name);// $"Alias \"{alias.Value}\" not found on {info.Name}.  ";
                        var aliases = GuildConfig.PokemonAliases.Where(x => x.Key == info.Id);
                        aresp += aliases.Count() > 0 ?
                            string.Format(Parser.Language.Formats["commandRemoveAliasNotFoundAvaliable"], string.Join(", ", aliases.Select(x => x.Value)))
                            : string.Format(Parser.Language.Formats["commandRemoveAliasNotFoundNone"], info.Name);
                    }

                    //if (pokemonId > 0)
                    //{
                    //    var aliases = new List<string>();

                    //    aliases.RemoveAll(x => x.Equals(Command[2], StringComparison.OrdinalIgnoreCase));
                    //    info.ServerAliases.Remove(alias);
                    //    Config.Save();
                    //}
                    //else
                    //{
                    //    aresp = string.Format(Parser.Language.Formats["commandRemoveAliasNotFound"], alias.Value, info.Name);// $"Alias \"{alias.Value}\" not found on {info.Name}.  ";
                    //    var aliases = info.ServerAliases.Where(x => x.Key == Guild.Id);
                    //    aresp += aliases.Count() > 0 ?
                    //        string.Format(Parser.Language.Formats["commandRemoveAliasNotFoundAvaliable"], string.Join(", ", aliases.Select(x => x.Value)))
                    //        : string.Format(Parser.Language.Formats["commandRemoveAliasNotFoundNone"], info.Name);
                    //}
                    //break;
                //}
            //}
            await Handler.MakeCommandMessage(Message.Channel, aresp);
        }

        [RaidBotCommand("merge")]
        private async Task Merge()
        {
            var post1 = GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (post1 == null)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));//$"Post with Unique Id \"{Command[1]}\" not found.");
                return;
            }

            var post2 = GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[2]);
            if (post2 == null)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[2]));//$"Post with Unique Id \"{Command[2]}\" not found.");
                return;
            }
            if (post1.UserId != Message.Author.Id && post2.UserId != Message.Author.Id && !IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
            }

            if (post1.HasEndDate)
            {
                if (post2.HasEndDate)
                {
                    post1.EndDate = new DateTime(Math.Max(post1.EndDate.Ticks, post2.EndDate.Ticks));
                }
            }
            else if (post2.HasEndDate)
                post1.EndDate = post2.EndDate;

            post1.Responses.AddRange(post2.Responses);

            Handler.DeletePost(post2);
            await Handler.MakePost(post1, Parser);
        }
        
        [RaidBotCommand("delete")]
        private async Task Delete()
        {
            var post = GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (post == null)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));//"Post with Unique Id \"{Command[1]}\" not found.");
                return;
            }
            if (post.UserId != Message.Author.Id && !IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
            }
            Handler.DeletePost(post);
        }

        [RaidBotCommand("pinall")]
        private async Task PinAll()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }
            foreach (var pinallChannel in Guild.Channels)
            {
                if (!GuildConfig.PinChannels.Contains(pinallChannel.Id))
                {
                    GuildConfig.PinChannels.Add(pinallChannel.Id);
                }
            }
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandPinAllSuccess"]);
        }

        [RaidBotCommand("pin")]
        private async Task Pin()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }
            var pinchannel = Guild.Channels.FirstOrDefault(x => x.Name.ToLowerInvariant() == Command[1].ToLowerInvariant());
            if (pinchannel == null)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandGuildNoChannel"], Guild.Name, Command[1]));// $"{Guild.Name} does not contain a channel named \"{Command[1]}\"");
                return;
            }
            if (!GuildConfig.PinChannels.Contains(pinchannel.Id))
            {
                GuildConfig.PinChannels.Add(pinchannel.Id);
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Strings["commandPinSuccess"], pinchannel.Name));//$"{pinchannel.Name} added to Pin Channels.");
            }
            else
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Strings["commandPinAlreadyDone"], pinchannel.Name));// $"{pinchannel.Name} is already in Pin Channels.");
            }
        }

        [RaidBotCommand("unpinall")]
        private async Task UnPinAll()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }
            GuildConfig.PinChannels.Clear();
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandUnPinAllSuccess"]);
        }

        [RaidBotCommand("unpin")]
        private async Task UnPin()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }
            var unpinchannel = Guild.Channels.FirstOrDefault(x => x.Name.ToLowerInvariant() == Command[1].ToLowerInvariant());
            if (unpinchannel == null)
            {

                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandGuildNoChannel"], Guild.Name, Command[1]));//$"{Guild.Name} does not contain a channel named \"{Command[1]}\"");
                return;
            }
            if (!GuildConfig.PinChannels.Contains(unpinchannel.Id))
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelNotPinned"], unpinchannel.Name));//$"{unpinchannel.Name} has not been added to Pin Channels.");
                return;
            }
            GuildConfig.PinChannels.Remove(unpinchannel.Id);
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelNotPinned"], unpinchannel.Name));//$"{unpinchannel.Name} removed from Pin Channels.");
        }

        [RaidBotCommand("pinlist")]
        private async Task PinList()
        {
            var pinstring = "";
            foreach (var channel in Guild.Channels)
            {
                if (GuildConfig.PinChannels.Contains(channel.Id))
                    pinstring += $"\n{channel.Name}";
            }

            if (string.IsNullOrEmpty(pinstring)) pinstring = "No channels in Pin List.";
            else pinstring = "Pinned Channels:" + pinstring;

            await Handler.MakeCommandMessage(Message.Channel, pinstring);
        }

        [RaidBotCommand("city")]
        private async Task City()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }

            var cityString = string.Join(" ", Command.Skip(1));
            Config.GetGuildConfig(Guild.Id).City = cityString;
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandCitySuccess"], Guild.Name, cityString));//$"{unpinchannel.Name} removed from Pin Channels.");
        }

        [RaidBotCommand("channelcity")]
        private async Task ChannelCity()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return;
            }
            var channel = Guild.Channels.FirstOrDefault(x => x.Name.ToLowerInvariant() == Command[1].ToLowerInvariant());
            if (Command.Length > 2 && !string.IsNullOrEmpty(Command[1]))
            {
                if (channel != null)
                {
                    var city = string.Join(" ", Command.Skip(2));
                    Config.GetGuildConfig(Guild.Id).ChannelCities[channel.Id] = city;
                    //Config.ServerChannels[Guild.Id] = channel.Id;
                    Config.Save();
                    await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandCitySuccess"], "channel " + channel.Name, city));//// $"Output channel for {Guild.Name} changed to {channel.Name}");
                    
                }
                else
                {
                    await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandGuildNoChannel"], Guild.Name, Command[1]));//$"{Guild.Name} does not contain a channel named \"{Command[1]}\"");
                }
            }
            else
            {
                Config.GetGuildConfig(Guild.Id).ChannelCities.Remove(channel.Id);
                //Config.ServerChannels.Remove(Guild.Id);
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelCityCleared"], Guild.Name, Config.GetGuildConfig(Guild.Id).City));//$"Output channel override for {Guild.Name} removed, default value \"{Config.OutputChannel}\" will be used.");
            }
        }

        [RaidBotCommand("help")]
        private async Task Help()
        {
            var helpMessage = Parser.GetHelpString(Config, IsAdmin);
            foreach(var message in helpMessage)
            {
                await Message.Channel.SendMessageAsync(message);
            }
        }
    }
}
