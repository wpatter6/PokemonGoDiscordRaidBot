using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using PokemonGoRaidBot.Services.Parsing;
using PokemonGoRaidBot.Objects.Interfaces;
using PokemonGoRaidBot.Configuration;
using PokemonGoRaidBot.Objects;
using PokemonGoRaidBot.Services.Discord;

namespace PokemonGoRaidBot.Services
{
    public class BotCommandHandler
    {
        private IChatMessageHandler Handler;
        private IChatMessage Message;
        private IChatMessageOutput Output;
        private IBotServerConfiguration GuildConfig;
        private IBotConfiguration Config;
        private MessageParser Parser;

        private List<string> Command;

        public BotCommandHandler(IChatMessageHandler handler, IChatMessage message, MessageParser parser)
        {
            Handler = handler;
            Message = message;
            Parser = parser;

            Config = Handler.Config;
            Command = new List<string>(Message.Content.ToLower().Replace("  ", " ").Substring(Config.Prefix.Length).Split(' '));
            Command.Remove("");
            switch (message.ChatType)
            {
                case ChatTypes.Discord:
                    Output = new DiscordMessageOutput();
                    break;
            }

            GuildConfig = Config.GetServerConfig(message.Server.Id, message.ChatType);
        }

        public async Task Execute()
        {
            try
            {
                MethodInfo[] methodInfos = GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);

                var method = methodInfos.FirstOrDefault(x => x.GetCustomAttributes<BotCommandAttribute>().Where(xx => xx != null && xx.Command == Command[0]).Count() > 0);

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
            if (!Message.User.IsAdmin)
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

            post = Handler.AddPost(Parser.ParsePost(Message), Parser, Message, false);
            return post;
        }
        private async Task<IChatChannel> GetChannelFromName(string name)
        {
            var channel = Message.Server.Channels.FirstOrDefault(x => (x.Name ?? "").ToLower() == (name ?? "").ToLower());

            if (channel == null)
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandGuildNoChannel"], Message.Server.Name, name));

            return channel;
        }

        //[BotCommand("test")]
        //private async Task Test()
        //{
        //    throw new Exception();
        //}

        [BotCommand("r")]
        [BotCommand("raid")]
        private async Task Raid()
        {
            if (Command.Count() < 4)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandInvalidNumberOfParameters"]);
                return;
            }

            var post = Parser.ParsePost(Message);

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

                if (GuildConfig.Places.ContainsKey(post.Location.ToLower()))
                {
                    post.LatLong = GuildConfig.Places[post.Location];
                }
                else
                    post.LatLong = await Parser.GetLocationLatLong(post.FullLocation, Message.Channel, Config);

                if (string.IsNullOrWhiteSpace(post.Location))
                {
                    await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandRaidLocationInvalid"]);
                    return;
                }
                post.IsValid = true;

                var outputchannel = !GuildConfig.OutputChannelId.HasValue || GuildConfig.OutputChannelId == 0 ? null : Message.Server.Channels.FirstOrDefault(x => x.Id == GuildConfig.OutputChannelId.Value);

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

        [BotCommand("j")]
        [BotCommand("join")]
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

            var joinedUser = post.JoinedUsers.FirstOrDefault(x => x.Id == Message.User.Id);
            if (joinedUser == null)
            {
                joinedUser = new PokemonRaidJoinedUser(Message.User.Id, Message.Server.Id, post.UniqueId, Message.User.Name, number);
                post.JoinedUsers.Add(joinedUser);
            }
            else if (isMore)
                joinedUser.PeopleCount += number;
            else if (isLess)
                joinedUser.PeopleCount -= number;
            else
                joinedUser.PeopleCount = number;

            if (joinedUser.PeopleCount <= 0) post.JoinedUsers.Remove(joinedUser);

            Parser.ParseTimespanFull(ref time, out TimeSpan? ts1, out TimeSpan? ts2, out DateTime? dt1, out DateTime? dt2);

            joinedUser.ArriveTime = dt2 ?? dt1 ?? (DateTime.Now + (ts1 ?? ts2)) ?? joinedUser.ArriveTime;

