using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Objects.Interfaces
{
    public interface IChatChannel
    {
        ChatTypes ChatType { get; }
        ulong Id { get; }
        string Name { get; }
        IChatServer Server { get; }

        IDisposable EnterTypingState();

        Task SendMessageAsync(string message, bool tts = false, object embed = null);
    }
}
