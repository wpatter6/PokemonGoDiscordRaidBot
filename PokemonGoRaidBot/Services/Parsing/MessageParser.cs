using Discord.WebSocket;
using PokemonGoRaidBot.Configuration;
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
using System.Globalization;

namespace PokemonGoRaidBot.Services.Parsing
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

        public string Lang;
        public int TimeOffset;

        public MessageParser(string language = "en-us", int timeZoneOffset = 0)
        {
            Lang = language;
            Language = new ParserLanguage(language);
            TimeOffset = timeZoneOffset;
            CultureInfo.CurrentCulture = new CultureInfo(language);
        }

        #region Input
        /// <summary>
        /// Attempts to parse the necessary information out of a message to create a raid post.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="config"></param>
        /// <returns>If return value is null, or property 'Pokemon' is null, raid post is invalid.</returns>
        public PokemonRaidPost ParsePost(SocketMessage message, BotConfiguration config)
        {
            var guild = ((SocketGuildChannel)message.Channel).Guild;
            var guildConfig = config.GetGuildConfig(guild.Id);

            var result = new PokemonRaidPost()
            {
                GuildId = guild.Id,
                PostDate = DateTime.Now,
                UserId = message.Author.Id,
                Color = GetRandomColorRGB(),
                LastMessageDate = DateTime.Now,
                ChannelMessages = new Dictionary<ulong, PokemonRaidPostOwner>(),
                EndDate = DateTime.Now + new TimeSpan(0, maxRaidMinutes, 0),
                MentionedRoleIds = new List<ulong>()
            };

            result.ChannelMessages[message.Channel.Id] = new PokemonRaidPostOwner(default(ulong), message.Author.Id);

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
                    cleanedword = message.MentionedUsers.FirstOrDefault(x => x.Id == userId)?.Username ?? cleanedword;
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
                    var pokemon = ParsePokemon(cleanedword, config, guild.Id);
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
            ParseTimespanFull(ref unmatchedString, out raidTimeSpan, out joinTimeSpan);

            DateTime? joinTime = null;

            if (joinTimeSpan.HasValue) joinTime = DateTime.Now + joinTimeSpan.Value;
            if (raidTimeSpan.HasValue)
            {
                result.EndDate = DateTime.Now + raidTimeSpan.Value;
                result.HasEndDate = true;
            }
            
            bool isMore, isLess;

            var joinCount = ParseJoinedUsersCount(ref unmatchedString, out isMore, out isLess);

            if (!joinCount.HasValue && joinTime.HasValue) joinCount = 1;

            if (joinCount.HasValue)
                result.JoinedUsers.Add(new PokemonRaidJoinedUser(message.Author.Id, guild.Id, result.UniqueId, message.Author.Username, joinCount.Value, isMore, isLess, joinTime));

            if (latLong.HasValue)
            {
                result.Location = latLong.ToString();
                result.LatLong = latLong;
            }
            else
            {
                result.Location = ParseLocation(unmatchedString, guildConfig);

                if (!string.IsNullOrEmpty(result.Location))
                    result.FullLocation = GetFullLocation(result.Location, guildConfig, message.Channel.Id);
            }

            result.Responses.Add(new PokemonMessage(message.Author.Id, message.Author.Username, messageString, DateTime.Now, message.Channel.Name));

            var mention = ((SocketGuildChannel)message.Channel).Guild.Roles.FirstOrDefault(x => x.Name == result.PokemonName);
            if (mention != default(SocketRole) && !message.MentionedRoles.Contains(mention))//avoid double notification
                result.MentionedRoleIds.Add(mention.Id);

            foreach (var role in guild.Roles.Where(x => x.IsMentionable && !message.MentionedRoles.Contains(x) && !result.MentionedRoleIds.Contains(x.Id)))
            {//notify for roles that weren't actually mentions
                var reg = new Regex($@"\b{role.Name}\b", RegexOptions.IgnoreCase);
                if (reg.IsMatch(message.Content))
                    result.MentionedRoleIds.Add(role.Id);
            }

            result.IsValid = result.PokemonId != default(int) && (!string.IsNullOrWhiteSpace(result.Location) || result.JoinedUsers.Count() > 0);

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
        public PokemonInfo ParsePokemon(string name, BotConfiguration config, ulong guildId)
        {

            var cleanedName = Regex.Replace(name, @"\W", "").ToLower();//never want any special characters in string

            if (cleanedName.Length < 3 || Language.RegularExpressions["pokemonTooShort"].IsMatch(name)) return null;

            var guildConfig = config.GetGuildConfig(guildId);

            var result = Language.Pokemon.FirstOrDefault(x => guildConfig.PokemonAliases.Where(xx => xx.Value.Contains(name.ToLower())).Count() > 0);
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
        public void ParseTimespanFull(ref string message, out TimeSpan? raidTimeSpan, out TimeSpan? joinTimeSpan)
        {
            raidTimeSpan = null;
            joinTimeSpan = null;

            if (string.IsNullOrEmpty(message)) return;

            var joinReg = Language.RegularExpressions["joinTime"];
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

                    if (part.First() == 'p') hour += 12;

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
                    else
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
                        else
                            raidTimeSpan = new TimeSpan(0, min, 0);
                    }
                }
            }

        }

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
        public string ParseLocation(string message, GuildConfiguration guildConfig)
        {
            var cleanedMessage = Language.RegularExpressions["locationExcludeWords"].Replace(message, matchedWordReplacement).ToLower();

            foreach(var place in guildConfig.Places)
            {
                var reg = new Regex($@"\b{place.Key}\b");
                if (reg.IsMatch(cleanedMessage))
                    return place.Key;
            }

            var result = ParseLocationBase(cleanedMessage);

            var cleanreg = Language.RegularExpressions["locationCleanWords"];
            var cleaned = cleanreg.Replace(result.Trim(), "", 1);

            var cleanedLocation = cleaned.Replace(", ", "").Replace(".", "").Replace("  ", " ").Replace(matchedWordReplacement, "").Trim();

            return ToTitleCase(cleanedLocation);
        }
        public PokemonRaidPost ParsePostFromPostMessage(string message, GuildConfiguration config)
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

            var crossStreetsReg = Language.RegularExpressions["locationCrossStreets"]; //pretty reliable but can have some missed matches
            if (crossStreetsReg.IsMatch(message))
            {
                var groups = crossStreetsReg.Match(message).Groups;

                return groups[1].Value.Replace($" {groups[3].Value} ", $" {Language.Strings["and"]} ");
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
        public async Task<GeoCoordinate> GetLocationLatLong(string location, SocketGuildChannel channel, BotConfiguration config)
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
        public string GetFullLocation(string location, GuildConfiguration guildConfig, ulong channelId)
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
            var result = new List<string>();
            var strs = str.Split(' ');
            var i = 0;
            foreach (var word in strs)
            {
                if (i > 0 && Language.RegularExpressions["smallWords"].IsMatch(word))
                    result.Add(word);
                else if (word.Length > 1)
                    result.Add(char.ToUpper(word[0]) + word.Substring(1));
                else
                    result.Add(word.ToUpperInvariant());

                i++;
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
        #endregion
        #endregion

        #region Output
        //public string[] GetRaidHelpString(BotConfig config)
        //{
        //    return GetFullHelpString(config, false);
        //}
        public Embed GetHelpEmbed(BotConfiguration config, bool admin)
        {
            var embed = new EmbedBuilder();

            string info = $"*{Language.Strings["helpParenthesis"]}*";
            if (admin) info += $"\n\\**{Language.Strings["helpAdmin"]}*";

            embed.AddField($"__**{Language.Strings["helpCommands"]}**__", info);

            embed.AddField(string.Format("{0}(r)aid [pokemon] [time left] [location]", config.Prefix), Language.Strings["helpRaid"]);
            embed.AddField(string.Format("{0}(j)oin [raid] [number]", config.Prefix), Language.Strings["helpJoin"]);
            embed.AddField(string.Format("{0}(un)join [raid]", config.Prefix), Language.Strings["helpUnJoin"]);
            embed.AddField(string.Format("{0}(d)elete [raid id]", config.Prefix), Language.Strings["helpDelete"]);
            embed.AddField(string.Format("{0}(m)erge [raid1] [raid2]", config.Prefix), Language.Strings["helpMerge"]);
            embed.AddField(string.Format("{0}(loc)ation [raid] [new location]", config.Prefix), Language.Strings["helpLocation"]);
            embed.AddField(string.Format("{0}(i)nfo [name]", config.Prefix), Language.Strings["helpInfo"]);
            embed.AddField(string.Format("{0}(h)elp", config.Prefix), Language.Strings["helpHelp"]);

            if (admin)
            {
                embed.AddField(string.Format("*{0}channel [name]", config.Prefix), string.Format(Language.Strings["helpChannel"], config.OutputChannel));
                embed.AddField(string.Format("*{0}nochannel", config.Prefix), Language.Strings["helpNoChannel"]);
                embed.AddField(string.Format("*{0}alias [pokemon] [alias]", config.Prefix), Language.Strings["helpAlias"]);
                embed.AddField(string.Format("*{0}removealias [pokemon] [alias]", config.Prefix), Language.Strings["helpRemoveAlias"]);
                embed.AddField(string.Format("*{0}pin [channel name]", config.Prefix), Language.Strings["helpPin"]);
                embed.AddField(string.Format("*{0}unpin [channel name]", config.Prefix), Language.Strings["helpUnPin"]);
                embed.AddField(string.Format("*{0}pinall", config.Prefix), Language.Strings["helpPinAll"]);
                embed.AddField(string.Format("*{0}unpinall", config.Prefix), Language.Strings["helpUnPinAll"]);
                embed.AddField(string.Format("*{0}pinlist", config.Prefix), Language.Strings["helpPinList"]);
                embed.AddField(string.Format("*{0}mute [channel name]", config.Prefix), Language.Strings["helpMute"]);
                embed.AddField(string.Format("*{0}unmute [channel name]", config.Prefix), Language.Strings["helpUnMute"]);
                embed.AddField(string.Format("*{0}muteall", config.Prefix), Language.Strings["helpMuteAll"]);
                embed.AddField(string.Format("*{0}unmuteall", config.Prefix), Language.Strings["helpUnMuteAll"]);
                embed.AddField(string.Format("*{0}mutelist", config.Prefix), Language.Strings["helpMuteList"]);
                embed.AddField(string.Format("*{0}timezone [gmt offset]", config.Prefix), Language.Strings["helpTimezone"]);
                embed.AddField(string.Format("*{0}language [language]", config.Prefix), Language.Strings["helpLanguage"]);
                embed.AddField(string.Format("*{0}city [city]", config.Prefix), Language.Strings["helpCity"]);
                embed.AddField(string.Format("*{0}channelcity [channel name] [city]", config.Prefix), Language.Strings["helpChannelCity"]);
                embed.AddField(string.Format("*{0}cities", config.Prefix), Language.Strings["helpCities"]);
                embed.AddField(string.Format("*{0}place", config.Prefix), Language.Strings["helpPlace"]);
                embed.AddField(string.Format("*{0}deleteplace", config.Prefix), Language.Strings["helpDeletePlace"]);
                embed.AddField(string.Format("*{0}places", config.Prefix), Language.Strings["helpPlaces"]);
            }

            return embed.Build();
        }
        /// <summary>
        /// Returns a single row of pokemon info for the !info command.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public string MakeInfoLine(PokemonInfo info, BotConfiguration config, ulong guildId, int paddingSize = 0)
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
        public Embed MakeResponseEmbed(PokemonRaidPost post, GuildConfiguration guildConfig, string header)
        {
            var builder = new EmbedBuilder();

            builder.WithColor(post.Color[0], post.Color[1], post.Color[2]);

            builder.WithDescription(header);
            builder.WithUrl(string.Format(Language.Formats["pokemonInfoLink"], post.PokemonId));
            
            builder.WithThumbnailUrl(string.Format(Language.Formats["imageUrlLargePokemon"], post.PokemonId.ToString().PadLeft(3, '0')));

            foreach (var message in post.Responses.OrderBy(x => x.MessageDate).Skip(Math.Max(0, post.Responses.Count() - 10)))//max fields is 25
            {
                builder.AddField(string.Format(Language.Formats["responseInfo"], message.MessageDate.AddHours(TimeOffset), message.ChannelName, message.Username), message.Content);
            }
            
            return builder.Build();
        }
        /// <summary>
        /// Creates the post itself for all possible channel outputs
        /// </summary>
        /// <param name="post"></param>
        /// <param name="header"></param>
        /// <param name="response"></param>
        /// <param name="channel"></param>
        /// <param name="mentions"></param>
        public void MakePostWithEmbed(PokemonRaidPost post, GuildConfiguration guildConfig, out Embed header, out Embed response, out string channel, out string mentions)
        {
            var headerstring = MakePostHeader(post);
            response = MakeResponseEmbed(post, guildConfig, headerstring);
            header = MakeHeaderEmbed(post, headerstring);

            var joinedUserIds = post.JoinedUsers.Select(x => x.Id);
            var mentionUserIds = post.Responses.Select(x => x.UserId.ToString()).Distinct().ToList();

            mentionUserIds.AddRange(post.JoinedUsers.Select(x => x.Id.ToString()).Distinct());


            channel = $"<#{string.Join(">, <#", post.ChannelMessages.Keys)}>";
            //var users = mentionUserIds.Count() > 0 ? $",<@{string.Join(">,<@", mentionUserIds.Distinct())}>" : "";
            mentions = post.MentionedRoleIds.Count() > 0 ? $"<@&{string.Join(">, <@&", post.MentionedRoleIds.Distinct())}>" : "";

            //mentions = channel +/* users +*/ roles;
        }
        /// <summary>
        /// Makes the header embed
        /// </summary>
        /// <param name="post"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public Embed MakeHeaderEmbed(PokemonRaidPost post, string text = null)
        {
            if (string.IsNullOrEmpty(text)) text = MakePostHeader(post);
            var headerembed = new EmbedBuilder();
            headerembed.WithColor(post.Color[0], post.Color[1], post.Color[2]);
            headerembed.WithUrl(string.Format(Language.Formats["pokemonInfoLink"], post.PokemonId));
            headerembed.WithDescription(Language.RegularExpressions["discordChannel"].Replace(text, "").Replace(" in ", " ").Replace("  ", " "));
            
            headerembed.WithThumbnailUrl(string.Format(Language.Formats["imageUrlSmallPokemon"], post.PokemonId.ToString().PadLeft(3, '0')));
            
            return headerembed.Build();
        }
        public string MakePostHeader(PokemonRaidPost post)
        {
            var joinString = string.Join(", ", post.JoinedUsers.Where(x => x.PeopleCount > 0).Select(x => string.Format("@{0}(**{1}**{2})", x.Name, x.PeopleCount, x.ArriveTime.HasValue ? $" *@{x.ArriveTime.Value.ToString("h:mmt")}*" : "")));
            //var roleString = string.Join(",", post.MentionedRoleIds.Where(x => x > 0).Select(x => string.Format("<@&{0}>", x)));

            var joinCount = post.JoinedUsers.Sum(x => x.PeopleCount);

            var location = post.Location;

            if (post.LatLong != null && post.LatLong.HasValue) location = string.Format("[{0}]({1})", location, string.Format(Language.Formats["googleMapLink"], post.LatLong.Latitude, post.LatLong.Longitude));

            string response = string.Format(Language.Formats["postHeader"],
                post.UniqueId,
                string.Format("[{0}]({1})", post.PokemonName, string.Format(Language.Formats["pokemonInfoLink"], post.PokemonId)),
                !string.IsNullOrEmpty(location) ? string.Format(Language.Formats["postLocation"], location) : "",
                string.Format(!post.HasEndDate ? Language.Formats["postEndsUnsure"] : Language.Formats["postEnds"], post.EndDate.AddHours(TimeOffset)),
                joinCount > 0 ? string.Format(Language.Formats["postJoined"], joinCount, joinString) : Language.Strings["postNoneJoined"]
                );
            return response;
        }
        #endregion
    }
}
