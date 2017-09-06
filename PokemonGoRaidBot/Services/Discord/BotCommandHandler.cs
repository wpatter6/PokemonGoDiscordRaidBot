using Discord.WebSocket;
using PokemonGoRaidBot.Configuration;
using PokemonGoRaidBot.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using PokemonGoRaidBot.Services.Parsing;

namespace PokemonGoRaidBot.Services.Discord
{
    public class BotCommandHandler
    {
        private MessageHandler Handler;
        private SocketUserMessage Message;
        private SocketGuildUser User;
        private SocketGuild Guild;
        private MessageParser Parser;
        private bool IsAdmin;
        private BotConfiguration Config;
        private GuildConfiguration GuildConfig;

        private List<string> Command;

        public BotCommandHandler(MessageHandler handler, SocketUserMessage message, MessageParser parser)
        {
            Handler = handler;
            Message = message;
            Parser = parser;

            Config = Handler.Config;
            Command = new List<string>(Message.Content.ToLower().Replace("  ", " ").Substring(Config.Prefix.Length).Split(' '));
            Command.Remove("");
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

                if (method != default(MethodInfo))
                    await (Task)method.Invoke(this, new object[] { });
                else
                    await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandUnknown"], Command[0], Config.Prefix));//$"Unknown Command \"{Command[0]}\".  Type {Config.Prefix}help to see valid Commands for this bot.");

            }
            catch (Exception e)
            {
                Handler.DoError(e, "Executor");
            }
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
        private PokemonRaidPost GetPost(string name, bool idOnly = false)
        {
            var post = GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == name);

            if (idOnly || post != null)
            {
                post.IsExisting = true;
                return post;
            }

