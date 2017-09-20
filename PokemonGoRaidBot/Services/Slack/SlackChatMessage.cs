using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Services.Slack
{
    public class SlackChatMessage : IChatMessage
    {
        public ChatTypes ChatType => throw new NotImplementedException();

        public string Content => throw new NotImplementedException();

        public IChatUser User => throw new NotImplementedException();

        public IChatChannel Channel => throw new NotImplementedException();

        public IChatServer Server => throw new NotImplementedException();

        public IEnumerable<IChatUser> MentionedUsers => throw new NotImplementedException();

        public IEnumerable<IChatRole> MentionedRoles => throw new NotImplementedException();

        public IEnumerable<IChatChannel> MentionedChannels => throw new NotImplementedException();

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
