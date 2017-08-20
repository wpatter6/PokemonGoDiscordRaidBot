using System;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonMessage
    {
        public PokemonMessage(string username, string message, DateTime date)
        {
            Username = username;
            Content = message;
            MessageDate = date;
        }
        public string Username;
        public string Content;
        public DateTime MessageDate;
    }
}
