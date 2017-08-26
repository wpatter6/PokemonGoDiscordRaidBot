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
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach(PokemonRaidJoinedUser item in e.NewItems)
                {
                    item.PeopleCountChanged -= JoinedUsers_PeopleCountChanged;
                    item.PeopleCountChanged += JoinedUsers_PeopleCountChanged;
                }
            }
            
            OnJoinedUsersChanged(new EventArgs());
        }

        private void JoinedUsers_PeopleCountChanged(object sender, EventArgs e)
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
        
        public event EventHandler JoinedUsersChanged;
        
        internal void UsersChanged()
        {
            OnJoinedUsersChanged(new EventArgs());
        }

        [JsonIgnore]
        public bool IsExisting;

        protected virtual void OnJoinedUsersChanged(EventArgs e)
        {
            if (JoinedUsersChanged != null)
                JoinedUsersChanged(this, e);
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
