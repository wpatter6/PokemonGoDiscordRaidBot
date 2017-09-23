using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;
using Discord.WebSocket;
using System.Threading.Tasks;
using Discord;

namespace PokemonGoRaidBot.Services.Discord
{
    public class DiscordChatUser : IChatUser
    {
        private IUser _user;
        public ChatTypes ChatType => ChatTypes.Discord;

        public ulong Id { get; private set; }

        public string Name { get; private set; }

        public string Nickname { get; private set; }

        public bool IsAdmin { get; private set; }

        public DiscordChatUser (IUser user)
        {
            Id = user.Id;
            Name = user.Username;
            _user = user;
            
            if(user is SocketGuildUser)
            {
                var guildUser = (SocketGuildUser)user;
                Nickname = guildUser.Nickname;
                IsAdmin = guildUser.GuildPermissions.Administrator || guildUser.GuildPermissions.ManageGuild;
            }
        }

        public async Task<IChatChannel> GetOrCreateDMChannelAsync()
        {
            return new DiscordChatChannel(await _user.GetOrCreateDMChannelAsync());
        }
    }
}
