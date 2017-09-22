using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;

namespace PokemonGoRaidBot.Tests.MockedObjects
{
    class MockedChatServer : IChatServer
    {
        public MockedChatServer(ChatTypes? chatType = null, ulong id = 0, string name = null, IEnumerable<IChatRole> roles = null, 
            IEnumerable<IChatChannel> channels = null, IEnumerable<IChatUser> users = null)
        {
            ChatType = chatType.HasValue ? chatType.Value : ChatTypes.Discord;
            Id = id;
            Name = name;
            ((List<IChatRole>)Roles).AddRange(roles);
            ((List<IChatChannel>)Channels).AddRange(channels);
            ((List<IChatUser>)Users).AddRange(users);
        }

        public ChatTypes ChatType { get; set; }

        public ulong Id { get; set; }

        public string Name { get; set; }

        public IEnumerable<IChatRole> Roles => new List<IChatRole>();

        public IEnumerable<IChatChannel> Channels => new List<IChatChannel>();

        public IEnumerable<IChatUser> Users => new List<IChatUser>();
    }
}
