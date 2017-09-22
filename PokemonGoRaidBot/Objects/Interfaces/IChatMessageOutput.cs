using PokemonGoRaidBot.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects.Interfaces
{
    public interface IChatMessageOutput
    {
        string MakeInfoLine(PokemonInfo info, IBotConfiguration config, ulong guildId, int paddingSize = 0);
        string MakePostHeader(PokemonRaidPost post);

        IChatEmbed GetHelpEmbed(IBotConfiguration config, bool admin);
        IChatEmbed MakeResponseEmbed(PokemonRaidPost post, IBotServerConfiguration guildConfig, string header);
        IChatEmbed MakeHeaderEmbed(PokemonRaidPost post, string text = null);

        void MakePostWithEmbed(PokemonRaidPost post, IBotServerConfiguration guildConfig, out IChatEmbed header, out IChatEmbed response, out string channel, out string mentions);
    }
}
