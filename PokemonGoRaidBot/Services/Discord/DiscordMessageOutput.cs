using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Configuration;
using PokemonGoRaidBot.Objects;
using PokemonGoRaidBot.Services.Parsing;
using Discord;
using System.Linq;
using System.Globalization;

namespace PokemonGoRaidBot.Services.Discord
{
    public class DiscordMessageOutput : IChatMessageOutput
    {
        private ParserLanguage Language;
        private int TimeOffset;

        public DiscordMessageOutput(string language = "en-us", int timeZoneOffset = 0)
        {
            Language = new ParserLanguage(language);
            TimeOffset = timeZoneOffset;
            CultureInfo.CurrentCulture = Language.GetCultureInfo();
        }

        public IChatEmbed GetHelpEmbed(IBotConfiguration config, bool admin)
        {
            var embed = new DiscordChatEmbed();

            string info = $"*{Language.Strings["helpParenthesis"]}*";
            if (admin) info += $"\n\\**{Language.Strings["helpAdmin"]}*";

            embed.AddField($"__**{Language.Strings["helpCommands"]}**__", info);

            embed.AddField(string.Format("{0}__r__aid [pokemon] [time left] [location]", config.Prefix), Language.Strings["helpRaid"]);
            embed.AddField(string.Format("{0}__j__oin [raid] [number]", config.Prefix), Language.Strings["helpJoin"]);
            embed.AddField(string.Format("{0}__un__join [raid]", config.Prefix), Language.Strings["helpUnJoin"]);
            embed.AddField(string.Format("{0}__d__elete [raid id]", config.Prefix), Language.Strings["helpDelete"]);
            embed.AddField(string.Format("{0}__m__erge [raid1] [raid2]", config.Prefix), Language.Strings["helpMerge"]);
            embed.AddField(string.Format("{0}__loc__ation [raid] [new location]", config.Prefix), Language.Strings["helpLocation"]);
            embed.AddField(string.Format("{0}__i__nfo [name]", config.Prefix), Language.Strings["helpInfo"]);
            embed.AddField(string.Format("{0}__h__elp", config.Prefix), Language.Strings["helpHelp"]);

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
                embed.AddField(string.Format("*{0}culture [culture]", config.Prefix), Language.Strings["helpCulture"]);
                embed.AddField(string.Format("*{0}city [city]", config.Prefix), Language.Strings["helpCity"]);
                embed.AddField(string.Format("*{0}channelcity [channel name] [city]", config.Prefix), Language.Strings["helpChannelCity"]);
                embed.AddField(string.Format("*{0}cities", config.Prefix), Language.Strings["helpCities"]);
                embed.AddField(string.Format("*{0}place", config.Prefix), Language.Strings["helpPlace"]);
                embed.AddField(string.Format("*{0}deleteplace", config.Prefix), Language.Strings["helpDeletePlace"]);
                embed.AddField(string.Format("*{0}places", config.Prefix), Language.Strings["helpPlaces"]);
            }

            return embed;
        }

        public IChatEmbed MakeHeaderEmbed(PokemonRaidPost post, string text = null)
        {
            if (string.IsNullOrEmpty(text)) text = MakePostHeader(post);
            var headerembed = new DiscordChatEmbed();
            headerembed.WithColor(post.Color[0], post.Color[1], post.Color[2]);
            headerembed.WithUrl(string.Format(Language.Formats["pokemonInfoLink"], post.PokemonId));
            headerembed.WithDescription(Language.RegularExpressions["discordChannel"].Replace(text, "").Replace(" in ", " ").Replace("  ", " "));

            headerembed.WithThumbnailUrl(string.Format(Language.Formats["imageUrlSmallPokemon"], post.PokemonId.ToString().PadLeft(3, '0')));

            return headerembed;
        }

        public string MakeInfoLine(PokemonInfo info, IBotConfiguration config, ulong guildId, int paddingSize = 0)
        {
            var lineFormat = Language.Formats["infoLine"];// "\n{0}: {7}Tier={1} BossCP={2:#,##0} MinCP={3:#,##0} MaxCP={4:#,##0} CatchRate={5}%{6}";
            var padding = 0;
            if (paddingSize > 0)
                padding = paddingSize - info.BossNameFormatted.Length;

            var allAliases = new List<string>(info.Aliases);

            if (config.GetServerConfig(guildId, ChatTypes.Discord).PokemonAliases.ContainsKey(info.Id))
                allAliases.AddRange(config.GetServerConfig(guildId, ChatTypes.Discord).PokemonAliases[info.Id]);

            return string.Format(lineFormat,
                info.BossNameFormatted,
                new String(' ', padding),
                info.Tier,
                info.BossCP.ToString() + (info.BossCP < 9999 ? " " : ""),
                info.MinCP.ToString() + (info.MinCP < 999 ? " " : ""),
                info.MaxCP.ToString() + (info.MaxCP < 999 ? " " : ""),
                info.CatchRate * 100,
                allAliases.Count == 0 ? "" : Language.Strings["aliases"] + ": " + string.Join(",", allAliases)
                );
        }

