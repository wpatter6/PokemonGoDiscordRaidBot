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

namespace PokemonGoRaidBot.Parsing
{
    public class MessageParser
    {//TODO Remove all parsing related strings and place in config file so other languages possible
        private static int colorIndex = 0;

        public ParserLanguage Language;
        private static string[] discordColors = new string[] { "css", "brainfuck", "fix", "apache", "" };

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
        public PokemonRaidPost ParsePost(SocketMessage message, BotConfig config)
        {
            var result = new PokemonRaidPost()
            {
                User = message.Author.Username,
                UserId = message.Author.Id,
                PostDate = DateTime.Now,//uses local time for bot
                FromChannel = message.Channel,
                Responses = new List<PokemonMessage>() { new PokemonMessage(message.Author.Id, message.Author.Username, message.Content, DateTime.Now) },
                EndDate = DateTime.Now + new TimeSpan(0, maxRaidMinutes, 0)
            };

            var guildId = ((SocketGuildChannel)message.Channel).Guild.Id;

            var messageString = message.Content;

            var words = messageString.Split(' ');
            //if (words.Length < 2) return null;

            var timespan = new TimeSpan();
            var i = 0;

            var unmatchedWords = new List<string>();
            var isActualTime = false;
            foreach (var word in words)
            {
                i++;

                if (result.Pokemon == null)
                {
                    result.Pokemon = ParsePokemon(word, config, guildId);
                    if (result.Pokemon != null)
                    {
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

                if (Language.MinuteAliases.Contains(word) && i > 1)//go back and get the previous word
                {
                    var mins = words[i - 2];
                    var min = 0;
                    var isminute = false;

                    if (Int32.TryParse(mins, out min))
                    { 
                        timespan = timespan.Add(new TimeSpan(0, min, 0));
                        isminute = true;
                    }
                    else if (mins.ToLowerInvariant() == "a" || mins.ToLowerInvariant() == "an")
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

                if (Language.HourAliases.Contains(word) && i > 1)//go back and get the previous word
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

            var result = config.PokemonInfoList.FirstOrDefault(x => x.ServerAliases.Where(xx => xx.Key == guildId && xx.Value == cleanedName).Count() > 0);
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

            return Language.RegularExpressions["locationExcludeWords"].Replace(result, "").Replace(",", "").Replace(".", "").Replace("  ", " ").Replace(matchedWordReplacement, "").Trim();
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
                return crossStreetsReg.Match(message).Groups[1].Value;

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
        public bool CompareLocations(string loc1, string loc2, bool triedCrossStreets = false)
        {
            loc1 = loc1.ToLowerInvariant();
            loc2 = loc2.ToLowerInvariant();

            if (loc1 == loc2 || loc1.StartsWith(loc2) || loc2.StartsWith(loc1)) return true;

            //Check if they used any abbreviations -- not language dependent
            var abbrReg1 = new Regex(loc1.Replace(" ", "[a-zA-Z]* ").Trim() + "[a-zA-Z]*");
            var abbrReg2 = new Regex(loc2.Replace(" ", "[a-zA-Z]* ").Trim() + "[a-zA-Z]*");

            if (abbrReg1.IsMatch(loc2)) return true;
            if (abbrReg2.IsMatch(loc1)) return true;

            if (triedCrossStreets) return false;

            var crossStreetsReg = Language.RegularExpressions["crossStreets"];//new Regex(@"([a-zA-Z0-9]*) (\&|and) ([a-zA-Z0-9]*)", RegexOptions.IgnoreCase);
            if(crossStreetsReg.IsMatch(loc1) && crossStreetsReg.IsMatch(loc2))
            {
                var match = crossStreetsReg.Match(loc2);
                return CompareLocations(loc1, string.Format("{0} {1} {2}", match.Groups[3].Value, match.Groups[2].Value, match.Groups[1].Value), true);
            }

            return false;
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
            messageout = message;
            return null;
        }
        #endregion

        #region Output
        public string GetHelpString(BotConfig config)
        {
            var helpmessage = string.Format(string.Format("```{0}\n\n", Language.Strings["helpTop"]), config.OutputChannel);
            helpmessage += string.Format("``````css\n       #{0}:\n", Language.Strings["helpCommands"]);
            helpmessage += string.Format("  {0}join [id] [number] - {1}\n", config.Prefix, Language.Strings["helpJoin"]);
            helpmessage += string.Format("  {0}unjoin [id] - {1}\n", config.Prefix, Language.Strings["helpUnJoin"]);
            helpmessage += string.Format("  {0}info [name] - {1}\n", config.Prefix, Language.Strings["helpInfo"]);
            helpmessage += string.Format(string.Format("  {0}channel [name] - *{1}\n", config.Prefix, Language.Strings["helpChannel"]), config.OutputChannel);
            helpmessage += string.Format("  {0}nochannel - *{1}\n", config.Prefix, Language.Strings["helpNoChannel"]);
            helpmessage += string.Format("  {0}alias [pokemon] [alias] - *{1}\n", config.Prefix, Language.Strings["helpAlias"]);
            helpmessage += string.Format("  {0}removealias [pokemon] [alias] - *{1}\n", config.Prefix, Language.Strings["helpRemoveAlias"]);
            helpmessage += string.Format("  {0}delete [id] - *{1}\n", config.Prefix, Language.Strings["helpDelete"]);
            helpmessage += string.Format("  {0}merge [id1] [id2] - *{1}\n", config.Prefix, Language.Strings["helpMerge"]);
            helpmessage += string.Format("  {0}pin [channel name] - *{1}\n", config.Prefix, Language.Strings["helpPin"]);
            helpmessage += string.Format("  {0}unpin [channel name] - *{1}\n", config.Prefix, Language.Strings["helpUnPin"]);
            helpmessage += string.Format("  {0}pinall - *{1}\n", config.Prefix, Language.Strings["helpPinAll"]);
            helpmessage += string.Format("  {0}unpinall - *{1}\n", config.Prefix, Language.Strings["helpUnPinAll"]);
            helpmessage += string.Format("  {0}timezone [gmt offset] - *{1}\n", config.Prefix, Language.Strings["helpTimezone"]);
            helpmessage += string.Format("  {0}language [language] - *{1}\n", config.Prefix, Language.Strings["helpLanguage"]);
            //helpmessage += string.Format("  {{0}}timezone - TODO Configure server GMT offset and apply it everywhere times are determined";
            helpmessage += string.Format("  {0}help - {1}\n", config.Prefix, Language.Strings["helpHelp"]);
            helpmessage += string.Format("        *{0}", Language.Strings["helpAdmin"]);
            helpmessage += "```";
            return helpmessage;
        }
        /// <summary>
        /// Returns a single row of pokemon info for the !info command.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public string MakeInfoLine(PokemonInfo info, ulong guildId, int paddingSize = 0)
        {
            var lineFormat = Language.Formats["infoLine"];// "\n{0}: {7}Tier={1} BossCP={2:#,##0} MinCP={3:#,##0} MaxCP={4:#,##0} CatchRate={5}%{6}";
            var padding = 0;
            if (paddingSize > 0)
                padding = paddingSize - info.BossNameFormatted.Length;

            var allAliases = new List<string>(info.Aliases);
            allAliases.AddRange(info.ServerAliases.Where(x => x.Key == guildId).Select(x => x.Value));

            return string.Format(lineFormat,
                new String(' ', padding),
                info.BossNameFormatted, 
                info.Tier, 
                info.BossCP, 
                info.MinCP, 
                info.MaxCP,
                info.CatchRate * 100,
                allAliases.Count() == 0 ? "" : Language.Strings["aliases"] + string.Join(",", allAliases),
                "https://pokemongo.gamepress.gg/pokemon/"+info.Id.ToString());//TODO MOVE THIS URL TO CONFIG
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
                post.Pokemon.Name,
                post.FromChannel.Id,
                !string.IsNullOrEmpty(post.Location) ? string.Format(Language.Formats["postLocation"], post.Location) : "",
                string.Format(Language.Formats["postEnds"], post.EndDate.AddHours(timeOffset)),
                joinCount > 0 ? string.Format(Language.Formats["postJoined"], joinCount, joinString) : string.Format(Language.Formats["postNoneJoined"], post.UserId)
                );
            return response;
        }
        #endregion
    }
}
