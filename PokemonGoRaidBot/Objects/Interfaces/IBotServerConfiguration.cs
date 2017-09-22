using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects.Interfaces
{
    public interface IBotServerConfiguration
    {
        ulong Id { get; set; }
        ulong? OutputChannelId { get; set; }
        int? Timezone { get; set; }
        string LinkFormat { get; set; }
        string Language { get; set; }
        string City { get; set; }
        Dictionary<ulong, string> ChannelCities { get; }
        Dictionary<int, List<string>> PokemonAliases { get; }
        List<PokemonRaidPost> Posts { get; }
        List<ulong> PinChannels { get; }
        List<ulong> MuteChannels { get; }
        Dictionary<string, GeoCoordinate> Places { get; }
        ChatTypes? ChatType { get; set; }
    }
}
