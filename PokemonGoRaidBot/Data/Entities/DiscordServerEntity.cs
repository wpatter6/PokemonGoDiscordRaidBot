using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Entities
{
    public class DiscordServerEntity
    {
        public ulong Id { get; set; }
        public string Name { get; set; }

        public List<RaidPostEntity> Posts { get; set; }
        public List<DiscordChannelEntity> Channels { get; set; }

        public string City { get; set; }

        public DateTime FirstSeenDate { get; set; }
        public DateTime LastSeenDate { get; set; }
    }
}
