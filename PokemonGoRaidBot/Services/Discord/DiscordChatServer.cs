using PokemonGoRaidBot.Objects.Interfaces;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;
using Discord.WebSocket;
using Discord;

namespace PokemonGoRaidBot.Services.Discord
{
    public class DiscordChatServer : IChatServer
    {
        public ChatTypes ChatType => ChatTypes.Discord;

        public ulong Id { get; private set; }

        public string Name { get; private set; }

        public IEnumerable<IChatRole> Roles { get; private set; }

        public IEnumerable<IChatChannel> Channels { get; private set; }

        public IEnumerable<IChatUser> Users { get; private set; }

        public DiscordChatServer(SocketGuild guild)
        {
            Id = guild.Id;
            Name = guild.Name;

            Roles = guild.Roles.Where(x => x.IsMentionable).Select(xx => new DiscordChatRole(xx));
            Channels = guild.Channels.Select(x => new DiscordChatChannel((IChannel)x));
            Users = guild.Users.Select(x => new DiscordChatUser(x));
        }
    }
}
