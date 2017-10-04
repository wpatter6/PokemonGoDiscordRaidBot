using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PokemonGoRaidBot.Objects.Interfaces;
using PokemonGoRaidBot.Configuration;
using PokemonGoRaidBot.Objects;

namespace PokemonGoRaidBot.Services.Parsing
{
    public class MessageParser
    {
        public ParserLanguage Language;

        private const int latLongComparisonMaxMeters = 80;
        private const int eggMinutes = 60;
        private const int maxRaidMinutes = 120;
        private const int defaultRaidMinutes = 60;
        private const string matchedWordReplacement = "#|#|#|#";//when trying to match location, replace pokemon names and time spans with this string
        private const string matchedPokemonWordReplacement = "#$##$$###";
        private const string matchedLocationWordReplacement = "&&&&-&-&&&&";
        private const string matchedTimeWordReplacement = "===!!!===";

        private IBotServerConfiguration serverConfig;

        public string Lang;
        public int TimeOffset;

        public MessageParser(IBotServerConfiguration serverConfig, string language = "en-us", int timeZoneOffset = 0)
        {
            this.serverConfig = serverConfig;
            Lang = language;
            Language = new ParserLanguage(language);
            TimeOffset = timeZoneOffset;
            CultureInfo.CurrentCulture = Language.GetCultureInfo();
        }