        public string MakePostHeader(PokemonRaidPost post)
        {
            var joinString = string.Join(", ", post.JoinedUsers.Where(x => x.PeopleCount > 0).Select(x => string.Format("@{0}(**{1}**{2})", x.Name, x.PeopleCount, x.ArriveTime.HasValue ? $" *@{x.ArriveTime.Value.ToString("t")}*" : "")));

            var joinCount = post.JoinedUsers.Sum(x => x.PeopleCount);

            var location = post.Location;

            var groupStarts = string.Join(", ", post.RaidStartTimes.OrderBy(x => x.Ticks).Select(x => x.ToString("t")));

            if (!string.IsNullOrEmpty(groupStarts))
                groupStarts = string.Format(Language.Formats["groupStartTimes"], post.RaidStartTimes.Count, groupStarts);

            var mapLinkFormat = Language.Formats["googleMapLink"];

            var old = CultureInfo.CurrentCulture.NumberFormat;

            CultureInfo.CurrentCulture.NumberFormat = new CultureInfo("en-us").NumberFormat;

            if (post.LatLong != null && post.LatLong.HasValue) location = string.Format("[{0}]({1})", location, string.Format(mapLinkFormat, post.LatLong.Latitude, post.LatLong.Longitude));

            CultureInfo.CurrentCulture.NumberFormat = old;

            string response = string.Format(Language.Formats["postHeader"],
                post.UniqueId,
                string.Format("[{0}]({1})", post.PokemonName, string.Format(Language.Formats["pokemonInfoLink"], post.PokemonId)),
                !string.IsNullOrEmpty(location) ? string.Format(Language.Formats["postLocation"], location) : "",
                string.Format(!post.HasEndDate ? Language.Formats["postEndsUnsure"] : Language.Formats["postEnds"], post.EndDate.AddHours(TimeOffset).ToString("t")),
                groupStarts,
                joinCount > 0 ? string.Format(Language.Formats["postJoined"], joinCount, joinString) : Language.Strings["postNoneJoined"]
                );
            return response;
        }

        public void MakePostWithEmbed(PokemonRaidPost post, IBotServerConfiguration guildConfig, out IChatEmbed header, out IChatEmbed response, out string channel, out string mentions)
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

        public IChatEmbed MakeResponseEmbed(PokemonRaidPost post, IBotServerConfiguration guildConfig, string header)
        {
            var embed = new DiscordChatEmbed();

            embed.WithDescription(header);

            embed.WithColor(post.Color[0], post.Color[1], post.Color[2]);
            embed.WithUrl(string.Format(Language.Formats["pokemonInfoLink"], post.PokemonId));
            embed.WithThumbnailUrl(string.Format(Language.Formats["imageUrlLargePokemon"], post.PokemonId.ToString().PadLeft(3, '0')));

            foreach (var message in post.Responses.OrderBy(x => x.MessageDate).Skip(Math.Max(0, post.Responses.Count() - 10)))//max fields is 25
            {
                embed.AddField(string.Format(Language.Formats["responseInfo"], message.MessageDate.AddHours(TimeOffset), message.ChannelName, message.Username), message.Content);
            }
            //var builder = new EmbedBuilder();
            /*
            builder.WithColor(post.Color[0], post.Color[1], post.Color[2]);

            builder.WithDescription(header);
            builder.WithUrl(string.Format(Language.Formats["pokemonInfoLink"], post.PokemonId));

            builder.WithThumbnailUrl(string.Format(Language.Formats["imageUrlLargePokemon"], post.PokemonId.ToString().PadLeft(3, '0')));

            foreach (var message in post.Responses.OrderBy(x => x.MessageDate).Skip(Math.Max(0, post.Responses.Count() - 10)))//max fields is 25
            {
                builder.AddField(string.Format(Language.Formats["responseInfo"], message.MessageDate.AddHours(TimeOffset), message.ChannelName, message.Username), message.Content);
            }
            */
            return embed;
        }
    }
}