            post = Handler.AddPost(Parser.ParsePost(Message, Config), Parser, Message, false);
            return post;
        }
        private async Task<SocketGuildChannel> GetChannelFromName(string name)
        {
            var channel = Guild.Channels.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());

            if (channel == null)
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandGuildNoChannel"], Guild.Name, name));

            return channel;
        }

        [RaidBotCommand("r")]
        [RaidBotCommand("raid")]
        private async Task Raid()
        {
            if (Command.Count() < 4)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandInvalidNumberOfParameters"]);
                return;
            }

            var post = Parser.ParsePost(Message, Config);

            if (post.PokemonId == default(int))
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandRaidPokemonInvalid"], Command[1]));
                return;
            }

            if (post.HasEndDate == false)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandRaidTimespanInvalid"], Command[2]));
                return;
            }

            var d = Message.Channel.EnterTypingState();
            try
            {

                post.Location = Parser.ToTitleCase(string.Join(" ", Command.Skip(3)));
                post.FullLocation = Parser.GetFullLocation(post.Location, GuildConfig, Message.Channel.Id);

                if (GuildConfig.Places.ContainsKey(post.Location))
                    post.LatLong = GuildConfig.Places[post.Location];
                else
                    post.LatLong = await Parser.GetLocationLatLong(post.FullLocation, (SocketGuildChannel)Message.Channel, Config);

                if (string.IsNullOrWhiteSpace(post.Location))
                {
                    await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandRaidLocationInvalid"]);
                    return;
                }
                post.IsValid = true;

                var outputchannel = !GuildConfig.OutputChannelId.HasValue || GuildConfig.OutputChannelId == 0 ? null : (ISocketMessageChannel)Guild.GetChannel(GuildConfig.OutputChannelId.Value);

                await Handler.DoPost(post, Message, Parser, outputchannel, true);
            }
            catch (Exception e)
            {
                Handler.DoError(e, "Executor");
            }
            finally
            {
                d.Dispose();
            }
        }

        [RaidBotCommand("j")]
        [RaidBotCommand("join")]
        private async Task Join()
        {
            string num = null;
            string poke = null;
            string time = null;
            bool isMore = false, isLess = false;

            int number = 0;

            if (Command.Count() == 2)
            {//just have a number or pokemon;
                if (!int.TryParse(Command[1], out number))
                {
                    poke = Command[1];
                    number = 1;
                    num = "1";
                }
                else
                {
                    poke = "";
                    num = Command[1];
                }
            }
            else if (Command.Count() >= 3)
            {
                if (!int.TryParse(Command[2], out number))
                {
                    if (!int.TryParse(Command[1], out number))
                    {
                        await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandInvalidNumber"], Command[2]));
                        return;
                    }
                    else
                    {
                        num = Command[1];
                        poke = Command[2];
                    }
                }
                else
                {
                    poke = Command[1];
                    num = Command[2];
                }

                time = string.Join(" ", Command.Skip(3));
            }
            else
            {
                num = "1";
                number = 1;
                poke = "";
            }

            if (num.StartsWith("+"))
            {
                isMore = true;
            }
            else if (num.StartsWith("-"))
            {
                isLess = true;
                number = Math.Abs(number);
            }

            if (number == 0 && Command.Count() > 2)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandInvalidNumber"], Command[2]));
                return;
            }

            var post = GetPost(poke);
            if (!post.IsExisting)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], poke));// $"Raid post with Id \"{Command[1]}\" does not exist.");
                return;
            }

            var joinedUser = post.JoinedUsers.FirstOrDefault(x => x.Id == Message.Author.Id);
            if (joinedUser == null)
            {
                joinedUser = new PokemonRaidJoinedUser(Message.Author.Id, Guild.Id, post.UniqueId, Message.Author.Username, number);
                post.JoinedUsers.Add(joinedUser);
            }
            else if (isMore)
                joinedUser.PeopleCount += number;
            else if (isLess)
                joinedUser.PeopleCount -= number;
            else
                joinedUser.PeopleCount = number;

            if (joinedUser.PeopleCount <= 0) post.JoinedUsers.Remove(joinedUser);

            TimeSpan? ts1, ts2;
            Parser.ParseTimespanFull(ref time, out ts1, out ts2);

            joinedUser.ArriveTime = (DateTime.Now + (ts1 ?? ts2)) ?? joinedUser.ArriveTime;

            await Handler.MakePost(post, Parser);
            Config.Save();
        }

        [RaidBotCommand("uj")]
        [RaidBotCommand("un")]
        [RaidBotCommand("unjoin")]
        private async Task UnJoin()
        {
            if (Command.Count() == 1)
            {
                var joinedPosts = GuildConfig.Posts.Where(x => x.JoinedUsers.Where(xx => xx.Id == Message.Author.Id).Count() > 0);
                var tasks = new List<Task>();
                foreach (var post in joinedPosts)
                {
                    post.JoinedUsers.Remove(post.JoinedUsers.First(x => x.Id == Message.Author.Id));
                    tasks.Add(Handler.MakePost(post, Parser));
                }
                Task.WaitAll(tasks.ToArray());
            }
            else
            {
                var post = GetPost(Command[1]);
                if (!post.IsExisting)
                {
                    await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));// $"Raid post with Id \"{Command[1]}\" does not exist.");
                    return;
                }
                post.JoinedUsers.Remove(post.JoinedUsers.First(x => x.Id == Message.Author.Id));
                await Handler.MakePost(post, Parser);
            }
            Config.Save();
        }

        [RaidBotCommand("i")]
        [RaidBotCommand("info")]
        private async Task Info()
        {
            if (Command.Count() > 1 && Command[1].Length > 2)
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

                var list = Parser.Language.Pokemon.Where(x => x.CatchRate > 0);

                if (Command.Count() > 1 && int.TryParse(Command[1], out tierCommand))
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

        //[RaidBotCommand("test")]
        //private async Task Test()
        //{
        //    var user = Guild.GetUser(235123612351201280);

        //    await Handler.DirectMessageUser(user, "TEST!!");
        //}

        [RaidBotCommand("m")]
        [RaidBotCommand("merge")]
        private async Task Merge()
        {
            var post1 = GetPost(Command[1], true);// GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (!post1.IsExisting)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));// $"Raid post with Id \"{Command[1]}\" does not exist.");
                return;
            }

            var post2 = GetPost(Command[2], true);//GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[2]);
            if (!post2.IsExisting)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));// $"Raid post with Id \"{Command[1]}\" does not exist.");
                return;
            }
            

            Handler.MergePosts(post1, post2);

            if(!await Handler.DeletePost(post2, Message.Author.Id, !IsAdmin))
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
                return;
            }

            await Handler.MakePost(post1, Parser);
            Config.Save();
        }

        [RaidBotCommand("d")]
        [RaidBotCommand("delete")]
        private async Task Delete()
        {
            if (!string.IsNullOrEmpty(Command[1]) && "all".Equals(Command[1]))
            {
                foreach (var allpost in GuildConfig.Posts)
                {
                    await Handler.DeletePost(allpost, Message.Author.Id, false, false);
                }
                await Handler.PurgePosts();
                return;
            }
            var post = GetPost(Command[1]);//GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (!post.IsExisting)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));//"Post with Unique Id \"{Command[1]}\" not found.");
                return;
            }
            var b = await Handler.DeletePost(post, Message.Author.Id, IsAdmin);

            if (!b)//post creators or admins can delete
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
                return;
            }

        }

        [RaidBotCommand("loc")]
        [RaidBotCommand("location")]
        private async Task Location()
        {
            var post = GetPost(Command[1]);//GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (!post.IsExisting)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));// $"Raid post with Id \"{Command[1]}\" does not exist.");
                return;
            }
            if (post.UserId != Message.Author.Id && !IsAdmin)//post creators or admins can delete
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
                return;
            }
            post.Location = Parser.ToTitleCase(string.Join(" ", Command.Skip(2)));

            if (GuildConfig.Places.ContainsKey(post.Location))
                post.LatLong = GuildConfig.Places[post.Location];
            else
                post.LatLong = await Parser.GetLocationLatLong(post.Location, (SocketGuildChannel)Message.Channel, Config);

            await Handler.MakePost(post, Parser);
            Config.Save();
        }

        [RaidBotCommand("h")]
        [RaidBotCommand("help")]
        private async Task Help()
        {
            //var helpMessage = Parser.GetFullHelpString(Config, IsAdmin);
            var embed = Parser.GetHelpEmbed(Config, IsAdmin);
            await Message.Channel.SendMessageAsync(string.Format(Parser.Language.Strings["helpTop"], Config.OutputChannel), false, embed);
            //foreach (var message in helpMessage)
            //{
            //    await Message.Channel.SendMessageAsync(message);
            //}
        }

        [RaidBotCommand("language")]
        private async Task Language()
        {
            if (!await CheckAdminAccess()) return;

            GuildConfig.Language = Command[1];
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandLanguageSuccess"], Guild.Name, Command[1]));
        }

        [RaidBotCommand("timezone")]
        private async Task Timezone()
        {
            if (!await CheckAdminAccess()) return;

            int timezoneOut = int.MinValue;
            var isvalid = int.TryParse(Command[1], out timezoneOut);

            if (!isvalid || timezoneOut > 12 || timezoneOut < -11)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandInvalidNumber"], Command[1]));
                return;
            }
            GuildConfig.Timezone = timezoneOut;
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandTimezoneSuccess"], Guild.Name, timezoneOut > -1 ? "+" + timezoneOut.ToString() : timezoneOut.ToString()));
        }

        [RaidBotCommand("channel")]
        private async Task Channel()
        {
            if (!await CheckAdminAccess()) return;

            if (Command.Count() > 1 && !string.IsNullOrEmpty(Command[1]))
            {
                var channel = await GetChannelFromName(Command[1]);
                if (channel == null) return;

                GuildConfig.OutputChannelId = channel.Id;
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelSuccess"], Guild.Name, channel.Name));//// $"Output channel for {Guild.Name} changed to {channel.Name}");

            }
            else
            {
                GuildConfig.OutputChannelId = null;
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelCleared"], Guild.Name, Config.OutputChannel));//$"Output channel override for {Guild.Name} removed, default value \"{Config.OutputChannel}\" will be used.");
            }
        }

        [RaidBotCommand("nochannel")]
        private async Task NoChannel()
        {
            if (!await CheckAdminAccess()) return;

            GuildConfig.OutputChannelId = 0;
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
                GuildConfig.PokemonAliases[foundInfo.Id].Add(Command[2].ToLower());

            Config.Save();

            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandAliasSuccess"], Command[2].ToLower(), foundInfo.Name));
        }

        [RaidBotCommand("removealias")]
        private async Task RemoveAlias()
        {
            if (!await CheckAdminAccess()) return;
            var aresp = "";

            var info = Parser.ParsePokemon(Command[1], Config, Guild.Id);

            var existing = GuildConfig.PokemonAliases.FirstOrDefault(x => x.Value.Contains(Command[2].ToLower()));
            if (!existing.Equals(default(KeyValuePair<int, List<string>>)))
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
            GuildConfig.City = cityString;
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandCitySuccess"], Guild.Name, cityString));//$"{unpinchannel.Name} removed from Pin Channels.");
        }

        [RaidBotCommand("channelcity")]
        private async Task ChannelCity()
        {
            if (!await CheckAdminAccess()) return;
            var channel = await GetChannelFromName(Command[1]);
            if (channel == null) return;

            if (Command.Count() > 2 && !string.IsNullOrEmpty(Command[1]))
            {
                var city = string.Join(" ", Command.Skip(2));
                GuildConfig.ChannelCities[channel.Id] = city;
                //Config.ServerChannels[Guild.Id] = channel.Id;
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandCitySuccess"], "channel " + channel.Name, city));//// $"Output channel for {Guild.Name} changed to {channel.Name}");
            }
            else
            {
                GuildConfig.ChannelCities.Remove(channel.Id);
                //Config.ServerChannels.Remove(Guild.Id);
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelCityCleared"], Guild.Name, GuildConfig.City));//$"Output channel override for {Guild.Name} removed, default value \"{Config.OutputChannel}\" will be used.");
            }
        }

        [RaidBotCommand("cities")]
        private async Task Cities()
        {
            if (!await CheckAdminAccess()) return;

            var str = string.Format("Default: {0}", GuildConfig.City);

            foreach (var city in GuildConfig.ChannelCities)
            {
                var channel = Guild.GetChannel(city.Key);
                str += string.Format("\n{0}: {1}", channel.Name, city.Value);
            }

            await Handler.MakeCommandMessage(Message.Channel, str);
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

        [RaidBotCommand("place")]
        private async Task Place()
        {
            if (!await CheckAdminAccess()) return;

            if(Command.Count() < 2)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandInvalidNumberOfParameters"]);
                return;
            }

            var cmdstr = string.Join(" ", Command.Skip(1));

            var latlng = Parser.ParseLatLong(ref cmdstr, "");

            var location = cmdstr.Trim();

            if (!string.IsNullOrEmpty(location))
            {
                if(latlng == null || !latlng.HasValue)
                    latlng = await Parser.GetLocationLatLong(location, (SocketGuildChannel)Message.Channel, Config);

                GuildConfig.Places[location] = latlng;
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPlaceSuccess"], location,
                    latlng != null && latlng.HasValue ? " at " + latlng.ToString() : ""));
            }
            else
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandInvalidNumberOfParameters"]);
                return;
            }
        }


        [RaidBotCommand("deleteplace")]
        private async Task DeletePlace()
        {
            if (!await CheckAdminAccess()) return;

            if (Command.Count() < 2)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandInvalidNumberOfParameters"]);
                return;
            }

            var cmdstr = string.Join(" ", Command.Skip(1));

            if (!GuildConfig.Places.ContainsKey(cmdstr))
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandDeletePlaceNotFound"], cmdstr));
            }
            else
            {
                GuildConfig.Places.Remove(cmdstr);
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandDeletePlaceSuccess"], cmdstr));
            }

        }

        [RaidBotCommand("places")]
        private async Task Places()
        {
            if (!await CheckAdminAccess()) return;
            var placestring = "";
            foreach (var place in GuildConfig.Places)
            {
                placestring += string.Format("\n{0}{1}", place.Key, place.Value != null && place.Value.HasValue ? $" ({place.Value})" : "");
            }

            await Handler.MakeCommandMessage(Message.Channel, placestring);
        }

        [RaidBotCommand("raidhelp")]
        private async Task RaidHelp()
        {
            var embed = Parser.GetHelpEmbed(Config, false);
            await Message.Channel.SendMessageAsync(string.Format(Parser.Language.Strings["helpTop"], Config.OutputChannel), false, embed);
        }

        [RaidBotCommand("version")]
        private async Task Version()
        {
            var version = Config.Version;// Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

            await Handler.MakeCommandMessage(Message.Channel, $"PokemonGoRaidBot {version}");
        }
    }
}
