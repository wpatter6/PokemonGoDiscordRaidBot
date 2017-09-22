using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Tests.MockedObjects
{
    class MockedChatUser : IChatUser
    {
        public MockedChatUser(ChatTypes? chatType = null, ulong id = 0, string name = null, string nickname = null, bool isAdmin = false)
        {
            ChatType = chatType.HasValue ? chatType.Value : ChatTypes.Discord;
            Id = id;
            Name = name;
            Nickname = nickname;
            IsAdmin = isAdmin;
        }

        public ChatTypes ChatType { get; set; }

        public ulong Id { get; set; }

        public string Name { get; set; }

        public string Nickname { get; set; }

        public bool IsAdmin { get; set; }

        public Task<IChatChannel> GetOrCreateDMChannelAsync()
        {
            throw new NotImplementedException();
        }
    }
}
