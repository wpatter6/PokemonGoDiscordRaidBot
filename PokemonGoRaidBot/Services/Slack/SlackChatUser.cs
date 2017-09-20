using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Services.Slack
{
    public class SlackChatUser : IChatUser
    {
        public ChatTypes ChatType => throw new NotImplementedException();

        public ulong Id => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public string Nickname => throw new NotImplementedException();

        public bool IsAdmin => throw new NotImplementedException();

        public Task<IChatChannel> GetOrCreateDMChannelAsync()
        {
            throw new NotImplementedException();
        }
    }
}
