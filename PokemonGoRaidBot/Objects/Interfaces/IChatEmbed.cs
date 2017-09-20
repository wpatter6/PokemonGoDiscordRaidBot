using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects.Interfaces
{
    public interface IChatEmbed
    {
        string Description { get; set; }

        object GetEmbed();

        void WithUrl(string url);
        void WithDescription(string desc);
        void WithThumbnailUrl(string url);
        void WithColor(int r, int g, int b);
        void AddField(string field, string content);
    }
}