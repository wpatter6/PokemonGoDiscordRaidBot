using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Entities
{
    public class DiscordUserEntity
    {
        public ulong Id { get; set; }
        public string Name { get; set; }

        public List<RaidPostEntity> Posts { get; set; }
    }
}