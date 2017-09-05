using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Objects
{
    public class ChannelDTO
    {
        public ChannelDTO(ulong id, string name, DiscordServerDTO server)
        {
            Id = id;
            Name = name;
            Server = server;
        }
        public ulong Id { get; set; }
        public string Name { get; set; }
        public DiscordServerDTO Server { get; set; }
    }
}
