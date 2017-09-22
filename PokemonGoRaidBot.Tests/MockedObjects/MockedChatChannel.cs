using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Tests.MockedObjects
{
    class MockedChatChannel : IChatChannel
    {
        public MockedChatChannel(ChatTypes? chatType = null, ulong id = 0, string name = null, IChatServer server = null)
        {
            ChatType = chatType.HasValue ? chatType.Value : ChatTypes.Discord;
            Id = id;
            Name = name;
            Server = server ?? new MockedChatServer(ChatType);
        }

        public ChatTypes ChatType { get; set; }

        public ulong Id { get; set; }

        public string Name { get; set; }

        public IChatServer Server { get; set; }

        public IDisposable EnterTypingState()
        {
            throw new NotImplementedException();
        }

        public Task SendMessageAsync(string message, bool tts = false, object embed = null)
        {
            throw new NotImplementedException();
        }
    }
}
