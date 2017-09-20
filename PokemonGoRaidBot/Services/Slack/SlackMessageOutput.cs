using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Configuration;
using PokemonGoRaidBot.Objects;

namespace PokemonGoRaidBot.Services.Slack
{
    public class SlackMessageOutput : IChatMessageOutput
    {
        public IChatEmbed GetHelpEmbed(BotConfiguration config, bool admin)
        {
            throw new NotImplementedException();
        }

        public IChatEmbed MakeHeaderEmbed(PokemonRaidPost post, string text = null)
        {
            throw new NotImplementedException();
        }

        public string MakeInfoLine(PokemonInfo info, BotConfiguration config, ulong guildId, int paddingSize = 0)
        {
            throw new NotImplementedException();
        }

        public string MakePostHeader(PokemonRaidPost post)
        {
            throw new NotImplementedException();
        }

        public void MakePostWithEmbed(PokemonRaidPost post, ServerConfiguration guildConfig, out IChatEmbed header, out IChatEmbed response, out string channel, out string mentions)
        {
            throw new NotImplementedException();
        }

        public IChatEmbed MakeResponseEmbed(PokemonRaidPost post, ServerConfiguration guildConfig, string header)
        {
            throw new NotImplementedException();
        }
    }
}
