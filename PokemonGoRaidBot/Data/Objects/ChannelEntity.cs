using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Objects
{
    public class ChannelEntity
    {
        public ChannelEntity(ulong id, string name, DiscordServerEntity server)
        {
            Id = id;
            Name = name;
            Server = server;
        }
        public ulong Id { get; set; }
        public string Name { get; set; }
        public DiscordServerEntity Server { get; set; }
    }
}
