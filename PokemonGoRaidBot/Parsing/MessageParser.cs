using Discord.WebSocket;
using PokemonGoRaidBot.Config;
using PokemonGoRaidBot.Objects;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Net;
using Discord;

namespace PokemonGoRaidBot.Parsing
{
    public class MessageParser
    {
        public ParserLanguage Language;

        private const int latLongComparisonMaxMeters = 80;
        private const int maxRaidMinutes = 120;
        private const string matchedWordReplacement = "#|#|#|#";//when trying to match location, replace pokemon names and time spans with this string
        private const string matchedPokemonWordReplacement = "#$##$$###";
        private const string matchedLocationWordReplacement = "&&&&-&-&&&&";
        private const string matchedTimeWordReplacement = "===!!!===";




        private int timeOffset;

        public MessageParser(string language = "en-us", int timeZoneOffset = 0)
        {
            Language = new ParserLanguage(language);
            timeOffset = timeZoneOffset;
        }

        #region Input
        /// <summary>
        /// Attempts to parse the necessary information out of a message to create a raid post.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="config"></param>
        /// <returns>If return value is null, or property 'Pokemon' is null, raid post is invalid.</returns>
        public async Task<PokemonRaidPost> ParsePost(SocketMessage message, BotConfig config)
        {
            var result = new PokemonRaidPost()
            {
                User = message.Author.Username,
                UserId = message.Author.Id,
                PostDate = DateTime.Now,//uses local time for bot
                FromChannelId = message.Channel.Id,
                EndDate = DateTime.Now + new TimeSpan(0, maxRaidMinutes, 0),
                MentionedRoleIds = new List<ulong>(message.MentionedRoles.Select(x => x.Id)),
                Color = GetRandomColorRGB()
            };

            var guildId = ((SocketGuildChannel)message.Channel).Guild.Id;
            var guildConfig = config.GetGuildConfig(guildId);
            var messageString = message.Content.Replace(" & ", $" {Language.Strings["and"]} ").Replace(" @ ", $" {Language.Strings["at"]} ");

            var words = messageString.Split(' ');

            var timespan = new TimeSpan();
            var i = 0;
            var nopost = false;

            var unmatchedWords = new List<string>();
            var isActualTime = false;
            
            foreach (var word in words)
            {
                i++;

                var cleanedword = Language.RegularExpressions["nonAlphaNumericWithPunctuation"].Replace(word, "");
                
                var roleReg = Language.RegularExpressions["discordRole"];
                var userReg = Language.RegularExpressions["discordUser"];
                var channelReg = Language.RegularExpressions["discordChannel"];

                //clean up all role, user and channel mentions
                if (roleReg.IsMatch(word))
                {
                    var roleId = Convert.ToUInt64(roleReg.Match(word).Groups[1].Value);
                    cleanedword = message.MentionedRoles.FirstOrDefault(x => x.Id == roleId)?.Name ?? cleanedword;
                    messageString = messageString.Replace(word, cleanedword);//This must be done so mentions display properly in embed with android
                }
                else if (userReg.IsMatch(word))
                {
                    var userId = Convert.ToUInt64(userReg.Match(word).Groups[1].Value);
                    cleanedword = message.MentionedUsers.FirstOrDefault(x => x.Id == userId)?.Username ?? cleanedword;
                    messageString = messageString.Replace(word, cleanedword);
                }
                else if (channelReg.IsMatch(word))
                {
                    var channelId = Convert.ToUInt64(channelReg.Match(word).Groups[1].Value);
                    cleanedword = message.MentionedChannels.FirstOrDefault(x => x.Id == channelId)?.Name ?? cleanedword;
                    messageString = messageString.Replace(word, cleanedword);
                }

                var garbageRegex = Language.RegularExpressions["garbage"];
                if (garbageRegex.IsMatch(cleanedword))
                {
                    unmatchedWords.Add(matchedWordReplacement);
                    continue;
                }

                if (result.PokemonId == default(int))
                {
                    var pokemon = ParsePokemon(cleanedword, config, guildId);
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

                var ts = ParseTimespan(word, ref isActualTime);
                if (ts.Ticks > 0)
                {
                    timespan = timespan.Add(ts);
                    unmatchedWords.Add(matchedTimeWordReplacement);
                    continue;
                }

                if (Language.RegularExpressions["minuteAliases"].IsMatch(cleanedword) && i > 1)//go back and get the previous word
                {
                    var mins = words[i - 2];
                    var min = 0;
                    var isminute = false;

                    if (int.TryParse(mins, out min))
                    {
                        timespan = timespan.Add(new TimeSpan(0, min, 0));
                        isminute = true;
                    }
                    else if (Language.RegularExpressions["aAliases"].IsMatch(cleanedword))
                    {
                        timespan = timespan.Add(new TimeSpan(0, 1, 0));
                        isminute = true;
                    }
                    if (isminute)
                    {
                        unmatchedWords[unmatchedWords.Count() - 1] = matchedTimeWordReplacement;
                        unmatchedWords.Add(matchedTimeWordReplacement);
                        continue;
                    }
                }

                if (Language.RegularExpressions["hourAliases"].IsMatch(cleanedword) && i > 1)//go back and get the previous word
                {
                    var hrs = words[i - 2];
                    var hr = 0;
                    var ishour = false;
                    if (int.TryParse(hrs, out hr))
                    {
                        timespan = timespan.Add(new TimeSpan(hr, 0, 0));
                        ishour = true;
                    }
                    else if (hrs.ToLowerInvariant() == "a" || hrs.ToLowerInvariant() == "an")
                    {
                        timespan = timespan.Add(new TimeSpan(1, 0, 0));
                        ishour = true;
                    }
                    if (ishour)
                    {
                        unmatchedWords[unmatchedWords.Count() - 1] = matchedTimeWordReplacement;
                        unmatchedWords.Add(matchedTimeWordReplacement);
                        continue;
                    }
                }
                //the word was not matched, add it to the cleaned array to check for location

                if (Language.RegularExpressions["atAliases"].IsMatch(cleanedword)) isActualTime = true;

                unmatchedWords.Add(cleanedword);
            }

            var unmatchedString = string.Join(" ", unmatchedWords.ToArray());

            //post was invalidated
            if (nopost) result.PokemonId = default(int);

            if (timespan.Ticks > 0)
            {
                var dt = result.PostDate + timespan;
                dt = dt.AddSeconds(dt.Second * -1);//make seconds "0"

                if (!isActualTime)//add actual time to end of string for message thread
                    messageString += string.Format(" ({0:h:mmtt})", dt.AddHours(timeOffset));

                var joinReg = Language.CombineRegex("joinEnd", "joinStart", "joinMe", "joinMore");
                if (!(joinReg.IsMatch(messageString) && !joinReg.IsMatch(unmatchedString)))//if it matches the full string but not the cleaned, we know the timespan is not remaining time
                {
                    result.EndDate = dt;
                    result.HasEndDate = true;
                }
            }

            var newUnmatchedString = "";
            bool isMore, isLess;
            var joinCount = ParseJoinedUsersCount(unmatchedString, out newUnmatchedString, out isMore, out isLess);

            if (joinCount.HasValue)
                result.JoinedUsers.Add(new PokemonRaidJoinedUser(message.Author.Id, message.Author.Username, joinCount.Value, isMore, isLess));

            result.Location = ParseLocation(newUnmatchedString);

            if (!string.IsNullOrEmpty(result.Location))
                result.FullLocation = GetFullLocation(result.Location, guildConfig, message.Channel.Id);

            if (!string.IsNullOrEmpty(result.FullLocation))
            {
                result.LatLong = await GetLocationLatLong(result.FullLocation, (SocketGuildChannel)message.Channel, config);
            }

            result.Responses.Add(new PokemonMessage(message.Author.Id, message.Author.Username, messageString, DateTime.Now));

            return result;
        }
        /// <summary>
        /// Attempts to match a single word string with a pokemon's name or aliases.  
        /// The string must be longer than three characters
        /// And will only match aliases exactly, or the beginning or entierty of the pokemon's name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public PokemonInfo ParsePokemon(string name, BotConfig config, ulong guildId)
        {
            if (name.Length < 3) return null;

            var cleanedName = Regex.Replace(name, @"\W", "").ToLowerInvariant();//never want any special characters in string

            var guildConfig = config.GetGuildConfig(guildId);

            var result = config.PokemonInfoList.FirstOrDefault(x => guildConfig.PokemonAliases.Where(xx => xx.Value.Contains(name.ToLowerInvariant())).Count() > 0);
            if (result != null) return result;

            result = config.PokemonInfoList.FirstOrDefault(x => x.Aliases.Contains(cleanedName));
            if (result != null) return result;

            result = config.PokemonInfoList.OrderByDescending(x => x.Id).FirstOrDefault(x => x.Name.ToLowerInvariant().StartsWith(cleanedName));
            if (result != null) return result;

            return null;
        }
        /// <summary>
        /// Attempts to get a time span out of a single word string.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public TimeSpan ParseTimespan(string message, ref bool isActualTime)
        {
            var timeRegex = Language.RegularExpressions["timeActual"];//new Regex("([0-9]{1,2}):?([0-9]{2})?(a|p)", RegexOptions.IgnoreCase);
            if (timeRegex.IsMatch(message))//posting an actual time like "3:30pm" or "10am"
            {
                var match = timeRegex.Match(message);
                int hour = Convert.ToInt32(match.Groups[1].Value),
                    minute = string.IsNullOrEmpty(match.Groups[2].Value) ? 0 : Convert.ToInt32(match.Groups[2].Value);
                string part = match.Groups[3].Value;

                if (part.Equals("p", StringComparison.OrdinalIgnoreCase))
                    hour += 12;

                var postedDate = DateTime.Today.Add(TimeSpan.Parse(string.Format("{0}:{1}", hour, minute + 1))).AddHours(timeOffset * -1);//subtract offset to convert to bot timezone
                isActualTime = true;
                return new TimeSpan(postedDate.Ticks - DateTime.Now.Ticks);
            }

            var tsRegex = Language.RegularExpressions["timeSpan"];//new Regex("([0-9]):([0-9]{2})");
            if (tsRegex.IsMatch(message))//posting like "1:30" -- ActualTime indicates it was preceeded by the word "at"
            {
                var match = tsRegex.Match(message);
                int hour = Convert.ToInt32(match.Groups[1].Value),
                    minute = Convert.ToInt32(match.Groups[2].Value);

                if (hour > 2) isActualTime = true;

                if (!isActualTime)
                    return new TimeSpan(hour, minute, 0);
                else
                {
                    if (DateTime.Now.Hour > 9 && hour < 9) hour += 12;//PM is inferred
                    var postedDate = DateTime.Today.Add(TimeSpan.Parse(string.Format("{0}:{1}", hour, minute + 1)));
                    return new TimeSpan(postedDate.Ticks - DateTime.Now.Ticks);
                }
            }

            var ts = new TimeSpan(0);
            var hrRegex = Language.RegularExpressions["timeHour"];//new Regex("([0-2]{1})h", RegexOptions.IgnoreCase);
            if (hrRegex.IsMatch(message))//posting like "1h"
            {
                var match = hrRegex.Match(message);
                string hour = match.Groups[1].Value;//, minute = match.Groups[1].Value;
                ts.Add(new TimeSpan(Convert.ToInt32(hour), 0, 0));
            }

            var minRegex = Language.RegularExpressions["timeMinute"];//new Regex("([0-9]{1,2})m");
            if (minRegex.IsMatch(message))
            {
                var match = minRegex.Match(message);
                string min = match.Groups[1].Value;//, minute = match.Groups[1].Value;
                ts = ts.Add(new TimeSpan(0, Convert.ToInt32(min), 0));
            }
            return ts;
        }
        /// <summary>
        /// Attempts to get a location out of a full message string.
        /// </summary>
        /// <param name="message"></param>
        /// <returns>The string representation of the location</returns>
        public string ParseLocation(string message)
        {
            var result = ParseLocationBase(message);
            var cleanedLocation = Language.RegularExpressions["locationExcludeWords"].Replace(result, "").Replace(",", "").Replace(".", "").Replace("  ", " ").Replace(matchedWordReplacement, "").Trim();

            return ToTitleCase(cleanedLocation);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private string ParseLocationBase(string message)
        {
            var crossStreetsReg = Language.RegularExpressions["locationCrossStreets"]; //new Regex(@"([a-zA-Z0-9]* (\&|and) [a-zA-Z0-9]*)", RegexOptions.IgnoreCase);

            if (crossStreetsReg.IsMatch(message))
            {
                var match = crossStreetsReg.Match(message);
                return match.Groups[1].Value;
            }


            var startReg = Language.RegularExpressions["locationStart"]; //new Regex("at ([a-zA-Z0-9 ]*)");//timespans should be removed already, so "at [blah blah]" should indicate location

            if (startReg.IsMatch(message))
                return startReg.Match(message).Groups[1].Value;


            var endReg = Language.RegularExpressions["locationEnd"]; //new Regex(@"([a-zA-Z0-9 ]*\b(park|school|church|museum|mural|statue) ?[a-zA-Z]*\b?)", RegexOptions.IgnoreCase);

            if (endReg.IsMatch(message))
                return endReg.Match(message).Groups[1].Value;

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

            loc1 = loc1.ToLowerInvariant();
            loc2 = loc2.ToLowerInvariant();

            if (loc1 == loc2 || loc1.StartsWith(loc2) || loc2.StartsWith(loc1)) return true;

            //Check if they used any abbreviations -- not language dependent
            var abbrReg1 = new Regex(loc1.Replace(" ", "[a-zA-Z]* ").Trim() + "[a-zA-Z]*");
            var abbrReg2 = new Regex(loc2.Replace(" ", "[a-zA-Z]* ").Trim() + "[a-zA-Z]*");

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
        public bool CompareLocationLatLong(KeyValuePair<double, double> ll1, KeyValuePair<double, double> ll2)
        {
            var distance = DistanceBetweenTwoPoints(ll1, ll2);
            return distance < latLongComparisonMaxMeters;
        }
        public int? ParseJoinedUsersCount(string message, out string messageout, out bool isMore, out bool isLess)
        {
            isLess = isMore = false;
            var endreg = Language.RegularExpressions["joinEnd"];

            if (endreg.IsMatch(message))
            {
                messageout = endreg.Replace(message, matchedWordReplacement);
                if (message.Contains(Language.Strings["questionMark"])) return null;

                var endmatch = endreg.Match(message);

                if (Language.RegularExpressions["joinLess"].IsMatch(message)) isLess = true;
                else if (Language.RegularExpressions["joinMore"].IsMatch(message)) isMore = true;

                var num = endmatch.Groups[1].Value;
                var result = 0;

                if (!int.TryParse(num, out result))
                    result = WordToInteger(num);

                if (result == -1) return null;
                return result;
            }

            var startReg = Language.RegularExpressions["joinStart"];
            if (startReg.IsMatch(message))
            {
                messageout = startReg.Replace(message, matchedWordReplacement);
                if (message.Contains(Language.Strings["questionMark"])) return null;

                var startmatch = startReg.Match(message);

                if (Language.RegularExpressions["joinLess"].IsMatch(message)) isLess = true;
                else if (Language.RegularExpressions["joinMore"].IsMatch(message)) isMore = true;

                var num = startmatch.Groups[2].Value;
                var result = 0;

                if (!int.TryParse(num, out result))
                    result = WordToInteger(num);

                if (result == -1) return null;
                return result;
            }

            var meReg = Language.RegularExpressions["joinMe"];
            if (meReg.IsMatch(message))
            {
                messageout = meReg.Replace(message, matchedWordReplacement);
                if (message.Contains(Language.Strings["questionMark"])) return null;
                return 1;
            }

            var moreReg = Language.RegularExpressions["joinMore"];
            if (moreReg.IsMatch(message))
            {
                messageout = moreReg.Replace(message, matchedWordReplacement);
                if (message.Contains(Language.Strings["questionMark"])) return null;

                var morematch = moreReg.Match(message);

                var num = morematch.Groups[2].Value;
                var result = 0;

                if (!int.TryParse(num, out result))
                    result = WordToInteger(num);

                isMore = true;
                return result;
            }

            var lessReg = Language.RegularExpressions["joinLess"];
            if (lessReg.IsMatch(message))
            {
                messageout = lessReg.Replace(message, matchedWordReplacement);
                if (message.Contains(Language.Strings["questionMark"])) return null;

                var lessmatch = lessReg.Match(message);

                var num = lessmatch.Groups[1].Value;
                var result = 0;

                if (!int.TryParse(num, out result))
                    result = WordToInteger(num);


                isLess = true;
                return result;
            }

            messageout = message;
            return null;
        }
        public async Task<KeyValuePair<double, double>?> GetLocationLatLong(string location, SocketGuildChannel channel, BotConfig config)
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

                return new KeyValuePair<double, double>(lat, lng);
            }
            else
            {
                return null;
            }
        }

        #region helpers
        private double DistanceBetweenTwoPoints(KeyValuePair<double, double> ll1, KeyValuePair<double, double> ll2)
        {
            var R = 6371e3; // metres
            var φ1 = ll1.Key / (180 / Math.PI);
            var φ2 = ll2.Key / (180 / Math.PI);
            var Δφ = (ll2.Key - ll1.Key) / (180 / Math.PI);
            var Δλ = (ll2.Value - ll1.Value) / (180 / Math.PI);

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

            return arr.IndexOf(str.ToLowerInvariant());
        }
        public string ToTitleCase(string str)
        {
            var result = new List<string>();
            var strs = str.Split(' ');
            foreach (var word in strs)
            {
                if (Language.RegularExpressions["smallWords"].IsMatch(word))
                    result.Add(word);
                else if (word.Length > 1)
                    result.Add(char.ToUpper(word[0]) + word.Substring(1));
                else
                    result.Add(word.ToUpperInvariant());
            }

            return string.Join(" ", result);
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
        private string GetFullLocation(string location, GuildConfig guildConfig, ulong channelId)
        {
            var city = guildConfig.ChannelCities.ContainsKey(channelId) ? guildConfig.ChannelCities[channelId] : guildConfig.City ?? "";
            if (!string.IsNullOrEmpty(city)) city = ", " + city;
            return Uri.EscapeDataString(location + city);
        }
        #endregion
        #endregion

        #region Output
        public string[] GetRaidHelpString(BotConfig config)
        {
            return GetFullHelpString(config, false);
        }
        public string[] GetFullHelpString(BotConfig config, bool admin)
        {
            var result = new List<string>();
            var helpheader = string.Format(string.Format("```\n{0}\n```", Language.Strings["helpTop"]), config.OutputChannel);

            if (!admin)
                helpheader = string.Format("```\n{0}\n```", Language.Strings["helpRaidTop"]);

            result.Add(helpheader);

            var helpmessage = string.Format("       #{0}:\n", Language.Strings["helpCommands"]);
            helpmessage += string.Format("  {0}(j)oin [id] [number] - {1}\n", config.Prefix, Language.Strings["helpJoin"]);
            helpmessage += string.Format("  {0}(un)join [id] - {1}\n", config.Prefix, Language.Strings["helpUnJoin"]);
            helpmessage += string.Format("  {0}(i)nfo [name] - {1}\n", config.Prefix, Language.Strings["helpInfo"]);
            helpmessage += string.Format("  {0}(d)elete [id] - {1}\n", config.Prefix, Language.Strings["helpDelete"]);
            helpmessage += string.Format("  {0}(m)erge [id1] [id2] - {1}\n", config.Prefix, Language.Strings["helpMerge"]);
            helpmessage += string.Format("  {0}(loc)ation [id] [new location] - {1}\n", config.Prefix, Language.Strings["helpLocation"]);
            helpmessage += string.Format("  {0}(h)elp - {1}\n", config.Prefix, Language.Strings["helpHelp"]);
            helpmessage += string.Format("       (){0}\n", Language.Strings["helpParenthesis"]);
            if (admin)
            {
                helpmessage += string.Format("       #{0}:\n", Language.Strings["helpAdminCommands"]);
                helpmessage += string.Format(string.Format("  {0}channel [name] - {1}\n", config.Prefix, Language.Strings["helpChannel"]), config.OutputChannel);
                helpmessage += string.Format("  {0}nochannel - {1}\n", config.Prefix, Language.Strings["helpNoChannel"]);
                helpmessage += string.Format("  {0}alias [pokemon] [alias] - {1}\n", config.Prefix, Language.Strings["helpAlias"]);
                helpmessage += string.Format("  {0}removealias [pokemon] [alias] - {1}\n", config.Prefix, Language.Strings["helpRemoveAlias"]);
                helpmessage += string.Format("  {0}pin [channel name] - {1}\n", config.Prefix, Language.Strings["helpPin"]);
                helpmessage += string.Format("  {0}unpin [channel name] - {1}\n", config.Prefix, Language.Strings["helpUnPin"]);
                helpmessage += string.Format("  {0}pinall - {1}\n", config.Prefix, Language.Strings["helpPinAll"]);
                helpmessage += string.Format("  {0}unpinall - {1}\n", config.Prefix, Language.Strings["helpUnPinAll"]);
                helpmessage += string.Format("  {0}pinlist - {1}\n", config.Prefix, Language.Strings["helpPinList"]);
                helpmessage += string.Format("  {0}mute [channel name] - {1}\n", config.Prefix, Language.Strings["helpMute"]);
                helpmessage += string.Format("  {0}unmute [channel name] - {1}\n", config.Prefix, Language.Strings["helpUnMute"]);
                helpmessage += string.Format("  {0}muteall - {1}\n", config.Prefix, Language.Strings["helpMuteAll"]);
                helpmessage += string.Format("  {0}unmuteall - {1}\n", config.Prefix, Language.Strings["helpUnMuteAll"]);
                helpmessage += string.Format("  {0}mutelist - {1}\n", config.Prefix, Language.Strings["helpMuteList"]);
                helpmessage += string.Format("  {0}timezone [gmt offset] - {1}\n", config.Prefix, Language.Strings["helpTimezone"]);
                helpmessage += string.Format("  {0}language [language] - {1}\n", config.Prefix, Language.Strings["helpLanguage"]);
                helpmessage += string.Format("  {0}city [city] - {1}\n", config.Prefix, Language.Strings["helpCity"]);
                helpmessage += string.Format("  {0}channelcity [channel name] [city] - {1}\n", config.Prefix, Language.Strings["helpChannelCity"]);
            }
            if (helpmessage.Length > 1990)//2000 is max, formatting strings add more
            {
                var length = 0;
                var helpmessages = new List<string>();
                var helpstring = "```css";
                var helpcommands = new List<string>(Regex.Split(helpmessage, @"\n"));


                foreach (var cmd in helpcommands)
                {
                    length += cmd.Length + 2;
                    if (length < 1990)
                    {
                        helpstring += "\n" + cmd;
                    }
                    else
                    {
                        helpmessages.Add(helpstring + "```");
                        length = 0;
                        helpstring = "```css";
                    }
                }
                result.AddRange(helpmessages);
            }
            else
                result.Add($"```css{helpmessage}\n```");

            return result.ToArray();
        }
        /// <summary>
        /// Returns a single row of pokemon info for the !info command.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public string MakeInfoLine(PokemonInfo info, BotConfig config, ulong guildId, int paddingSize = 0)
        {
            var lineFormat = Language.Formats["infoLine"];// "\n{0}: {7}Tier={1} BossCP={2:#,##0} MinCP={3:#,##0} MaxCP={4:#,##0} CatchRate={5}%{6}";
            var padding = 0;
            if (paddingSize > 0)
                padding = paddingSize - info.BossNameFormatted.Length;

            var allAliases = new List<string>(info.Aliases);

            if (config.GetGuildConfig(guildId).PokemonAliases.ContainsKey(info.Id))
                allAliases.AddRange(config.GetGuildConfig(guildId).PokemonAliases[info.Id]);

            return string.Format(lineFormat,
                info.BossNameFormatted,
                new String(' ', padding),
                info.Tier,
                info.BossCP.ToString() + (info.BossCP < 9999 ? " " : ""),
                info.MinCP.ToString() + (info.MinCP < 999 ? " " : ""),
                info.MaxCP.ToString() + (info.MaxCP < 999 ? " " : ""),
                info.CatchRate * 100,
                allAliases.Count() == 0 ? "" : Language.Strings["aliases"] + ": " + string.Join(",", allAliases)
                );
        }
        /// <summary>
        /// Creates the Discord.Embed object containing the message thread of the raid post
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        public Embed MakeResponseEmbed(PokemonRaidPost post, string header)
        {
            var builder = new EmbedBuilder();

            builder.WithColor(post.Color[0], post.Color[1], post.Color[2]);

            builder.WithDescription(header);
            builder.WithUrl(string.Format(Language.Formats["pokemonInfoLink"], post.PokemonId));
            
            builder.WithThumbnailUrl(string.Format(Language.Formats["imageUrlLargePokemon"], post.PokemonId));

            foreach (var message in post.Responses.OrderBy(x => x.MessageDate).Skip(Math.Max(0, post.Responses.Count() - 24)))//max fields is 25
            {
                builder.AddField(message.Username + ":", message.Content);
            }
            
            return builder.Build();
        }
        public void MakePostWithEmbed(PokemonRaidPost post, out Embed header, out Embed response, out string mentions)
        {
            var headerstring = MakePostHeader(post);
            response = MakeResponseEmbed(post, headerstring);
            header = MakeHeaderEmbed(post, headerstring);

            var joinedUserIds = post.JoinedUsers.Select(x => x.Id);
            var mentionUserIds = post.Responses.Select(x => x.UserId.ToString()).Distinct().ToList();

            mentionUserIds.AddRange(post.JoinedUsers.Select(x => x.Id.ToString()).Distinct());


            var channel = $"<#{post.FromChannelId}>";
            var users = mentionUserIds.Count() > 0 ? $",<@{string.Join(">,<@", mentionUserIds.Distinct())}>" : "";
            var roles = post.MentionedRoleIds.Count() > 0 ? $",<@&{string.Join(">,<@&", post.MentionedRoleIds.Distinct())}>" : "";

            mentions = channel + users + roles;
        }
        public Embed MakeHeaderEmbed(PokemonRaidPost post, string text = null)
        {
            if (string.IsNullOrEmpty(text)) text = MakePostHeader(post);
            var headerembed = new EmbedBuilder();
            headerembed.WithColor(post.Color[0], post.Color[1], post.Color[2]);
            headerembed.WithUrl(string.Format(Language.Formats["pokemonInfoLink"], post.PokemonId));
            headerembed.WithDescription(Language.RegularExpressions["discordChannel"].Replace(text, "").Replace(" in ", " ").Replace("  ", " "));

            headerembed.WithThumbnailUrl(string.Format(Language.Formats["imageUrlSmallPokemon"], post.PokemonId));
            return headerembed.Build();
        }

        public string MakePostHeader(PokemonRaidPost post)
        {
            var joinString = string.Join(",", post.JoinedUsers.Where(x => x.Count > 0).Select(x => string.Format("@{0}({1})", x.Name, x.Count)));
            //var roleString = string.Join(",", post.MentionedRoleIds.Where(x => x > 0).Select(x => string.Format("<@&{0}>", x)));

            var joinCount = post.JoinedUsers.Sum(x => x.Count);

            var location = post.Location;

            if (post.LatLong.HasValue) location = string.Format("[{0}]({1})", location, string.Format(Language.Formats["googleMapLink"], post.LatLong.Value.Key, post.LatLong.Value.Value));

            string response = string.Format(Language.Formats["postHeader"],
                post.UniqueId,
                string.Format("[{0}]({1})", post.PokemonName, string.Format(Language.Formats["pokemonInfoLink"], post.PokemonId)),
                !string.IsNullOrEmpty(location) ? string.Format(Language.Formats["postLocation"], location) : "",
                string.Format(Language.Formats["postEnds"], post.EndDate.AddHours(timeOffset)),
                joinCount > 0 ? string.Format(Language.Formats["postJoined"], joinCount, joinString) : Language.Strings["postNoneJoined"]
                );
            return response;
        }
        #endregion
    }
}
