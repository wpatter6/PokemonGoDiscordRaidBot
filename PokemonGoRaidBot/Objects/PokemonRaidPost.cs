using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonRaidPost
    {
        public PokemonRaidPost()
        {
            UniqueId = NewId();
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
        
        public List<ulong> OutputMessageIds = new List<ulong>();

        public List<ulong> MentionedRoleIds = new List<ulong>();

        public List<PokemonMessage> Responses = new List<PokemonMessage>();

        public Dictionary<ulong, int> JoinedUsers = new Dictionary<ulong, int>();

        public ulong GuildId;

        public ulong FromChannelId;

        public ulong OutputChannelId;

        public string UniqueId;

        public KeyValuePair<double, double>? LatLong;

        public int[] Color;

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
