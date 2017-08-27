using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonRaidPost
    {
        public PokemonRaidPost()
        {
            UniqueId = NewId();

            JoinedUsers.CollectionChanged += JoinedUsers_CollectionChanged;
        }

        private void JoinedUsers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var changeType = JoinCountChangeType.Add;
            PokemonRaidJoinedUser joinUser = null;

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                joinUser = (PokemonRaidJoinedUser)e.NewItems[0];
            }
            else if(e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                changeType = JoinCountChangeType.Remove;
                joinUser = (PokemonRaidJoinedUser)e.OldItems[0];
            }

            if(joinUser != null)
                OnJoinedUsersChanged(new JoinedCountChangedEventArgs(joinUser.Id, joinUser.Name, joinUser.PeopleCount, joinUser.ArriveTime, changeType));//should always only be one at a time
        }

        private void JoinedUsers_PeopleCountChanged(object sender, JoinedCountChangedEventArgs e)
        {
            OnJoinedUsersChanged(e);
        }

        public bool HasEndDate;

        public bool Pin;
        
        public string User;

        public ulong UserId;

        public ulong MessageId;

        public string DiscordColor;

        public string Location;//used for display

        public string FullLocation;//used for google search

        public DateTime PostDate;

        public DateTime EndDate;

        public DateTime LastMessageDate;

        public int PokemonId;

        public string PokemonName;
        
        public ulong OutputMessageId;

        public List<ulong> MentionedRoleIds = new List<ulong>();

        public List<PokemonMessage> Responses = new List<PokemonMessage>();

        public ObservableCollection<PokemonRaidJoinedUser> JoinedUsers = new ObservableCollection<PokemonRaidJoinedUser>();

        public ulong GuildId;

        public ulong FromChannelId;

        public ulong OutputChannelId;

        public string UniqueId;

        public KeyValuePair<double, double>? LatLong;

        public int[] Color;
        
        public event JoinedUserCountChangedEventHandler JoinedUsersChanged;
        
        public delegate void JoinedUserCountChangedEventHandler(object sender, JoinedCountChangedEventArgs e);
        
        [JsonIgnore]
        public bool IsExisting;

        protected virtual void OnJoinedUsersChanged(JoinedCountChangedEventArgs e)
        {
            JoinedUsersChanged?.Invoke(this, e);
        }

        private static String NewId()
        {
            long num = new Random().Next(10000000, 90000000);
            int nbase = 36;
            String chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            long r;
            String newNumber = "";

            // in r we have the offset of the char that was converted to the new base
            while (num >= nbase)
            {
                r = num % nbase;
                newNumber = chars[(int)r] + newNumber;
                num = num / nbase;
            }
            // the last number to convert
            newNumber = chars[(int)num] + newNumber;

            return newNumber.ToLower();
        }
    }
}
