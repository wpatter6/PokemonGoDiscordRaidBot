using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects.Interfaces
{
    public interface IChatRole
    {
        ulong Id { get; }
        string Name { get; }
    }
}
