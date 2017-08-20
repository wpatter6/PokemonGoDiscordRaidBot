using System;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonMessage
    {
        public PokemonMessage(ulong userId, string userName, string message, DateTime date)
        {
            UserId = userId;
            Username = userName;
            Content = message;
            MessageDate = date;
        }
        //public string Username;
        public ulong UserId;
        public string Username;
        public string Content;
        public DateTime MessageDate;
    }
}
