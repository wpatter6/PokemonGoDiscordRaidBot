using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Entities
{
    public class RaidPostChannelEntity
    {
        public ulong Id { get; set; }

        public ulong ChannelId { get; set; }
        public DiscordChannelEntity Channel { get; set; }

        public ulong RaidPostId { get; set; }
        public RaidPostEntity Post { get; set; }
    }
}
