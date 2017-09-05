using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Objects
{
    public class DiscordServerDTO
    {
        public DiscordServerDTO(ulong id, string name)
        {
            Id = id;
            Name = name;
        }
        public ulong Id { get; set; }
        public string Name { get; set; }
        public IEnumerable<ChannelDTO> Channels { get; set; }

        public DateTime FirstSeenDate { get; set; }
        public DateTime LastSeenDate { get; set; }
    }
}
