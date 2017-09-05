using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Objects
{
    public class DiscordUserDTO
    {
        public DiscordUserDTO(ulong id, string name)
        {
            Id = id;
            Name = name;
        }
        public ulong Id { get; set; }
        public string Name { get; set; }
    }
}