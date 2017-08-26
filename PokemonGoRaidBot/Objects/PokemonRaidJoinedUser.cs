using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonRaidJoinedUser
    {
        public PokemonRaidJoinedUser (PokemonRaidPost post, ulong id, string name, int count, bool isMore = false, bool isLess = false, DateTime? arriveTime = null)
        {
            Id = id;
            Name = name;
            Post = post;
            PeopleCount = count;
            ArriveTime = arriveTime;
            IsMore = isMore;
            IsLess = isLess;
        }
        public ulong Id { get; set; }
        public string Name { get; set; }

        private int _peopleCount;

        public int PeopleCount
        {
            get
            {
                return _peopleCount;
            }
            set
            {
                _peopleCount = value;
                Post.UsersChanged();
            }
        }
        public DateTime? ArriveTime { get; set; }
        public readonly PokemonRaidPost Post;

        [JsonIgnore]
        public bool IsMore { get; set; }
        [JsonIgnore]
        public bool IsLess { get; set; }
    }
}
