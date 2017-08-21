using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Config
{
    public class GuildConfig
    {
        public GuildConfig()
        {
            ChannelCities = new Dictionary<ulong, string>();
        }
        public ulong Id { get; set; }
        public ulong? OutputChannelId { get; set; }
        public int? Timezone { get; set; }
        public string LinkFormat { get; set; }
        public string Language { get; set; }
        public string City { get; set; }
        public Dictionary<ulong, string> ChannelCities { get; set; }
    }
}
