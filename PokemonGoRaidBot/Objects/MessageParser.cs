using Discord.WebSocket;
using PokemonGoRaidBot.Config;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace PokemonGoRaidBot.Objects
{
    public static class MessageParser
    {
        private static int maxRaidMinutes = 100;
        private static int colorIndex = 0;
        private static string[] minuteAliases = new string[] { "m", "mi", "min", "mins", "minutes", "minute" };
        private static string[] hourAliases = new string[] { "h", "hr", "hour", "hours" };
        private static string[] discordColors = new string[] { "css", "brainfuck", "fix", "apache" };

        /// <summary>
        /// Will attempt to parse the necessary information out of a message to create a raid post.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="config"></param>
        /// <returns>If return value is null, or property 'Pokemon' is null, raid post is invalid.</returns>
        public static PokemonRaidPost ParseMessage(SocketMessage message, BotConfig config)
        {
            var result = new PokemonRaidPost()
            {
                User = message.Author.Username,
                PostDate = DateTime.Now,//uses local time for bot
                FromChannel = message.Channel,
                Responses = new List<PokemonMessage>() { new PokemonMessage(message.Author.Username, message.Content) }
            };

            var messageString = message.Content;

            var words = messageString.Split(' ');
            if (words.Length < 2) return null;

            var timespan = new TimeSpan();
            var i = 0;

            foreach (var word in words)
            {
                i++;

                if (result.Pokemon == null)
                {
                    result.Pokemon = GetPokemon(word, config);
                    if (result.Pokemon != null) continue;
                }

                var ts = ParseTimespan(word);
                if (ts.Ticks > 0)
                {
                    timespan = timespan.Add(ts);
                    continue;
                }

                if (minuteAliases.Contains(word) && i > 1)//go back and get the previous word
                {
                    var mins = words[i - 2];
                    var min = 0;
                    if (Int32.TryParse(mins, out min))
                        timespan = timespan.Add(new TimeSpan(0, min, 0));
                    else if (mins.ToLowerInvariant() == "a")
                        timespan = timespan.Add(new TimeSpan(0, 1, 0));
                }

                if (hourAliases.Contains(word) && i > 1)//go back and get the previous word
                {
                    var hrs = words[i - 2];
                    var hr = 0;
                    if (Int32.TryParse(hrs, out hr))
                        timespan = timespan.Add(new TimeSpan(hr, 0, 0));
                    else if (hrs.ToLowerInvariant() == "a")
                        timespan = timespan.Add(new TimeSpan(1, 0, 0));
                }
            }

            if (timespan.Ticks > 0 && !Regex.IsMatch(messageString, "(there|arrive) in|away|my way|omw"))
            {
                result.EndDate = result.PostDate + timespan;
                result.HasEndDate = true;
            }
            else
            {
                result.EndDate = result.PostDate + new TimeSpan(0, maxRaidMinutes, 0);
                result.HasEndDate = false;
            }

            return result;
        }
        /// <summary>
        /// Creates the string that contains user resposes to a raid post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        public static string[] MakeResponseStrings(PokemonRaidPost post, string startMessage)
        {
            List<string> resultList = new List<string>();
            int i = 0, maxLength = 2000, firstMaxLength = maxLength - startMessage.Length;

            resultList.Add(startMessage + $"```{post.DiscordColor ?? (post.DiscordColor = discordColors[colorIndex >= discordColors.Length - 1 ? (colorIndex = 0) : colorIndex++])}");

            foreach(var message in post.Responses)
            {
                var messageString = $"\n   #{message.Username}:  {Regex.Replace(message.Content, @"<(@|#)[0-9]*>", "").TrimStart()}";

                if (resultList[i].Length + messageString.Length > (i==0 ? firstMaxLength : maxLength))
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

        /// <summary>
        /// Returns a single row of pokemon info for the !info command.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public static string MakeInfoLine(PokemonInfo info, int paddingSize = 0)
        {
            var lineFormat = "\n{0}: {7}Tier={1} BossCP={2:#,##0} MinCP={3:#,##0} MaxCP={4:#,##0} CatchRate={5}%{6}";
            var padding = 0;
            if (paddingSize > 0)
                padding = paddingSize - info.BossNameFormatted.Length;

            return string.Format(lineFormat, info.BossNameFormatted, info.Tier, info.BossCP, info.MinCP, info.MaxCP, 
                info.CatchRate * 100, 
                info.Aliases.Count() == 0 ? "" : " Aliases: " + string.Join(",", info.Aliases),
                new String(' ', padding));
        }
        /// <summary>
        /// Attempts to match a single word string with a pokemon's name or aliases.  
        /// The string must be longer than three characters
        /// And will only match aliases exactly, or the beginning or entierty of the pokemon's name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        private static PokemonInfo GetPokemon(string name, BotConfig config)
        {
            if (name.Length < 3) return null;

            var cleanedName = Regex.Replace(name, @"\W", "").ToLowerInvariant();

            var result = config.PokemonInfoList.FirstOrDefault(x => x.Aliases.Contains(cleanedName));
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
        private static TimeSpan ParseTimespan(string message)
        {
            var tsRegex = new Regex("([0-9]):([0-9]{2})");
            if (tsRegex.IsMatch(message))
            {
                var match = tsRegex.Match(message);
                string hour = match.Groups[1].Value, minute = match.Groups[2].Value;

                return new TimeSpan(Convert.ToInt32(hour), Convert.ToInt32(minute), 0);
            }

            var ts = new TimeSpan(0);
            var hrRegex = new Regex("([0-9]{1})h", RegexOptions.IgnoreCase);
            if (hrRegex.IsMatch(message))
            {
                var match = hrRegex.Match(message);
                string hour = match.Groups[1].Value;//, minute = match.Groups[1].Value;
                ts.Add(new TimeSpan(Convert.ToInt32(hour), 0, 0));
            }

            var minRegex = new Regex("([0-9]{1,2})m");
            if (minRegex.IsMatch(message))
            {
                var match = minRegex.Match(message);
                string min = match.Groups[1].Value;//, minute = match.Groups[1].Value;
                ts = ts.Add(new TimeSpan(0, Convert.ToInt32(min), 0));
            }
            return ts;
        }
    }
}
