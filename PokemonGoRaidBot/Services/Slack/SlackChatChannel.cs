using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Services.Slack
{
    public class SlackChatChannel : IChatChannel
    {
        public ChatTypes ChatType => throw new NotImplementedException();

        public ulong Id => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public IChatServer Server => throw new NotImplementedException();

        public IDisposable EnterTypingState()
        {
            throw new NotImplementedException();
        }

        public Task SendMessageAsync(string message, bool tts = false, IChatEmbed embed = null)
        {
            throw new NotImplementedException();
        }
    }
}
