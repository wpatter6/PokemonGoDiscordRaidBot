using PokemonGoRaidBot.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects.Interfaces
{
    public interface IChatMessageOutput
    {
        string MakeInfoLine(PokemonInfo info, BotConfiguration config, ulong guildId, int paddingSize = 0);
        string MakePostHeader(PokemonRaidPost post);

        IChatEmbed GetHelpEmbed(BotConfiguration config, bool admin);
        IChatEmbed MakeResponseEmbed(PokemonRaidPost post, ServerConfiguration guildConfig, string header);
        IChatEmbed MakeHeaderEmbed(PokemonRaidPost post, string text = null);

        void MakePostWithEmbed(PokemonRaidPost post, ServerConfiguration guildConfig, out IChatEmbed header, out IChatEmbed response, out string channel, out string mentions);
    }
}
