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

namespace PokemonGoRaidBot.Parsing
{
    public class MessageParser
    {
        private static int colorIndex = 0;

        public ParserLanguage Language;
        private static string[] discordColors = new string[] { "css", "brainfuck", "fix", "apache", "" };
        private const string googleGeocodeApiUrl = "https://maps.googleapis.com/maps/api/geocode/json?address={0}&key={1}";

        private const int latLongComparisonMaxMeters = 30;
        private const int maxRaidMinutes = 100;
        private const string matchedWordReplacement = "#|#|#|#";//when trying to match location, replace pokemon names and time spans with this string
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
                Responses = new List<PokemonMessage>() { new PokemonMessage(message.Author.Id, message.Author.Username, message.Content, DateTime.Now) },
                EndDate = DateTime.Now + new TimeSpan(0, maxRaidMinutes, 0)
            };

            var guildId = ((SocketGuildChannel)message.Channel).Guild.Id;

            var messageString = message.Content;

            var words = messageString.Split(' ');
            //if (words.Length < 2) return null;

            var timespan = new TimeSpan();
            var i = 0;
            var nopost = false;

            var unmatchedWords = new List<string>();
            var isActualTime = false;
            foreach (var word in words)
            {
                i++;

                var garbageRegex = Language.RegularExpressions["garbage"];
                if (garbageRegex.IsMatch(word))
                {
                    unmatchedWords.Add(matchedWordReplacement);
                    continue;
                }

                if (result.PokemonId == default(int))
                {
                    var pokemon = ParsePokemon(word, config, guildId);
                    if(pokemon != null)
                    {
                        result.PokemonId = pokemon.Id;
                        result.PokemonName = pokemon.Name;
                        
                        if (i > 1 && Language.RegularExpressions["pokemonInvalidators"].IsMatch(words[i - 2]))//if previous word invalidates -- ex "any Snorlax?"
                            nopost = true;

                        unmatchedWords.Add(matchedWordReplacement);
                        continue;
                    }
                }

                var ts = ParseTimespan(word, ref isActualTime);
                if (ts.Ticks > 0)
                {
                    timespan = timespan.Add(ts);
                    unmatchedWords.Add(matchedWordReplacement);
                    continue;
                }

                if (Language.RegularExpressions["minuteAliases"].IsMatch(word) && i > 1)//go back and get the previous word
                {
                    var mins = words[i - 2];
                    var min = 0;
                    var isminute = false;

                    if (Int32.TryParse(mins, out min))
                    { 
                        timespan = timespan.Add(new TimeSpan(0, min, 0));
                        isminute = true;
                    }
                    else if (Language.RegularExpressions["aAliases"].IsMatch(mins))
                    { 
                        timespan = timespan.Add(new TimeSpan(0, 1, 0));
                        isminute = true;
                    }
                    if (isminute)
                    {
                        unmatchedWords[unmatchedWords.Count() - 1] = matchedWordReplacement;
                        unmatchedWords.Add(matchedWordReplacement);
                        continue;
                    }
                }

                if (Language.RegularExpressions["hourAliases"].IsMatch(word) && i > 1)//go back and get the previous word
                {
                    var hrs = words[i - 2];
                    var hr = 0;
                    var ishour = false;
                    if (Int32.TryParse(hrs, out hr))
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
                        unmatchedWords[unmatchedWords.Count() - 1] = matchedWordReplacement;
                        unmatchedWords.Add(matchedWordReplacement);
                        continue;
                    }
                }
                //the word was not matched, add it to the cleaned array to check for location

                if (Regex.IsMatch(word, "^at$", RegexOptions.IgnoreCase)) isActualTime = true;

