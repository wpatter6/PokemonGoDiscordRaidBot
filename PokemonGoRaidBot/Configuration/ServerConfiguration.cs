using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;
using PokemonGoRaidBot.Objects.Interfaces;

namespace PokemonGoRaidBot.Configuration
{
    public class ServerConfiguration : IBotServerConfiguration
    {
        public ServerConfiguration()
        {
            ChannelCities = new Dictionary<ulong, string>();
            PokemonAliases = new Dictionary<int, List<string>>();
            Posts = new List<PokemonRaidPost>();
            PinChannels = new List<ulong>();
            MuteChannels = new List<ulong>();
            Places = new Dictionary<string, GeoCoordinate>();
        }
        public ulong Id { get; set; }
        public ulong? OutputChannelId { get; set; }
        public int? Timezone { get; set; }
        public string LinkFormat { get; set; }
        public string Language { get; set; }
        public string City { get; set; }
        public Dictionary<ulong, string> ChannelCities { get; set; }
        public Dictionary<int, List<string>> PokemonAliases { get; set; }
        public List<PokemonRaidPost> Posts { get; set; }
        public List<ulong> PinChannels { get; set; }
        public List<ulong> MuteChannels { get; set; }
        public Dictionary<string, GeoCoordinate> Places { get; set; }
        public ChatTypes? ChatType { get; set; }
    }
}