        #region Input
        /// <summary>
        /// Attempts to parse the necessary information out of a message to create a raid post.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="config"></param>
        /// <returns>If return value is null, or property 'Pokemon' is null, raid post is invalid.</returns>
        public PokemonRaidPost ParsePost(IChatMessage message)
        {
            //var guild = ((SocketGuildChannel)message.Channel).Guild;
            //var guildConfig = config.GetServerConfig(message.Server.Id, message.ChatType, message.Server.Name);

            var result = new PokemonRaidPost()
            {
                GuildId = message.Server.Id,
                PostDate = DateTime.Now,
                UserId = message.User.Id,
                Color = GetRandomColorRGB(),
                LastMessageDate = DateTime.Now,
                ChannelMessages = new Dictionary<ulong, PokemonRaidPostOwner>(),
                EndDate = DateTime.Now + new TimeSpan(0, defaultRaidMinutes, 0),
                MentionedRoleIds = new List<ulong>()
            };

            result.ChannelMessages[message.Channel.Id] = new PokemonRaidPostOwner(default(ulong), message.User.Id);

            var messageString = message.Content.Replace(" & ", $" {Language.Strings["and"]} ").Replace(" @ ", $" {Language.Strings["at"]} ");
            var words = messageString.Split(' ');
            
            var i = 0;
            var nopost = false;

            var unmatchedWords = new List<string>();
            
            foreach (var word in words)
            {
                i++;

                var cleanedword = word;
                bool isMention = false;
                
                var roleReg = Language.RegularExpressions["discordRole"];
                var userReg = Language.RegularExpressions["discordUser"];
                var channelReg = Language.RegularExpressions["discordChannel"];

                //clean up all role, user and channel mentions
                if (roleReg.IsMatch(word))
                {
                    var roleId = Convert.ToUInt64(roleReg.Match(word).Groups[1].Value);
                    cleanedword = message.MentionedRoles.FirstOrDefault(x => x.Id == roleId)?.Name ?? cleanedword;
                    messageString = messageString.Replace(word, "@" + cleanedword);//This must be done so mentions display properly in embed with android
                    isMention = true;
                }
                else if (userReg.IsMatch(word))
                {
                    var userId = Convert.ToUInt64(userReg.Match(word).Groups[1].Value);
                    var u = message.MentionedUsers.FirstOrDefault(x => x.Id == userId);

                    cleanedword = u?.Nickname ?? u?.Name ?? cleanedword;
                    messageString = messageString.Replace(word, "@" + cleanedword);
                    isMention = true;
                }
                else if (channelReg.IsMatch(word))
                {
                    var channelId = Convert.ToUInt64(channelReg.Match(word).Groups[1].Value);
                    cleanedword = message.MentionedChannels.FirstOrDefault(x => x.Id == channelId)?.Name ?? cleanedword;
                    messageString = messageString.Replace(word, "#" + cleanedword);
                    isMention = true;
                }

                var garbageRegex = Language.RegularExpressions["garbage"];
                if (garbageRegex.IsMatch(cleanedword))
                {
                    unmatchedWords.Add(matchedWordReplacement);
                    continue;
                }

                if (result.PokemonId == default(int))
                {
                    var pokemon = ParsePokemon(cleanedword);
                    if (pokemon != null)
                    {
                        result.PokemonId = pokemon.Id;
                        result.PokemonName = pokemon.Name;

                        if (i > 1 && Language.RegularExpressions["pokemonInvalidators"].IsMatch(words[i - 2]))//if previous word invalidates -- ex "any Snorlax?"
                            nopost = true;

                        unmatchedWords.Add(matchedPokemonWordReplacement);
                        continue;
                    }
                }

                if (isMention) cleanedword = matchedWordReplacement;

                unmatchedWords.Add(cleanedword);
            }

            var unmatchedString = string.Join(" ", unmatchedWords.ToArray());

            //post was invalidated
            if (nopost) result.PokemonId = default(int);

            //lat longs will break the timespan parser and are easy to identify, so get them first
            var latLong = ParseLatLong(ref unmatchedString);
            
            TimeSpan? raidTimeSpan, joinTimeSpan;
            DateTime? raidTime, joinTime;
            ParseTimespanFull(ref unmatchedString, out raidTimeSpan, out joinTimeSpan, out raidTime, out joinTime);

            if (joinTimeSpan.HasValue) joinTime = DateTime.Now + joinTimeSpan.Value;

            if (raidTime.HasValue)
            {
                result.EndDate = raidTime.Value;
                result.HasEndDate = true;
            }
            else if (raidTimeSpan.HasValue)
            {
                var resultTs = new TimeSpan(0, maxRaidMinutes, 0);
                if (raidTimeSpan.Value < resultTs) resultTs = raidTimeSpan.Value;

                result.EndDate = DateTime.Now + resultTs;
                result.HasEndDate = true;
            }
            
            bool isMore, isLess;

            var joinCount = ParseJoinedUsersCount(ref unmatchedString, out isMore, out isLess);

            if (!joinCount.HasValue && joinTime.HasValue) joinCount = 1;

            if (joinCount.HasValue)
                result.JoinedUsers.Add(new PokemonRaidJoinedUser(message.User.Id, message.Server.Id, result.UniqueId, message.User.Name, joinCount.Value, isMore, isLess, joinTime));

            if (latLong.HasValue)
            {
                result.Location = latLong.ToString();
                result.LatLong = latLong;
            }
            else
            {
                result.Location = ParseLocation(unmatchedString, serverConfig);

                if (!string.IsNullOrEmpty(result.Location))
                    result.FullLocation = GetFullLocation(result.Location, serverConfig, message.Channel.Id);
            }

            result.Responses.Add(new PokemonMessage(message.User.Id, message.User.Name, messageString, DateTime.Now, message.Channel.Name));

            var mention = message.Server.Roles.FirstOrDefault(x => x.Name == result.PokemonName); //((SocketGuildChannel)message.Channel).Guild.Roles.FirstOrDefault(x => x.Name == result.PokemonName);
            if (mention != null && !message.MentionedRoles.ToList().Contains(mention) && !result.MentionedRoleIds.Contains(mention.Id))//avoid double notification
                result.MentionedRoleIds.Add(mention.Id);

            if (Language.RegularExpressions["sponsored"].IsMatch(message.Content))
            {
                mention = message.Server.Roles.FirstOrDefault(x => x.Name.ToLower() == Language.Strings["sponsored"].ToLower());
                if (mention != null && !message.MentionedRoles.ToList().Contains(mention) && !result.MentionedRoleIds.Contains(mention.Id))//avoid double notification
                    result.MentionedRoleIds.Add(mention.Id);
            }

            foreach (var role in message.Server.Roles.Where(x => !message.MentionedRoles.Contains(x) && !result.MentionedRoleIds.Contains(x.Id)))
            {//notify for roles that weren't actually mentions
                var reg = new Regex($@"\b{role.Name}\b", RegexOptions.IgnoreCase);
                if (reg.IsMatch(message.Content))
                    result.MentionedRoleIds.Add(role.Id);
            }

            result.IsValid = result.PokemonId != default(int) && (!string.IsNullOrWhiteSpace(result.Location));

            return result;
        }
        /// <summary>
        /// Attempts to match a single word string with a pokemon's name or aliases.  
        /// The string must be longer than three characters
        /// And will only match aliases exactly, or the beginning or entierty of the pokemon's name.
        /// </summary>
        /// <returns></returns>
        public PokemonInfo ParsePokemon(string name)
        {

            var cleanedName = Regex.Replace(name, @"\W", "").ToLower();//never want any special characters in string

            if (cleanedName.Length < 3 || Language.RegularExpressions["pokemonTooShort"].IsMatch(name)) return null;
            
            var result = Language.Pokemon.FirstOrDefault(x => serverConfig.PokemonAliases.Where(xx => xx.Value.Contains(name.ToLower())).Count() > 0);
            if (result != null && result.CatchRate > 0) return result;

            result = Language.Pokemon.FirstOrDefault(x => x.Aliases.Contains(cleanedName));
            if (result != null && result.CatchRate > 0) return result;

            result = Language.Pokemon.OrderByDescending(x => x.Id).FirstOrDefault(x => x.Name.ToLower().StartsWith(cleanedName));
            if (result != null && result.CatchRate > 0) return result;

            return null;
        }
        /// <summary>
        /// Gets both the raid time span and joining user timespan from a full message string.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="raidTimeSpan"></param>
        /// <param name="joinTimeSpan"></param>
        public void ParseTimespanFull(ref string message, out TimeSpan? raidTimeSpan, out TimeSpan? joinTimeSpan, out DateTime? actualRaidTime, out DateTime? actualJoinTime)
        {
            raidTimeSpan = null;
            joinTimeSpan = null;
            actualRaidTime = null;
            actualJoinTime = null;

            if (string.IsNullOrEmpty(message)) return;

            var joinReg = Language.RegularExpressions["joinTime"];
            var eggReg = Language.RegularExpressions["eggTime"];
            var actualRegEnd = Language.RegularExpressions["timeActualEnd"];
            if (actualRegEnd.IsMatch(message))
            {
                var matches = actualRegEnd.Matches(message).Cast<Match>();
                foreach (var match in matches)
                {
                    var hr = match.Groups[1].Value;
                    var min = match.Groups[2].Value;

                    int hour = Convert.ToInt32(hr),
                        minute = string.IsNullOrEmpty(min) ? 0 : Convert.ToInt32(min);

                    string part = match.Groups[3].Value;
                    if (string.IsNullOrEmpty(part)) part = DateTime.Now.Hour < hour ? "p" : DateTime.Now.ToString("tt").ToLower();

                    if (part.First() == 'p')
                        hour += 12;

                    var actualTime = DateTime.Today.AddHours(hour).AddMinutes(minute);
                    //var tsout = ParseTimeSpanBase(string.Format("{0}:{1}", hour, minute), TimeOffset * -1);

                    //if (tsout.HasValue)
                    //{
                    message = message.Replace(match.Groups[0].Value, matchedTimeWordReplacement);

                    if (joinReg.IsMatch(message))
                    {
                        message = joinReg.Replace(message, matchedWordReplacement);
                        actualJoinTime = actualTime;// = tsout.Value;
                    }
                    else
                        actualRaidTime = actualTime;// = tsout.Value;
                    //}
                }

                if (joinTimeSpan.HasValue && raidTimeSpan.HasValue)
                    return;
            }

            var actualRegStart = Language.CombineRegex(" ", "timeActualStartPre", "timeActualStart");
            if (actualRegStart.IsMatch(message))
            {
                var matches = actualRegStart.Matches(message).Cast<Match>();
                foreach (var match in matches)
                {
                    var hr = match.Groups[2].Value;
                    var min = match.Groups[3].Value;

                    int hour = Convert.ToInt32(hr),
                        minute = string.IsNullOrEmpty(min) ? 0 : Convert.ToInt32(min);

                    string part = DateTime.Now.Hour < hour ? "p" : DateTime.Now.ToString("tt").ToLower();

                    if (part.First() == 'p' && hour < 12) hour += 12;

                    var tsout = ParseTimeSpanBase(string.Format("{0}:{1}", hour, minute), TimeOffset * -1);
                    
                    if (tsout.HasValue)
                    {
                        message = message.Replace(match.Groups[0].Value, matchedTimeWordReplacement);

                        if (joinReg.IsMatch(message))
                        {
                            message = joinReg.Replace(message, matchedWordReplacement);
                            joinTimeSpan = tsout.Value;
                        }
                        else
                            raidTimeSpan = tsout.Value;
                    }
                }

                if (joinTimeSpan.HasValue && raidTimeSpan.HasValue)
                    return;
            }
            
            var tsRegex = Language.RegularExpressions["timeSpan"];//new Regex("([0-9]):([0-9]{2})");
            if (tsRegex.IsMatch(message))
            {
                var matches = tsRegex.Matches(message).Cast<Match>();
                foreach(var match in matches)
                {
                    int hour = Convert.ToInt32(match.Groups[1].Value),
                        minute = Convert.ToInt32(match.Groups[2].Value);
                
                    TimeSpan ts;
                    if (hour < 2)//this will miss if someone posts an actual time of 1:30 (without pm or matching actualRegEnd, should be rare)
                        ts = new TimeSpan(hour, minute, 0);
                    else
                    {
                        if (hour < DateTime.Now.Hour) hour += 12;//PM is inferred -- this may be where the long running raids are coming from... Also should handle timezone better...

                        var tsout = ParseTimeSpanBase(string.Format("{0}:{1}", hour, minute + 1), TimeOffset * -1);
                        if (tsout.HasValue)
                            ts = tsout.Value;
                    }

                    message = message.Replace(match.Groups[0].Value, matchedTimeWordReplacement);

                    if (joinReg.IsMatch(message))
                    {
                        message = joinReg.Replace(message, matchedWordReplacement);
                        joinTimeSpan = ts;
                    }
                    else if (eggReg.IsMatch(message))
                    {
                        message = eggReg.Replace(message, matchedWordReplacement);
                        raidTimeSpan = ts.Add(new TimeSpan(0, eggMinutes, 0));
                    } else
                        raidTimeSpan = ts;
                }
                if (joinTimeSpan.HasValue && raidTimeSpan.HasValue)
                    return;
            }

            var hrminReg = Language.RegularExpressions["timeHourMin"];
            if (hrminReg.IsMatch(message))
            {
                var matches = hrminReg.Matches(message).Cast<Match>();
                foreach(var match in matches)
                {
                    var hr = match.Groups[1].Value;
                    var mi = match.Groups[3].Value;

                    var hour = string.IsNullOrEmpty(hr) ? 1 : Convert.ToInt32(hr);
                    var min = string.IsNullOrEmpty(mi) ? 0 : Convert.ToInt32(mi);

                    if (min + hour == 0) continue;

                    message = message.Replace(match.ToString(), matchedTimeWordReplacement);

                    if (joinReg.IsMatch(message))
                    {
                        message = joinReg.Replace(match.Groups[0].Value, matchedWordReplacement);
                        joinTimeSpan = new TimeSpan(hour, min, 0);
                    }
                    else if (eggReg.IsMatch(message))
                    {
                        message = eggReg.Replace(message, matchedWordReplacement);
                        raidTimeSpan = new TimeSpan(hour, min + eggMinutes, 0);
                    }
                    else
                        raidTimeSpan = new TimeSpan(hour, min, 0);
                }

                if (joinTimeSpan.HasValue && raidTimeSpan.HasValue)
                    return;
            }
            else
            {
                var hrAndHalfReg = Language.RegularExpressions["timeHourHalf"];
                if (hrAndHalfReg.IsMatch(message))
                {
                    message = hrAndHalfReg.Replace(message, matchedTimeWordReplacement);

                    if (joinReg.IsMatch(message))
                    {
                        message = joinReg.Replace(message, matchedWordReplacement);
                        joinTimeSpan = new TimeSpan(1, 30, 0);
                    }
                    else
                        raidTimeSpan = new TimeSpan(1, 30, 0);
                    return;
                }

                var halfHourReg = Language.RegularExpressions["timeHalfHour"];
                if (halfHourReg.IsMatch(message))
                {
                    message = halfHourReg.Replace(message, matchedTimeWordReplacement);

                    if (joinReg.IsMatch(message))
                    {
                        message = joinReg.Replace(message, matchedWordReplacement);
                        joinTimeSpan = new TimeSpan(0, 30, 0);
                    }
                    else if (eggReg.IsMatch(message))
                    {
                        message = eggReg.Replace(message, matchedWordReplacement);
                        raidTimeSpan = new TimeSpan(0, 30 + eggMinutes, 0);
                    }
                    else
                        raidTimeSpan = new TimeSpan(0, 30, 0);
                    return;
                }

                var hrReg = Language.RegularExpressions["timeHour"];
                if (hrReg.IsMatch(message))
                {
                    var matches = hrReg.Matches(message).Cast<Match>();
                    foreach (var match in matches)
                    {
                        var hour = string.IsNullOrWhiteSpace(match.Groups[1].Value) ? 1 : Convert.ToInt32(match.Groups[1].Value);

                        message = message.Replace(match.Groups[0].Value, matchedTimeWordReplacement);

                        if (joinReg.IsMatch(message))
                        {
                            message = joinReg.Replace(message, matchedWordReplacement);
                            joinTimeSpan = new TimeSpan(hour, 0, 0);
                        }
                        else if (eggReg.IsMatch(message))
                        {
                            message = eggReg.Replace(message, matchedWordReplacement);
                            raidTimeSpan = new TimeSpan(hour, eggMinutes, 0);
                        }
                        else
                            raidTimeSpan = new TimeSpan(hour, 0, 0);
                    }
                    if (joinTimeSpan.HasValue && raidTimeSpan.HasValue)
                        return;
                }

                var minReg = Language.RegularExpressions["timeMin"];
                if (minReg.IsMatch(message))
                {
                    var matches = minReg.Matches(message).Cast<Match>();
                    foreach (var match in matches)
                    {
                        var min = Convert.ToInt32(match.Groups[1].Value);

                        message = message.Replace(match.Groups[0].Value, matchedTimeWordReplacement);
                        if (joinReg.IsMatch(message))
                        {
                            message = joinReg.Replace(message, matchedWordReplacement);
                            joinTimeSpan = new TimeSpan(0, min, 0);
                        }
                        else if (eggReg.IsMatch(message))
                        {
                            message = eggReg.Replace(message, matchedWordReplacement);
                            raidTimeSpan = new TimeSpan(0, min + eggMinutes, 0);
                        }
                        else
                            raidTimeSpan = new TimeSpan(0, min, 0);
                    }
                }
            }

        }
        /// <summary>
        /// Base methods for parsing timespans.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public TimeSpan? ParseTimeSpanBase(string str, int offset)
        {
            TimeSpan tsout = new TimeSpan();
            if (TimeSpan.TryParse(str, out tsout))
            {
                var postedDate = DateTime.Today.Add(tsout).AddHours(offset);
                return new TimeSpan(postedDate.Ticks - DateTime.Now.Ticks);
            }
            return null;
        }
        /// <summary>
        /// Attempts to get a location out of a full message string.
        /// </summary>
        /// <param name="message"></param>
        /// <returns>The string representation of the location</returns>
        public string ParseLocation(string message, IBotServerConfiguration guildConfig)
        {
            var cleanedMessage = Language.RegularExpressions["locationExcludeWords"].Replace(message, matchedWordReplacement).ToLower();

            foreach(var place in guildConfig.Places)
            {
                var reg = new Regex($@"\b{place.Key}\b", RegexOptions.IgnoreCase);
                if (reg.IsMatch(cleanedMessage))
                    return ToTitleCase(place.Key);
            }

            var result = ParseLocationBase(cleanedMessage);

            var cleanreg = Language.RegularExpressions["locationCleanWords"];
            var cleaned = cleanreg.Replace(result.Trim(), "");

            var cleanedLocation = cleaned.Replace(",", "").Replace(".", "").Replace("  ", " ").Replace(matchedWordReplacement, "").Trim();

            if (Language.RegularExpressions["locationTooShort"].IsMatch(cleanedLocation)) return "";

            return ToTitleCase(cleanedLocation);
        }
        /// <summary>
        /// Parses a bot's message to get an existing post from the config using the unique id.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public PokemonRaidPost ParsePostFromPostMessage(string message, IBotServerConfiguration config)
        {
            var uidReg = Language.RegularExpressions["postUniqueId"];
            if (!uidReg.IsMatch(message)) return null;

            var uid = uidReg.Match(message).Groups[1].Value;

            return config.Posts.FirstOrDefault(x => x.UniqueId == uid);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private string ParseLocationBase(string message)
        {
            var endReg = Language.RegularExpressions["locationEnd"]; //endreg seems to match the best, and generally google directions go to parking more than with cross streets
            if (endReg.IsMatch(message))
                return endReg.Match(message).Groups[1].Value;

            var urlReg = Language.RegularExpressions["url"];
            var crossStreetsReg = Language.RegularExpressions["locationCrossStreets"]; //pretty reliable but can have some missed matches
            if (crossStreetsReg.IsMatch(message))
            {
                var groups = crossStreetsReg.Match(message).Groups;

                if(!urlReg.IsMatch(groups[1].Value))
                    return groups[1].Value.Replace(groups[3].Value, $" {Language.Strings["and"]} ");
            }

            var startReg = Language.CombineRegex(" ", "locationStartPre", "locationStart");//basically "at [foo] [bar].
            if (startReg.IsMatch(message))
            {
                var match = startReg.Match(message);
                return Language.RegularExpressions["locationStartPre"].Replace(match.Groups[2].Value, "", 1);
            }

            return "";
        }

        /// <summary>
        /// Attempts to determine if two locations are the same.
        /// </summary>
        /// <param name="loc1"></param>
        /// <param name="loc2"></param>
        /// <returns></returns>
        public bool CompareLocationStrings(string loc1, string loc2, bool triedCrossStreets = false)
        {
            if (string.IsNullOrEmpty(loc1) || string.IsNullOrEmpty(loc2)) return false;

            loc1 = loc1.ToLower();
            loc2 = loc2.ToLower();

            if (loc1 == loc2 || loc1.StartsWith(loc2) || loc2.StartsWith(loc1)) return true;

            //Check if they used any abbreviations -- not language dependent
            var abbrReg1 = new Regex(Regex.Escape(loc1).Replace(" ", "[a-zA-Z]* ").Trim() + "[a-zA-Z]*");
            var abbrReg2 = new Regex(Regex.Escape(loc2).Replace(" ", "[a-zA-Z]* ").Trim() + "[a-zA-Z]*");

            if (abbrReg1.IsMatch(loc2)) return true;
            if (abbrReg2.IsMatch(loc1)) return true;

            if (triedCrossStreets) return false;

            var crossStreetsReg = Language.RegularExpressions["locationCrossStreets"];//new Regex(@"([a-zA-Z0-9]*) (\&|and) ([a-zA-Z0-9]*)", RegexOptions.IgnoreCase);
            if (crossStreetsReg.IsMatch(loc1) && crossStreetsReg.IsMatch(loc2))
            {
                var match = crossStreetsReg.Match(loc2);
                return CompareLocationStrings(loc1, string.Format("{0} {1} {2}", match.Groups[4].Value, match.Groups[3].Value, match.Groups[2].Value), true);
            }

            return false;
        }
        //Not being used right now -- needs to happen *after* google lat/long
        //public bool CompareLocationLatLong(GeoCoordinate ll1, GeoCoordinate ll2)
        //{
        //    var distance = DistanceBetweenTwoPoints(ll1, ll2);
        //    return distance < latLongComparisonMaxMeters;
        //}
        public int? ParseJoinedUsersCount(ref string message, out bool isMore, out bool isLess)
        {
            isLess = isMore = false;
            var endreg = Language.RegularExpressions["joinEnd"];

            if (endreg.IsMatch(message))
            {
                var endmatch = endreg.Match(message);
                
                if (Language.RegularExpressions["joinLess"].IsMatch(message)) isLess = true;
                else if (Language.RegularExpressions["joinMore"].IsMatch(message)) isMore = true;

                message = endreg.Replace(message, matchedWordReplacement);

                var num = endmatch.Groups[1].Value;
                var result = 0;

                if (!int.TryParse(num, out result))
                    result = WordToInteger(num);

                if (result == -1 || (result == 1 && message.Contains(Language.Strings["questionMark"]))) return null;
                return result;
            }

            var startReg = Language.RegularExpressions["joinStart"];
            if (startReg.IsMatch(message))
            {
                var startmatch = startReg.Match(message);
                
                if (Language.RegularExpressions["joinLess"].IsMatch(message)) isLess = true;
                else if (Language.RegularExpressions["joinMore"].IsMatch(message)) isMore = true;

                message = startReg.Replace(message, matchedWordReplacement);

                var num = startmatch.Groups[2].Value;
                var result = 0;

                if (!int.TryParse(num, out result))
                    result = WordToInteger(num);
                
                if (result == -1 || (result == 1 && message.Contains(Language.Strings["questionMark"]))) return null;
                return result;
            }

            var meReg = Language.RegularExpressions["joinMe"];
            if (meReg.IsMatch(message))
            {
                message = meReg.Replace(message, matchedWordReplacement);
                if (message.Contains(Language.Strings["questionMark"])) return null;
                return 1;
            }

            var moreReg = Language.RegularExpressions["joinMore"];
            if (moreReg.IsMatch(message))
            {
                var morematch = moreReg.Match(message);

                message = moreReg.Replace(message, matchedWordReplacement);
                
                var num = morematch.Groups[2].Value;
                var result = 0;

                if (!int.TryParse(num, out result))
                    result = WordToInteger(num);

                isMore = true;

                if (result == -1 || (result == 1 && message.Contains(Language.Strings["questionMark"]))) return null;
                return result;
            }

            var lessReg = Language.RegularExpressions["joinLess"];
            if (lessReg.IsMatch(message))
            {
                var lessmatch = lessReg.Match(message);

                message = lessReg.Replace(message, matchedWordReplacement);
                
                var num = lessmatch.Groups[1].Value;
                var result = 0;

                if (!int.TryParse(num, out result))
                    result = WordToInteger(num);
                
                isLess = true;

                if (result == -1 || (result == 1 && message.Contains(Language.Strings["questionMark"]))) return null;
                return result;
            }
            
            return null;
        }
        /// <summary>
        /// Uses google geocoding API to get the lat & long from a full location string
        /// </summary>
        /// <param name="location"></param>
        /// <param name="channel"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public async Task<GeoCoordinate> GetLocationLatLong(string location, IChatChannel channel, IBotConfiguration config)
        {
            if (string.IsNullOrEmpty(config.GoogleApiKey)) return null;

            var url = string.Format(Language.Formats["googleMapGeocodeApi"], location, config.GoogleApiKey);

            var request = (HttpWebRequest)WebRequest.Create(url);
            dynamic fullresult;
            
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            {
                using (var responseStream = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(responseStream))
                    {
                        fullresult = JsonConvert.DeserializeObject<dynamic>(reader.ReadToEnd());
                    }
                }
            }
            if (fullresult.results != null && ((JArray)fullresult.results).Count() > 0)
            {

                var lat = ((JValue)fullresult.results[0].geometry.location.lat).ToObject<double>();
                var lng = ((JValue)fullresult.results[0].geometry.location.lng).ToObject<double>();

                return new GeoCoordinate(lat, lng);
            }
            else
            {
                return null;
            }
        }
        public GeoCoordinate ParseLatLong(ref string message, string replacement = matchedLocationWordReplacement)
        {
            var reg = Language.RegularExpressions["latLong"];
            if (reg.IsMatch(message))
            {
                var match = reg.Match(message);

                message = message.Replace(match.Value, replacement);

                return new GeoCoordinate(Convert.ToDouble(match.Groups[1].Value + match.Groups[2].Value), Convert.ToDouble(match.Groups[5].Value + match.Groups[6].Value));
            }

            return new GeoCoordinate();
        }
        public string GetFullLocation(string location, IBotServerConfiguration guildConfig, ulong channelId)
        {
            var city = guildConfig.ChannelCities.ContainsKey(channelId) ? guildConfig.ChannelCities[channelId] : guildConfig.City ?? "";
            if (!string.IsNullOrEmpty(city)) city = " near " + city;
            return Uri.EscapeDataString(location + city);
        }

