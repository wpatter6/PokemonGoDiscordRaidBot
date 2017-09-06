using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Entities
{
    public class RaidPostEntity
    {
        public ulong Id { get; set; }
        public ulong LocationId { get; set; }
        public int PokemonId { get; set; }

        public int JoinCount { get; set; }
        public int ResponseCount { get; set; }

        public string UniqueId { get; set; }
        public string CreationMessage { get; set; }

        public bool Deleted { get; set; }
        public bool HasEndDate { get; set; }

        public DateTime PostedDate { get; set; }
        public DateTime EndDate { get; set; }

        public PokemonEntity Pokemon { get; set; }
        public RaidPostLocationEntity Location { get; set; }

        public virtual ICollection<RaidPostChannelEntity> ChannelPosts { get; set; }
    }
}