            await Handler.MakePost(post, Parser);
            Config.Save();
        }

        [BotCommand("uj")]
        [BotCommand("un")]
        [BotCommand("unjoin")]
        private async Task UnJoin()
        {
            if (Command.Count() == 1)
            {
                var joinedPosts = GuildConfig.Posts.Where(x => x.JoinedUsers.Where(xx => xx.Id == Message.User.Id).Count() > 0);
                var tasks = new List<Task>();
                foreach (var post in joinedPosts)
                {
                    post.JoinedUsers.Remove(post.JoinedUsers.First(x => x.Id == Message.User.Id));
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
                post.JoinedUsers.Remove(post.JoinedUsers.First(x => x.Id == Message.User.Id));
                await Handler.MakePost(post, Parser);
            }
            Config.Save();
        }

        [BotCommand("i")]
        [BotCommand("info")]
        private async Task Info()
        {
            if (Command.Count() > 1 && Command[1].Length > 2)
            {
                var info = Parser.ParsePokemon(Command[1]);

                if (info != null) {
                    var message = "```css" + Output.MakeInfoLine(info, Config, Message.Server.Id) + "```" + string.Format(Parser.Language.Formats["pokemonInfoLink"], info.Id);
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
                    var lineStr = Output.MakeInfoLine(info, Config, Message.Server.Id, maxBossLength);

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

        [BotCommand("stats")]
        private async Task Stats()
        {
            int count = 5, days = 7;
            string aggregate = "boss";

            var ct = Command.Count();
            if(ct > 1)
            {
                var paramStart = 1;
                if (!int.TryParse(Command[1], out count))
                {
                    aggregate = Command[1];
                    paramStart++;
                }
                if(ct >= paramStart)
                { 
                    if (!int.TryParse(Command[paramStart], out count))
                    { 
                        await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandInvalidNumber"], Command[paramStart]));
                        return;
                    }

                    if (ct >= paramStart + 1)
                    {
                        if (!int.TryParse(Command[paramStart + 1], out days))
                        {
                            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandInvalidNumber"], Command[paramStart + 1]));
                            return;
                        }
                    }
                }
            }
            
            days = Math.Abs(days);
            string statString = "";
            switch (aggregate)
            {//only boss for now, maybe other aggregates later.
                case "boss":
                    statString = GetBossStats(count, days);
                    break;
                default:
                    await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandInvalidNumber"], aggregate));
                    break;
            }
            await Handler.MakeCommandMessage(Message.Channel, statString);
        }

        private string GetBossStats(int count, int days)
        {
            var statString = "";
            var result = Handler.GetBossAggregates(count, x => x.PostedDate > DateTime.Now.AddDays(-1 * days) && x.ChannelPosts.Where(y => y.Channel.ServerId == Message.Server.Id).Count() > 0);
            var total = Handler.GetPostCount(days, Message.Server.Id);
            statString = string.Format("Top {0} Bosses for the last {2} days out of {3:#,##0} total:", count, Message.Server.Name, days, total);
            int i = 1;

            foreach (var grouping in result)
                statString += string.Format("\n#{0}: {1} with {2} posts.", i++, grouping.Key.Name, grouping.Count());
            return statString;
        }

        //[RaidBotCommand("test")]
        //private async Task Test()
        //{
        //    var user = Guild.GetUser(235123612351201280);

        //    await Handler.DirectMessageUser(user, "TEST!!");
        //}

        [BotCommand("m")]
        [BotCommand("merge")]
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

            if(!await Handler.DeletePost(post2, Message.User.Id, Message.User.IsAdmin))
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
                return;
            }

            await Handler.MakePost(post1, Parser);
            Config.Save();
        }

        [BotCommand("d")]
        [BotCommand("delete")]
        private async Task Delete()
        {
            if (!string.IsNullOrEmpty(Command[1]) && "all".Equals(Command[1]))
            {
                foreach (var allpost in GuildConfig.Posts)
                {
                    await Handler.DeletePost(allpost, Message.User.Id, false, false);
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
            var b = await Handler.DeletePost(post, Message.User.Id, Message.User.IsAdmin);

            if (!b)//post creators or admins can delete
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
                return;
            }

        }

        [BotCommand("loc")]
        [BotCommand("location")]
        private async Task Location()
        {
            var post = GetPost(Command[1]);//GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (!post.IsExisting)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));// $"Raid post with Id \"{Command[1]}\" does not exist.");
                return;
            }
            if (post.UserId != Message.User.Id && !Message.User.IsAdmin)//post creators or admins can delete
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
                return;
            }
            post.Location = Parser.ToTitleCase(string.Join(" ", Command.Skip(2)));

            if (GuildConfig.Places.ContainsKey(post.Location.ToLower()))
                post.LatLong = GuildConfig.Places[post.Location.ToLower()];
            else
                post.LatLong = await Parser.GetLocationLatLong(post.Location, Message.Channel, Config);

            await Handler.MakePost(post, Parser);
            Config.Save();
        }
        
        [BotCommand("s")]
        [BotCommand("start")]
        private async Task Start()
        {
            var post = GetPost(Command[1]);//GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (!post.IsExisting)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));// $"Raid post with Id \"{Command[1]}\" does not exist.");
                return;
            }
            
            string txt = string.Join(" ", Command.Skip(1));

            Parser.ParseTimespanFull(ref txt, out TimeSpan? ts1, out TimeSpan? ts2, out DateTime? dt1, out DateTime? dt2);

            var startTime = dt1 ?? dt2 ?? (DateTime.Now + (ts1 ?? ts2));

            if (startTime.HasValue)
            {
                if (post.JoinedUsers.Count == 0)
                    post.JoinedUsers.Add(new PokemonRaidJoinedUser(Message.User.Id, Message.Server.Id, post.UniqueId, Message.User.Name, 1));

                post.RaidStartTimes.Add(startTime.Value);
                await Handler.MakePost(post, Parser);
                Config.Save();
            }
            else
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandRaidTimespanInvalid"], string.Join(" ", Command.Skip(1))));
        }

