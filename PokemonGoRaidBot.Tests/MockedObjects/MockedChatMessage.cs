using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Tests.MockedObjects
{
    class MockedChatMessage : IChatMessage
    {
        public MockedChatMessage (string content, ChatTypes? chatType = null, IChatUser user = null, IChatChannel channel = null, IChatServer server = null)
        {
            Content = content;
            ChatType = chatType.HasValue ? chatType.Value : ChatTypes.Discord;
            User = user ?? new MockedChatUser(ChatType);
            Channel = channel ?? new MockedChatChannel(ChatType);
            Server = server ?? new MockedChatServer(ChatType);
        }

        public ChatTypes ChatType { get; set; }

        public string Content { get; set; }

        public IChatUser User { get; set; }

        public IChatChannel Channel { get; set; }

        public IChatServer Server { get; set; }

        public IEnumerable<IChatUser> MentionedUsers => new List<IChatUser>();

        public IEnumerable<IChatRole> MentionedRoles => new List<IChatRole>();

        public IEnumerable<IChatChannel> MentionedChannels => new List<IChatChannel>();

        public Task AddReactionAsync(string emote, object options = null)
        {
            throw new NotImplementedException();
        }

        public Task RemoveReactionAsync(string emote, IChatUser user, object options = null)
        {
            throw new NotImplementedException();
        }
    }
}
