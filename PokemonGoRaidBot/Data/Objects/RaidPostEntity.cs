using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Objects
{
    public class RaidPostEntity
    {
        public PokemonEntity Pokemon { get; set; }
        public ChannelEntity Channel { get; set; }
        public DiscordUserEntity PostedByUser { get; set; }
        public DateTime PostedDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Location { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string UniqueId { get; set; }
        public int ResponseCount { get; set; }
        public int JoinCount { get; set; }
    }
}
