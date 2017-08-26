using Newtonsoft.Json;
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
            PeopleCount = count;
            ArriveTime = arriveTime;
            IsMore = isMore;
            IsLess = isLess;
        }
        public ulong Id { get; set; }
        public string Name { get; set; }


        private int peopleCount;

        public int PeopleCount {
            get
            {
                return peopleCount;
            }
            set
            {
                peopleCount = value;
                OnPeopleCountChanged(new EventArgs());
            }
        }

        protected virtual void OnPeopleCountChanged(EventArgs e)
        {
            if (PeopleCountChanged != null)
                PeopleCountChanged(this, e);
        }

        public event EventHandler PeopleCountChanged;

        public DateTime? ArriveTime { get; set; }

        [JsonIgnore]
        public bool IsMore { get; set; }
        [JsonIgnore]
        public bool IsLess { get; set; }
    }
}
