using System;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonMessage
    {
        public PokemonMessage(ulong userId, string userName, string message, DateTime date, string channelName)
        {
            UserId = userId;
            Username = userName;
            Content = message;
            MessageDate = date;
            ChannelName = channelName;
        }

        public ulong UserId;
        public string Username;
        public string Content;
        public string ChannelName;
        public DateTime MessageDate;
    }
}
