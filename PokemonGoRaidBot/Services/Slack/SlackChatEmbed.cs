using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Services.Slack
{
    public class SlackChatEmbed : IChatEmbed
    {
        public string Description { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void AddField(string field, string content)
        {
            throw new NotImplementedException();
        }

        public object GetEmbed()
        {
            throw new NotImplementedException();
        }

        public void WithColor(int r, int g, int b)
        {
            throw new NotImplementedException();
        }

        public void WithDescription(string desc)
        {
            throw new NotImplementedException();
        }

        public void WithThumbnailUrl(string url)
        {
            throw new NotImplementedException();
        }

        public void WithUrl(string url)
        {
            throw new NotImplementedException();
        }
    }
}
