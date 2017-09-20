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
        private IMessageChannel _channel;

        public ChatTypes ChatType => ChatTypes.Discord;

        public ulong Id { get; private set; }

        public string Name { get; private set; }

        public IChatServer Server { get; private set; }

        public DiscordChatChannel(IMessageChannel channel)
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
            return _channel.EnterTypingState();
        }

        public async Task SendMessageAsync(string message, bool tts = false, object embed = null)
        {
            await _channel.SendMessageAsync(message, tts, (Embed)embed);
        }
    }
}
