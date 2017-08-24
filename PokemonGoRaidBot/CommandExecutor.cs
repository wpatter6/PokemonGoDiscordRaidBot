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
using Discord;

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
                MethodInfo[] methodInfos = GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);

                var method = methodInfos.FirstOrDefault(x => x.GetCustomAttributes<RaidBotCommandAttribute>().Where(xx => xx != null && xx.Command == Command[0]).Count() > 0);

                if(method != default(MethodInfo))
                    await (Task)method.Invoke(this, new object[] { });
                else
                    await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandUnknown"], Command[0], Config.Prefix));//$"Unknown Command \"{Command[0]}\".  Type {Config.Prefix}help to see valid Commands for this bot.");
            }
            catch (Exception e)
            {
                Handler.DoError(e, "executor");
            }
        }

        [RaidBotCommand("j")]
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

            bool isMore = false, isLess = false;

            if(Command[2].StartsWith("+"))
            {
                isMore = true;
                Command[2] = Command[2].Substring(1);
            }
            else if (Command[2].StartsWith("-"))
            {
                isLess = true;
                Command[2] = Command[2].Substring(1);
            }

            if(!int.TryParse(Command[2], out num))
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandInvalidNumber"], Command[2]));//$"Invalid number of raid joiners \"{Command[2]}\".");
                return;
            }

            var joinedUser = post.JoinedUsers.FirstOrDefault(x => x.Id == Message.Author.Id);
            if (joinedUser == null)
                post.JoinedUsers.Add(new PokemonRaidJoinedUser(Message.Author.Id, Message.Author.Username, num));
            else
            {
                if (isMore)
                    joinedUser.Count += num;
                else if (isLess)
                    joinedUser.Count -= num;
                else
                    joinedUser.Count = num;
            }
                

            await Handler.MakePost(post, Parser);
        }
        
        [RaidBotCommand("uj")]
        [RaidBotCommand("un")]
        [RaidBotCommand("unjoin")]
        private async Task UnJoin()
        {

            var post = GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (post == null)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));
                return;
            }

            post.JoinedUsers.RemoveAll(x => x.Id == Message.Author.Id);
            await Handler.MakePost(post, Parser);
        }

        [RaidBotCommand("i")]
        [RaidBotCommand("info")]
        private async Task Info()
        {
            if (Command.Length > 1 && Command[1].Length > 2)
            {
                var info = Parser.ParsePokemon(Command[1], Config, Guild.Id);

                if (info != null) {
                    var message = "```css" + Parser.MakeInfoLine(info, Config, Guild.Id) + "```" + string.Format(Parser.Language.Formats["pokemonInfoLink"], info.Id);
                    await Message.Channel.SendMessageAsync(message);
                } else
                    await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPokemonNotFound"], Command[1]));//$"'{Command[1]}' did not match any raid boss names or aliases.";
            }
            else
            {
                var strings = new List<string>();
                var strInd = 0;

                var tierCommand = 0;

                var list = Config.PokemonInfoList.Where(x => x.CatchRate > 0);

                if (Command.Length > 1 && int.TryParse(Command[1], out tierCommand))
                {
                    list = list.Where(x => x.Tier == tierCommand);
                }

                var orderedList = list.OrderByDescending(x => x.Id).OrderByDescending(x => x.BossCP);
                
                var maxBossLength = orderedList.Select(x => x.BossNameFormatted.Length).Max();
                strings.Add("");
                foreach (var info in orderedList)
                {
                    var lineStr = Parser.MakeInfoLine(info, Config, Guild.Id, maxBossLength);

                    if (strings[strInd].Length + lineStr.Length + 3 < 2000)
                        strings[strInd] += lineStr;
                    else
                    {
                        strings.Add(lineStr);
                        strInd++;
                    }

                }

                foreach (var str in strings)
                    await Handler.MakeCommandMessage(Message.Channel, "css" + str);// Message.Channel.SendMessageAsync(str);
            }
        }

        [RaidBotCommand("test")]
        private async Task Test()
        {
            var list = Config.PokemonInfoList.Where(x => x.CatchRate > 0);
            var orderedList = list.OrderByDescending(x => x.BossCP);


            var builder = new EmbedBuilder();
            builder.WithDescription("abcd 123 alkdsfja;lsdjf;lkajsd;lfj ;lkjsadlkj sdlkfj lkjfds ljfds lk");
            builder.WithThumbnailUrl(string.Format(Parser.Language.Formats["imageUrlLargePokemon"], 3));
            builder.WithUrl("https://pokemongo.gamepress.gg/pokemon/3#raid-boss-counters");
            await Message.Channel.SendMessageAsync("", false, builder);
        }

        [RaidBotCommand("m")]
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
            if (post2.UserId != Message.Author.Id && !IsAdmin)//post 2 creator or admins can delete -- #2 merges into #1.  Don't want #1 creator to be able to do this or they could (maliciously?) screw up someone else's raid
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
            }

            Handler.MergePosts(post1, post2);
            
            Handler.DeletePost(post2);

            await Handler.MakePost(post1, Parser);
        }

        [RaidBotCommand("d")]
        [RaidBotCommand("delete")]
        private async Task Delete()
        {
            var post = GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (post == null)
            {
                if(!string.IsNullOrEmpty(Command[1]) && "all".Equals(Command[1]))
                {
                    foreach(var allpost in GuildConfig.Posts.Where(x => x.UserId == Message.Author.Id || IsAdmin))
                    {
                        Handler.DeletePost(allpost);
                    }
                    return;
                }
                else
                { 
                    await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));//"Post with Unique Id \"{Command[1]}\" not found.");
                    return;
                }
            }
            if (post.UserId != Message.Author.Id && !IsAdmin)//post creators or admins can delete
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
                return;
            }
            Handler.DeletePost(post);
        }

        [RaidBotCommand("loc")]
        [RaidBotCommand("location")]
        private async Task Location()
        {
            var post = GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (post == null)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));//"Post with Unique Id \"{Command[1]}\" not found.");
                return;
            }
            if (post.UserId != Message.Author.Id && !IsAdmin)//post creators or admins can delete
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
                return;
            }
            post.Location = Parser.ToTitleCase(string.Join(" ", Command.Skip(2)));
            post.LatLong = await Parser.GetLocationLatLong(post.Location, (SocketGuildChannel)Message.Channel, Config);
            await Handler.MakePost(post, Parser);
        }

        [RaidBotCommand("language")]
        private async Task Language()
        {
            if (!await CheckAdminAccess()) return;

            Config.GetGuildConfig(Guild.Id).Language = Command[1];
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandLanguageSuccess"], Guild.Name, Command[1]));
        }

        [RaidBotCommand("timezone")]
        private async Task Timezone()
        {
            if (!await CheckAdminAccess()) return;

            int timezoneOut = int.MinValue;
            var isvalid = int.TryParse(Command[1], out timezoneOut);

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
            if (!await CheckAdminAccess()) return;

            if (Command.Length > 1 && !string.IsNullOrEmpty(Command[1]))
            {
                var channel = await GetChannelFromName(Command[1]);
                if (channel == null) return;

                Config.GetGuildConfig(Guild.Id).OutputChannelId = channel.Id;
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelSuccess"], Guild.Name, channel.Name));//// $"Output channel for {Guild.Name} changed to {channel.Name}");

            }
            else
            {
                Config.GetGuildConfig(Guild.Id).OutputChannelId = null;
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelCleared"], Guild.Name, Config.OutputChannel));//$"Output channel override for {Guild.Name} removed, default value \"{Config.OutputChannel}\" will be used.");
            }
        }

        [RaidBotCommand("nochannel")]
        private async Task NoChannel()
        {
            if (!await CheckAdminAccess()) return;

            Config.GetGuildConfig(Guild.Id).OutputChannelId = 0;
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoChannelSuccess"]);
        }

        [RaidBotCommand("alias")]
        private async Task Alias()
        {
            if (!await CheckAdminAccess()) return;

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
            if (!await CheckAdminAccess()) return;
            var aresp = "";

            var info = Parser.ParsePokemon(Command[1], Config, Guild.Id);
            
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
                    
            await Handler.MakeCommandMessage(Message.Channel, aresp);
        }
        
        [RaidBotCommand("pinall")]
        private async Task PinAll()
        {
            if (!await CheckAdminAccess()) return;
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
            if (!await CheckAdminAccess()) return;
            var channel = await GetChannelFromName(Command[1]);
            if (channel == null) return;

            if (!GuildConfig.PinChannels.Contains(channel.Id))
            {
                GuildConfig.PinChannels.Add(channel.Id);
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Strings["commandPinSuccess"], channel.Name));//$"{pinchannel.Name} added to Pin Channels.");
            }
            else
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Strings["commandPinAlreadyDone"], channel.Name));// $"{pinchannel.Name} is already in Pin Channels.");
            }
        }

        [RaidBotCommand("unpinall")]
        private async Task UnPinAll()
        {
            if (!await CheckAdminAccess()) return;
            GuildConfig.PinChannels.Clear();
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandUnPinAllSuccess"]);
        }

        [RaidBotCommand("unpin")]
        private async Task UnPin()
        {
            if (!await CheckAdminAccess()) return;
            var channel = await GetChannelFromName(Command[1]);
            if (channel == null) return;

            if (!GuildConfig.PinChannels.Contains(channel.Id))
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandUnPinAlreadyDone"], channel.Name));//$"{unpinchannel.Name} has not been added to Pin Channels.");
                return;
            }
            GuildConfig.PinChannels.Remove(channel.Id);
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandUnPinSuccess"], channel.Name));//$"{unpinchannel.Name} removed from Pin Channels.");
        }

        [RaidBotCommand("pinlist")]
        private async Task PinList()
        {
            var pinstring = "";
            foreach (var channel in Guild.Channels)
            {
                if (GuildConfig.PinChannels.Contains(channel.Id))
                    pinstring += "\n" + channel.Name;
            }

            if (string.IsNullOrEmpty(pinstring)) pinstring = Parser.Language.Strings["commandPinListNone"];//"No channels in Pin List."
            else pinstring = string.Format(Parser.Language.Formats["commandPinListHeader"], pinstring);// "Pinned Channels:" + pinstring;

            await Handler.MakeCommandMessage(Message.Channel, pinstring);
        }

        [RaidBotCommand("city")]
        private async Task City()
        {
            if (!await CheckAdminAccess()) return;

            var cityString = string.Join(" ", Command.Skip(1));
            Config.GetGuildConfig(Guild.Id).City = cityString;
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandCitySuccess"], Guild.Name, cityString));//$"{unpinchannel.Name} removed from Pin Channels.");
        }

        [RaidBotCommand("channelcity")]
        private async Task ChannelCity()
        {
            if (!await CheckAdminAccess()) return;
            var channel = await GetChannelFromName(Command[1]);
            if (channel == null) return;

            if (Command.Length > 2 && !string.IsNullOrEmpty(Command[1]))
            {
                var city = string.Join(" ", Command.Skip(2));
                Config.GetGuildConfig(Guild.Id).ChannelCities[channel.Id] = city;
                //Config.ServerChannels[Guild.Id] = channel.Id;
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandCitySuccess"], "channel " + channel.Name, city));//// $"Output channel for {Guild.Name} changed to {channel.Name}");
            }
            else
            {
                Config.GetGuildConfig(Guild.Id).ChannelCities.Remove(channel.Id);
                //Config.ServerChannels.Remove(Guild.Id);
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelCityCleared"], Guild.Name, Config.GetGuildConfig(Guild.Id).City));//$"Output channel override for {Guild.Name} removed, default value \"{Config.OutputChannel}\" will be used.");
            }
        }

        [RaidBotCommand("mute")]
        private async Task Mute()
        {
            if (!await CheckAdminAccess()) return;
            var channel = await GetChannelFromName(Command[1]);
            if (channel == null) return;

            if (!GuildConfig.MuteChannels.Contains(channel.Id))
            { 
                GuildConfig.MuteChannels.Add(channel.Id);
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandMuteSuccess"], channel.Name));//success
            }
            else
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandMuteAlreadyDone"], channel.Name));//already muted
        }

        [RaidBotCommand("unmute")]
        private async Task UnMute()
        {
            if (!await CheckAdminAccess()) return;
            var channel = await GetChannelFromName(Command[1]);
            if (channel == null) return;

            if (GuildConfig.MuteChannels.Contains(channel.Id))
            { 
                GuildConfig.MuteChannels.Remove(channel.Id);
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandUnMuteSuccess"], channel.Name));//success
            }
            else
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandUnMuteAlreadyDone"], channel.Name));//not muted

        }

        [RaidBotCommand("muteall")]
        private async Task MuteAll()
        {
            if (!await CheckAdminAccess()) return;

            foreach (var channel in Guild.Channels)
            {
                if (!GuildConfig.MuteChannels.Contains(channel.Id))
                {
                    GuildConfig.MuteChannels.Add(channel.Id);
                }
            }
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandMuteAllSuccess"]);//all muted

        }

        [RaidBotCommand("unmuteall")]
        private async Task UnMuteAll()
        {
            if (!await CheckAdminAccess()) return;
            GuildConfig.MuteChannels.Clear();
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandUnMuteAllSuccess"]);//all unmuted

        }

        [RaidBotCommand("mutelist")]
        private async Task MuteList()
        {
            if (!await CheckAdminAccess()) return;
            var mutestring = "";
            foreach (var channel in Guild.Channels)
            {
                if (GuildConfig.MuteChannels.Contains(channel.Id))
                    mutestring += "\n" + channel.Name;
            }

            if (string.IsNullOrEmpty(mutestring)) mutestring = Parser.Language.Strings["commandMuteListNone"];
            else mutestring = string.Format(Parser.Language.Formats["commandMuteListHeader"], mutestring);// "     #[Muted Channels]:" + mutestring;

            await Handler.MakeCommandMessage(Message.Channel, mutestring);
        }

        [RaidBotCommand("raidhelp")]
        private async Task RaidHelp()
        {
            var helpMessage = Parser.GetRaidHelpString(Config);
            foreach (var message in helpMessage)
            {
                await Message.Channel.SendMessageAsync(message);
            }
        }

        [RaidBotCommand("h")]
        [RaidBotCommand("help")]
        private async Task Help()
        {
            var helpMessage = Parser.GetFullHelpString(Config, IsAdmin);
            foreach(var message in helpMessage)
            {
                await Message.Channel.SendMessageAsync(message);
            }
        }

        [RaidBotCommand("version")]
        private async Task Version()
        {
            var version = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

            await Handler.MakeCommandMessage(Message.Channel, $"PokemonGoRaidBot {version}");
        }

        private async Task<bool> CheckAdminAccess()
        {
            if (!IsAdmin)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoAccess"]);
                return false;
            }
            return true;
        }
        private async Task<SocketGuildChannel> GetChannelFromName(string name)
        {
            var channel = Guild.Channels.FirstOrDefault(x => x.Name.ToLowerInvariant() == name.ToLowerInvariant());

            if (channel == null)
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandGuildNoChannel"], Guild.Name, name));

            return channel;
        }
    }
}
