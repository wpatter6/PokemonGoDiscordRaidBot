using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects.Interfaces
{
    public interface IChatServer
    {
        ChatTypes ChatType { get; }
        ulong Id { get; }
        string Name { get; }
        IEnumerable<IChatRole> Roles { get; }
        IEnumerable<IChatChannel> Channels { get; }
        IEnumerable<IChatUser> Users { get; }
    }
}
