using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonRaidJoinedUser
    {
        public PokemonRaidJoinedUser (ulong id, string name, int count, bool isMore = false, bool isLess = false, DateTime? arriveTime = null)
        {
            Id = id;
            Name = name;
            Count = count;
            IsMore = isMore;
            IsLess = isLess;
            ArriveTime = arriveTime;
        }
        public ulong Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public bool IsMore { get; set; }
        public bool IsLess { get; set; }
        public DateTime? ArriveTime { get; set; }
    }
}
