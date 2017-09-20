using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;

namespace PokemonGoRaidBot.Services.Slack
{
    public class SlackChatServer : IChatServer
    {
        public ChatTypes ChatType => throw new NotImplementedException();

        public ulong Id => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public IEnumerable<IChatRole> Roles => throw new NotImplementedException();

        public IEnumerable<IChatChannel> Channels => throw new NotImplementedException();

        public IEnumerable<IChatUser> Users => throw new NotImplementedException();
    }
}