                unmatchedWords.Add(word);
            }

            //post was invalidated
            if (nopost) result.PokemonId = default(int);

            if (timespan.Ticks > 0)
            {
                var dt = result.PostDate + timespan;

                if(!isActualTime)
                    result.Responses[0].Content += string.Format(" ({0:h:mmtt})", dt.AddHours(timeOffset));

                if (!Regex.IsMatch(messageString, @"\b((there|arrive) in|away|my way|omw|out)\b", RegexOptions.IgnoreCase))
                {
                    result.EndDate = dt;
                    result.HasEndDate = true;
                }
            }

            var unmatchedString = string.Join(" ", unmatchedWords.ToArray());
            var newUnmatchedString = "";

            var joinCount = ParseJoinedUsersCount(unmatchedString, out newUnmatchedString);

            if (joinCount.HasValue)
                result.JoinedUsers[message.Author.Id] = joinCount.Value;

            result.Location = ParseLocation(newUnmatchedString);

            if (!string.IsNullOrEmpty(result.Location))
            {
                result.LatLong = await GetLocationLatLong(result.Location, (SocketGuildChannel)message.Channel, config);
            }

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

            var cleanedName = Regex.Replace(name, @"\W", "").ToLowerInvariant();

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

                if(part.Equals("p", StringComparison.OrdinalIgnoreCase))
                    hour += 12;

                var postedDate = DateTime.Today.Add(TimeSpan.Parse(string.Format("{0}:{1}", hour, minute + 1)));
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

                if(!isActualTime)
                    return new TimeSpan(hour, minute, 0);
                else
                {
                    if (DateTime.Now.Hour > 9 && hour < 9) hour += 12;//PM is inferred
                    var postedDate = DateTime.Today.Add(TimeSpan.Parse(string.Format("{0}:{1}", hour, minute +1)));
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
                

            var atReg = Language.RegularExpressions["locationAt"]; //new Regex("at ([a-zA-Z0-9 ]*)");//timespans should be removed already, so "at [blah blah]" should indicate location

            if (atReg.IsMatch(message))
                return atReg.Match(message).Groups[1].Value;


            var parkReg = Language.RegularExpressions["locationLandmark"]; //new Regex(@"([a-zA-Z0-9 ]*\b(park|school|church|museum|mural|statue) ?[a-zA-Z]*\b?)", RegexOptions.IgnoreCase);

            if (parkReg.IsMatch(message))
                return parkReg.Match(message).Groups[1].Value;
            
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
            if(crossStreetsReg.IsMatch(loc1) && crossStreetsReg.IsMatch(loc2))
            {
                var match = crossStreetsReg.Match(loc2);
                return CompareLocationStrings(loc1, string.Format("{0} {1} {2}", match.Groups[4].Value, match.Groups[3].Value, match.Groups[2].Value), true);
            }

            return false;
        }
        public bool CompareLocationLatLong(KeyValuePair<double,double> ll1, KeyValuePair<double,double> ll2)
        {
            var distance = DistanceBetweenTwoPoints(ll1, ll2);
            return distance < latLongComparisonMaxMeters;
        }
        public int? ParseJoinedUsersCount(string message, out string messageout)
        {
            var endreg = Language.RegularExpressions["joinEnd"];//new Regex(@"([0-9]{1,2}) (people|here|on (the|our))", RegexOptions.IgnoreCase);

            if (endreg.IsMatch(message))
            {
                var endmatch = endreg.Match(message);
                messageout = endreg.Replace(message, matchedWordReplacement);

                if (message.Contains("?")) return null;

                return Convert.ToInt32(endmatch.Groups[1].Value);
            }

            var startReg = Language.RegularExpressions["joinStart"];//new Regex(@"(have|are|there's) ([0-9]{1,2})", RegexOptions.IgnoreCase);
            if (startReg.IsMatch(message))
            {
                var startmatch = endreg.Match(message);
                messageout = endreg.Replace(message, matchedWordReplacement);

                if (message.Contains("?")) return null;

                return Convert.ToInt32(startmatch.Groups[2].Value);
            }

            var meReg = Language.RegularExpressions["joinMe"];
            if (meReg.IsMatch(message))
            {
                messageout = meReg.Replace(message, matchedWordReplacement);
                return 1;
            }

            messageout = message;
            return null;
        }
        public double DistanceBetweenTwoPoints(KeyValuePair<double, double> ll1, KeyValuePair<double, double> ll2)
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
        public async Task<KeyValuePair<double, double>?> GetLocationLatLong(string location, SocketGuildChannel channel, BotConfig config)
        {
            if (string.IsNullOrEmpty(config.GoogleApiKey)) return null;
            var guildConfig = config.GetGuildConfig(channel.Guild.Id);
            var city = guildConfig.ChannelCities.ContainsKey(channel.Id) ? guildConfig.ChannelCities[channel.Id] : guildConfig.City ?? "";
            if (!string.IsNullOrEmpty(city)) city = ", " + city;
            var search = Uri.EscapeDataString(location + city);

            var url = string.Format(googleGeocodeApiUrl, search, config.GoogleApiKey);

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
                //throw new Exception(string.Format("Invalid response from google geocoding api ({0})", url));
            }
        }


        private static string ToTitleCase(string str)
        {
            if (str.Length > 1)
                return char.ToUpper(str[0]) + str.Substring(1);

            return str.ToUpper();
        }
        #endregion

        #region Output
        public string[] GetRaidHelpString(BotConfig config)
        {
            return GetFullHelpString(config, false);
        }
        public string[] GetFullHelpString(BotConfig config, bool admin)
        {
            var result = new List<string>();
            var helpmessage = string.Format(string.Format("```{0}\n\n", Language.Strings["helpTop"]), config.OutputChannel);

            if (!admin)
                helpmessage = string.Format("```{0}\n\n", Language.Strings["helpRaidTop"]);

            helpmessage += string.Format("``````css\n       #{0}:\n", Language.Strings["helpCommands"]);
            helpmessage += string.Format("  {0}(j)oin [id] [number] - {1}\n", config.Prefix, Language.Strings["helpJoin"]);
            helpmessage += string.Format("  {0}(un)join [id] - {1}\n", config.Prefix, Language.Strings["helpUnJoin"]);
            helpmessage += string.Format("  {0}(i)nfo [name] - {1}\n", config.Prefix, Language.Strings["helpInfo"]);
            helpmessage += string.Format("  {0}(d)elete [id] - *{1}\n", config.Prefix, Language.Strings["helpDelete"]);
            helpmessage += string.Format("  {0}(m)erge [id1] [id2] - *{1}\n", config.Prefix, Language.Strings["helpMerge"]);
            helpmessage += string.Format("  {0}(h)elp - {1}\n", config.Prefix, Language.Strings["helpHelp"]);
            helpmessage += string.Format("       (){0}\n", Language.Strings["helpParenthesis"]);
            if (admin)
            {
                result.Add(helpmessage + "```");//getting too long! 2000 char max
                helpmessage = string.Format("```css\n       #{0}:\n", Language.Strings["helpAdminCommands"]);
                helpmessage += string.Format(string.Format("  {0}channel [name] - {1}\n", config.Prefix, Language.Strings["helpChannel"]), config.OutputChannel);
                helpmessage += string.Format("  {0}nochannel - {1}\n", config.Prefix, Language.Strings["helpNoChannel"]);
                helpmessage += string.Format("  {0}alias [pokemon] [alias] - {1}\n", config.Prefix, Language.Strings["helpAlias"]);
                helpmessage += string.Format("  {0}removealias [pokemon] [alias] - {1}\n", config.Prefix, Language.Strings["helpRemoveAlias"]);
                helpmessage += string.Format("  {0}pin [channel name] - {1}\n", config.Prefix, Language.Strings["helpPin"]);
                helpmessage += string.Format("  {0}unpin [channel name] - {1}\n", config.Prefix, Language.Strings["helpUnPin"]);
                helpmessage += string.Format("  {0}pinall - {1}\n", config.Prefix, Language.Strings["helpPinAll"]);
                helpmessage += string.Format("  {0}unpinall - {1}\n", config.Prefix, Language.Strings["helpUnPinAll"]);
                helpmessage += string.Format("  {0}timezone [gmt offset] - {1}\n", config.Prefix, Language.Strings["helpTimezone"]);
                helpmessage += string.Format("  {0}language [language] - {1}\n", config.Prefix, Language.Strings["helpLanguage"]);
                helpmessage += string.Format("  {0}city [city] - {1}\n", config.Prefix, Language.Strings["helpCity"]);
                helpmessage += string.Format("  {0}channelcity [channel name] [city] - {1}\n", config.Prefix, Language.Strings["helpChannelCity"]);
            }
            helpmessage += "```";
            result.Add(helpmessage);
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

            if(config.GetGuildConfig(guildId).PokemonAliases.ContainsKey(info.Id))
                allAliases.AddRange(config.GetGuildConfig(guildId).PokemonAliases[info.Id]);

            return string.Format(lineFormat,
                info.BossNameFormatted,
                new String(' ', padding),
                info.Tier, 
                info.BossCP, 
                info.MinCP, 
                info.MaxCP,
                info.CatchRate * 100,
                allAliases.Count() == 0 ? "" : Language.Strings["aliases"] + string.Join(",", allAliases)
                );
        }
        /// <summary>
        /// Creates the string that contains user resposes to a raid post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        public string[] MakeResponseStrings(PokemonRaidPost post, string startMessage)
        {
            List<string> resultList = new List<string>();
            int i = 1, maxLength = 2000;//, firstMaxLength = maxLength - startMessage.Length;

            resultList.Add(startMessage);
            resultList.Add($"```{post.DiscordColor ?? (post.DiscordColor = discordColors[colorIndex >= discordColors.Length - 1 ? (colorIndex = 0) : colorIndex++])}");

            foreach (var message in post.Responses.OrderBy(x => x.MessageDate))
            {
                var messageString = $"\n   #{message.Username}:  {Regex.Replace(message.Content, @"<(@|#)[0-9]*>", "").TrimStart()}";

                if (resultList[i].Length + messageString.Length > maxLength)
                {
                    resultList[i] += "```";
                    resultList.Add("```" + post.DiscordColor);
                    i++;
                }

                resultList[i] += messageString;
            }

            resultList[i] += "\n```";
            return resultList.ToArray();
        }
        public string[] MakePostStrings(PokemonRaidPost post)
        {
            var response = MakePostHeader(post);

            //if (post.Pin) return new string[] { response };

            return MakeResponseStrings(post, response);
        }
        public string MakePostHeader(PokemonRaidPost post)
        {
            var joinString = "";

            foreach (var user in post.JoinedUsers.Where(x => x.Value > 0))
                joinString += string.Format("<@{0}>({1})", user.Key, user.Value);

            var joinCount = post.JoinedUsers.Sum(x => x.Value);

            string response = string.Format(Language.Formats["postHeader"],
                post.UniqueId,
                post.PokemonName,
                post.FromChannelId,
                !string.IsNullOrEmpty(post.Location) ? string.Format(Language.Formats["postLocation"], post.Location) : "",
                string.Format(Language.Formats["postEnds"], post.EndDate.AddHours(timeOffset)),
                post.LatLong.HasValue ? string.Format("https://www.google.com/maps/dir/Current+Location/{0},{1}\n", post.LatLong.Value.Key, post.LatLong.Value.Value) : "",
                joinCount > 0 ? string.Format(Language.Formats["postJoined"], joinCount, joinString) : string.Format(Language.Formats["postNoneJoined"], post.UserId)
                );
            return response;
        }
        #endregion
    }
}
