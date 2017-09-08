using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Entities
{
    public class DiscordChannelEntity
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public ulong ServerId { get; set; }
        public DiscordServerEntity Server { get; set; }

        public string City { get; set; }

        public DateTime FirstSeenDate { get; set; }
        public DateTime LastSeenDate { get; set; }

        public virtual ICollection<RaidPostChannelEntity> PostChannels { get; set; }
    }
}
