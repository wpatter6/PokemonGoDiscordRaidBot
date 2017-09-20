using PokemonGoRaidBot.Objects.Interfaces;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;
using Discord.WebSocket;
using System.Threading.Tasks;
using Discord;

namespace PokemonGoRaidBot.Services.Discord
{
    public class DiscordChatMessage : IChatMessage
    {
        private IUserMessage _message;
        public ChatTypes ChatType => ChatTypes.Discord;

        public string Content { get; private set; }
        public IChatUser User { get; private set; }
        public IChatChannel Channel { get; private set; }
        public IChatServer Server { get; private set; }

        public IEnumerable<IChatChannel> MentionedChannels { get; private set; }
        public IEnumerable<IChatUser> MentionedUsers { get; private set; }
        public IEnumerable<IChatRole> MentionedRoles { get; private set; }

        public DiscordChatMessage (IUserMessage message)
        {
            _message = message;
            Content = message.Content;

            User = new DiscordChatUser(message.Author);
            Channel = new DiscordChatChannel(message.Channel);
            Server = Channel.Server;
            
            if(message is SocketMessage)
            {
                var socketMessage = (SocketMessage)message;
                MentionedRoles = socketMessage.MentionedRoles.Select(x => new DiscordChatRole(x));
                MentionedUsers = socketMessage.MentionedUsers.Select(x => new DiscordChatUser(x));
                MentionedChannels = socketMessage.MentionedChannels.Select(x => new DiscordChatChannel((ISocketMessageChannel)x));
            }
        }

        public async Task AddReactionAsync(string emote, object options = null)
        {
            RequestOptions roptions = null;
            if(options != null && options is RequestOptions)
                roptions = (RequestOptions)options;

            await _message.AddReactionAsync(new Emoji(emote), roptions);
        }

        public async Task RemoveReactionAsync(string emote, IChatUser user, object options = null)
        {
            RequestOptions roptions = null;
            if (options != null && options is RequestOptions)
                roptions = (RequestOptions)options;

            if(_message.Channel is SocketGuildChannel)
            {
                var guildUser = ((SocketGuildChannel)_message.Channel).Guild.Users.FirstOrDefault(x => x.Id == user.Id);

                if(guildUser != null)
                    await _message.RemoveReactionAsync(new Emoji(emote), guildUser, roptions);
            }
        }
    }
}
