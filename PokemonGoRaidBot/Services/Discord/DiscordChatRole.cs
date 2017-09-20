using Discord.WebSocket;
using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Services.Discord
{
    public class DiscordChatRole : IChatRole
    {
        public ulong Id { get; private set; }

        public string Name { get; private set; }

        public DiscordChatRole(SocketRole role)
        {
            Id = role.Id;
            Name = role.Name;
        }
    }
}
