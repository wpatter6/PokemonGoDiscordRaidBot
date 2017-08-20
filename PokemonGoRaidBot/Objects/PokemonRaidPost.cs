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

        public string DiscordColor;

        public string Location;

        public DateTime PostDate;

        public DateTime EndDate;

        public PokemonInfo Pokemon;

        public List<ulong> MessageIds = new List<ulong>();

        public List<PokemonMessage> Responses = new List<PokemonMessage>();

        public ISocketMessageChannel FromChannel;

        public ISocketMessageChannel OutputChannel;

        public string UniqueId;

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