        [BotCommand("e")]
        [BotCommand("end")]
        private async Task End()
        {
            var post = GetPost(Command[1]);//GuildConfig.Posts.FirstOrDefault(x => x.UniqueId == Command[1]);
            if (!post.IsExisting)
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPostNotFound"], Command[1]));// $"Raid post with Id \"{Command[1]}\" does not exist.");
                return;
            }
            if (post.UserId != Message.User.Id && !Message.User.IsAdmin)//post creators or admins can delete
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoPostAccess"]);
                return;
            }
            
            string txt = string.Join(" ", Command.Skip(1));

            Parser.ParseTimespanFull(ref txt, out TimeSpan? ts1, out TimeSpan? ts2, out DateTime? dt1, out DateTime? dt2);

            var endTime = dt1 ?? dt2 ?? (DateTime.Now + (ts1 ?? ts2));

            if (endTime.HasValue)
            {
                post.EndDate = endTime.Value;
                post.HasEndDate = true;
                await Handler.MakePost(post, Parser);
                Config.Save();
            }
            else
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandRaidTimespanInvalid"], string.Join(" ", Command.Skip(1))));
        }

        [BotCommand("h")]
        [BotCommand("help")]
        private async Task Help()
        {
            var embed = Output.GetHelpEmbed(Config, Message.User.IsAdmin);
            await Message.Channel.SendMessageAsync(string.Format(Parser.Language.Strings["helpTop"], Config.OutputChannel), false, embed);
        }

        [BotCommand("culture")]
        private async Task Culture()
        {
            if (!await CheckAdminAccess()) return;

            GuildConfig.Language = Command[1];
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandLanguageSuccess"], Message.Server.Name, Command[1]));
        }

        [BotCommand("timezone")]
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
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandTimezoneSuccess"], Message.Server.Name, timezoneOut > -1 ? "+" + timezoneOut.ToString() : timezoneOut.ToString()));
        }

        [BotCommand("channel")]
        private async Task Channel()
        {
            if (!await CheckAdminAccess()) return;

            if (Command.Count() > 1 && !string.IsNullOrEmpty(Command[1]))
            {
                var channel = await GetChannelFromName(Command[1]);
                if (channel == null) return;

                GuildConfig.OutputChannelId = channel.Id;
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelSuccess"], Message.Server.Name, channel.Name));//// $"Output channel for {Guild.Name} changed to {channel.Name}");

            }
            else
            {
                GuildConfig.OutputChannelId = null;
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelCleared"], Message.Server.Name, Config.OutputChannel));//$"Output channel override for {Guild.Name} removed, default value \"{Config.OutputChannel}\" will be used.");
            }
        }

        [BotCommand("nochannel")]
        private async Task NoChannel()
        {
            if (!await CheckAdminAccess()) return;

            GuildConfig.OutputChannelId = 0;
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandNoChannelSuccess"]);
        }

        [BotCommand("alias")]
        private async Task Alias()
        {
            if (!await CheckAdminAccess()) return;

            PokemonInfo foundInfo = Parser.ParsePokemon(Command[1]);

            if (foundInfo == null)
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPokemonNotFound"], Command[1]));

            if (GuildConfig.PokemonAliases.ContainsKey(foundInfo.Id))
                GuildConfig.PokemonAliases[foundInfo.Id].Add(Command[2].ToLower());

            Config.Save();

            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandAliasSuccess"], Command[2].ToLower(), foundInfo.Name));
        }

        [BotCommand("removealias")]
        private async Task RemoveAlias()
        {
            if (!await CheckAdminAccess()) return;
            var aresp = "";

            var info = Parser.ParsePokemon(Command[1]);

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

        [BotCommand("pinall")]
        private async Task PinAll()
        {
            if (!await CheckAdminAccess()) return;
            foreach (var pinallChannel in Message.Server.Channels)
            {
                if (!GuildConfig.PinChannels.Contains(pinallChannel.Id))
                {
                    GuildConfig.PinChannels.Add(pinallChannel.Id);
                }
            }
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandPinAllSuccess"]);
        }

        [BotCommand("pin")]
        private async Task Pin()
        {
            if (!await CheckAdminAccess()) return;
            var channel = await GetChannelFromName(Command[1]);
            if (channel == null) return;

            if (!GuildConfig.PinChannels.Contains(channel.Id))
            {
                GuildConfig.PinChannels.Add(channel.Id);
                Config.Save();
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPinSuccess"], channel.Name));//$"{pinchannel.Name} added to Pin Channels.");
            }
            else
            {
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPinAlreadyDone"], channel.Name));// $"{pinchannel.Name} is already in Pin Channels.");
            }
        }

        [BotCommand("unpinall")]
        private async Task UnPinAll()
        {
            if (!await CheckAdminAccess()) return;
            GuildConfig.PinChannels.Clear();
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandUnPinAllSuccess"]);
        }

        [BotCommand("unpin")]
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

        [BotCommand("pinlist")]
        private async Task PinList()
        {
            var pinstring = "";
            foreach (var channel in Message.Server.Channels)
            {
                if (GuildConfig.PinChannels.Contains(channel.Id))
                    pinstring += "\n" + channel.Name;
            }

            if (string.IsNullOrEmpty(pinstring)) pinstring = Parser.Language.Strings["commandPinListNone"];//"No channels in Pin List."
            else pinstring = string.Format(Parser.Language.Formats["commandPinListHeader"], pinstring);// "Pinned Channels:" + pinstring;

            await Handler.MakeCommandMessage(Message.Channel, pinstring);
        }

        [BotCommand("city")]
        private async Task City()
        {
            if (!await CheckAdminAccess()) return;

            var cityString = string.Join(" ", Command.Skip(1));
            GuildConfig.City = cityString;
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandCitySuccess"], Message.Server.Name, cityString));//$"{unpinchannel.Name} removed from Pin Channels.");
        }

        [BotCommand("channelcity")]
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
                await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandChannelCityCleared"], Message.Server.Name, GuildConfig.City));//$"Output channel override for {Guild.Name} removed, default value \"{Config.OutputChannel}\" will be used.");
            }
        }

        [BotCommand("cities")]
        private async Task Cities()
        {
            if (!await CheckAdminAccess()) return;

            var str = string.Format("Default: {0}", GuildConfig.City);

            foreach (var city in GuildConfig.ChannelCities)
            {
                var channel = Message.Server.Channels.FirstOrDefault(x => x.Id == city.Key);
                str += string.Format("\n{0}: {1}", channel.Name, city.Value);
            }

            await Handler.MakeCommandMessage(Message.Channel, str);
        }

        [BotCommand("mute")]
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

        [BotCommand("unmute")]
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

        [BotCommand("muteall")]
        private async Task MuteAll()
        {
            if (!await CheckAdminAccess()) return;

            foreach (var channel in Message.Server.Channels)
            {
                if (!GuildConfig.MuteChannels.Contains(channel.Id))
                {
                    GuildConfig.MuteChannels.Add(channel.Id);
                }
            }
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandMuteAllSuccess"]);//all muted

        }

        [BotCommand("unmuteall")]
        private async Task UnMuteAll()
        {
            if (!await CheckAdminAccess()) return;
            GuildConfig.MuteChannels.Clear();
            Config.Save();
            await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandUnMuteAllSuccess"]);//all unmuted

        }

        [BotCommand("mutelist")]
        private async Task MuteList()
        {
            if (!await CheckAdminAccess()) return;
            var mutestring = "";
            foreach (var channel in Message.Server.Channels)
            {
                if (GuildConfig.MuteChannels.Contains(channel.Id))
                    mutestring += "\n" + channel.Name;
            }

            if (string.IsNullOrEmpty(mutestring)) mutestring = Parser.Language.Strings["commandMuteListNone"];
            else mutestring = string.Format(Parser.Language.Formats["commandMuteListHeader"], mutestring);// "     #[Muted Channels]:" + mutestring;

            await Handler.MakeCommandMessage(Message.Channel, mutestring);
        }

        [BotCommand("place")]
        private async Task Place()
        {
            if (!await CheckAdminAccess()) return;

            if(Command.Count() < 2)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandInvalidNumberOfParameters"]);
                return;
            }

            var cmdstr = string.Join(" ", Command.Skip(1));

            var strs = cmdstr.Split('\n');

            foreach(var s in strs)
            {
                var mystr = s;
                var latlng = Parser.ParseLatLong(ref mystr, "");

                var location = mystr.Trim();

                if (!string.IsNullOrEmpty(location))
                {
                    if (latlng == null || !latlng.HasValue)
                        latlng = await Parser.GetLocationLatLong(location, Message.Channel, Config);

                    GuildConfig.Places[location] = latlng;
                    if(strs.Length == 1)
                        await Handler.MakeCommandMessage(Message.Channel, string.Format(Parser.Language.Formats["commandPlaceSuccess"], location,
                            latlng != null && latlng.HasValue ? Parser.Language.Strings["at"] + latlng.ToString() : ""));
                }
                Config.Save();
            }
        }
        
        [BotCommand("deleteplace")]
        private async Task DeletePlace()
        {
            if (!await CheckAdminAccess()) return;

            if (Command.Count() < 2)
            {
                await Handler.MakeCommandMessage(Message.Channel, Parser.Language.Strings["commandInvalidNumberOfParameters"]);
                return;
            }

            var cmdstr = string.Join(" ", Command.Skip(1)).ToLower();

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

        [BotCommand("places")]
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

        [BotCommand("raidhelp")]
        private async Task RaidHelp()
        {
            var embed = Output.GetHelpEmbed(Config, false);
            await Message.Channel.SendMessageAsync(string.Format(Parser.Language.Strings["helpTop"], Config.OutputChannel), false, embed);
        }

        [BotCommand("version")]
        private async Task Version()
        {
            var version = Config.Version;// Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

            await Handler.MakeCommandMessage(Message.Channel, $"PokemonGoRaidBot {version}");
        }
    }
}
