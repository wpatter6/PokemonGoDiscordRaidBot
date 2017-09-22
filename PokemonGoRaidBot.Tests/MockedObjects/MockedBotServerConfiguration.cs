using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;

namespace PokemonGoRaidBot.Tests.MockedObjects
{
    class MockedBotServerConfiguration : IBotServerConfiguration
    {
        public ulong Id { get; set; }
        public ulong? OutputChannelId { get; set; }
        public int? Timezone { get; set; }
        public string LinkFormat  { get; set; }
        public string Language  { get; set; }
        public string City  { get; set; }
        public Dictionary<ulong, string> ChannelCities => new Dictionary<ulong, string>();
        public Dictionary<int, List<string>> PokemonAliases => new Dictionary<int, List<string>>();
        public List<PokemonRaidPost> Posts => new List<PokemonRaidPost>();
        public List<ulong> PinChannels => new List<ulong>();
        public List<ulong> MuteChannels => new List<ulong>();
        public Dictionary<string, GeoCoordinate> Places => new Dictionary<string, GeoCoordinate>();
        public ChatTypes? ChatType  { get; set; }
    }
}
