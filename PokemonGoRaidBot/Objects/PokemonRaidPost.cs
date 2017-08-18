using Discord.WebSocket;
using System;
using System.Collections.Generic;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonRaidPost
    {
        public ulong MessageId;

        public bool HasEndDate;
        
        public string User;

        public string DiscordColor;

        public DateTime PostDate;

        public DateTime EndDate;

        public PokemonInfo Pokemon;

        public List<PokemonMessage> Responses = new List<PokemonMessage>();

        public ISocketMessageChannel FromChannel;

        public ISocketMessageChannel OutputChannel;
    }
}
