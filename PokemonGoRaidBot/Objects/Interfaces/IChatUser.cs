using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Objects.Interfaces
{
    public interface IChatUser
    {
        ChatTypes ChatType { get; }
        ulong Id { get; }
        string Name { get; }
        string Nickname { get; }
        bool IsAdmin { get; }

        Task<IChatChannel> GetOrCreateDMChannelAsync();
    }
}
