using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Entities
{
    public class RaidPostLocationEntity
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public List<RaidPostEntity> Posts { get; set; }
    }
}
