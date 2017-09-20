using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Objects.Interfaces
{
    public interface IChatMessage
    {
        ChatTypes ChatType { get; }
        string Content { get; }

        IChatUser User { get; }
        IChatChannel Channel { get; }
        IChatServer Server { get; }

        IEnumerable<IChatUser> MentionedUsers { get; }
        IEnumerable<IChatRole> MentionedRoles { get; }
        IEnumerable<IChatChannel> MentionedChannels { get; }

        Task AddReactionAsync(string emote, object options = null);
        Task RemoveReactionAsync(string emote, IChatUser user, object options = null);
    }
}