        #region helpers
        private double DistanceBetweenTwoPoints(GeoCoordinate ll1, GeoCoordinate ll2)
        {
            if (ll1 == null || !ll1.HasValue || ll2 == null || !ll2.HasValue) return double.MaxValue;

            var R = 6371e3; // metres
            var φ1 = ll1.Latitude.Value / (180 / Math.PI);
            var φ2 = ll2.Latitude.Value / (180 / Math.PI);
            var Δφ = (ll2.Latitude.Value - ll1.Latitude.Value) / (180 / Math.PI);
            var Δλ = (ll2.Longitude.Value - ll1.Longitude.Value) / (180 / Math.PI);

            var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                    Math.Cos(φ1) * Math.Cos(φ2) *
                    Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            var d = R * c;
            return d;
        }
        //todo add this nonsense to language file -- why the hell do people write numbers this way in chat??
        private int WordToInteger(string str)
        {
            var arr = new List<string>(new string[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" });

            return arr.IndexOf(str.ToLower());
        }
        public string ToTitleCase(string str)
        {
            var result = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str);
            return Language.RegularExpressions["smallWords"].Replace(result, m => m.ToString().ToLower());
        }
        private int[] GetRandomColorRGB()
        {
            var random = new Random();

            var result = new List<int>();
            result.Add(random.Next(256));
            result.Add(random.Next(256));
            result.Add(random.Next(256));
            return result.ToArray();
        }
        #endregion
        #endregion
    }
}
