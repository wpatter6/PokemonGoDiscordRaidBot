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
    public class DiscordChatChannel : IChatChannel
    {
        private IChannel _channel;

        public ChatTypes ChatType => ChatTypes.Discord;

        public ulong Id { get; private set; }

        public string Name { get; private set; }

        public IChatServer Server { get; private set; }

        public DiscordChatChannel(IChannel channel)
        {
            Id = channel.Id;
            _channel = channel;
            if(channel is SocketGuildChannel)
            {
                var guildChannel = (SocketGuildChannel)channel;
                Name = guildChannel.Name;
                Server = new DiscordChatServer(guildChannel.Guild);
            }
        }

        public IDisposable EnterTypingState()
        {
            if(_channel is IMessageChannel)
                return ((IMessageChannel)_channel).EnterTypingState();
            return new DummyDisposable();
        }

        public async Task SendMessageAsync(string message, bool tts = false, IChatEmbed embed = null)
        {
            if (_channel is IMessageChannel)
                await ((IMessageChannel)_channel).SendMessageAsync(message, tts, (Embed)embed?.GetEmbed());
        }
    }

    class DummyDisposable : IDisposable
    {
        public void Dispose()
        {
            
        }
    }
}
