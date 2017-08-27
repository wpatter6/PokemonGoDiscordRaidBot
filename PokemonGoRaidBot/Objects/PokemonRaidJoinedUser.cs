using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonRaidJoinedUser
    {
        public PokemonRaidJoinedUser (ulong id, ulong guildId, string postId, string username, int count, bool isMore = false, bool isLess = false, DateTime? arriveTime = null)
        {
            Id = id;
            GuildId = guildId;
            PostId = postId;
            Name = username;
            PeopleCount = count;
            ArriveTime = arriveTime;
            IsMore = isMore;
            IsLess = isLess;
        }
        public ulong Id { get; set; }
        public ulong GuildId { get; set; }
        public string PostId { get; set; }
        public string Name { get; set; }


        private int peopleCount;

        public int PeopleCount {
            get
            {
                return peopleCount;
            }
            set
            {
                if(peopleCount != value)
                {
                    var diff = value - peopleCount;
                    peopleCount = value;
                    OnPeopleCountChanged(new JoinedCountChangedEventArgs(Id, Name, diff, ArriveTime, JoinCountChangeType.Change));
                }
            }
        }

        protected virtual void OnPeopleCountChanged(JoinedCountChangedEventArgs e)
        {
            PeopleCountChanged?.Invoke(this, e);
        }

        public event JoinedUserCountChangedEventHandler PeopleCountChanged;

        public delegate void JoinedUserCountChangedEventHandler(object sender, JoinedCountChangedEventArgs e);

        public DateTime? ArriveTime { get; set; }

        [JsonIgnore]
        public bool IsMore { get; set; }
        [JsonIgnore]
        public bool IsLess { get; set; }
    }
}
