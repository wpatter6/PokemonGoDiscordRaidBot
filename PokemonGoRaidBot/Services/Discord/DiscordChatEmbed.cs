using Discord;
using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Services.Discord
{
    public class DiscordChatEmbed : IChatEmbed
    {
        private EmbedBuilder builder;

        public DiscordChatEmbed()
        {
            builder = new EmbedBuilder();
        }

        private string _description;

        public string Description
        {
            get
            {
                return _description;
            }
            set
            {
                _description = value;
                builder.WithDescription(value);
            }
        }

        public void WithUrl(string url)
        {
            builder.WithUrl(url);
        }

        public void WithDescription(string desc)
        {
            Description = desc;
            builder.WithDescription(desc);
        }

        public void WithThumbnailUrl(string url)
        {
            builder.WithThumbnailUrl(url);
        }

        public void WithColor(int r, int g, int b)
        {
            builder.WithColor(r, g, b);
        }

        public void AddField(string field, string content)
        {
            builder.AddField(field, content);
        }

        public object GetEmbed()
        {
            return builder.Build();
        }
    }
}
