using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonRaidPostOwner
    {
        public ulong MessageId;
        public ulong UserId;

        public PokemonRaidPostOwner(ulong messageId, ulong userId)
        {
            MessageId = messageId;
            UserId = userId;
        }
    }
}
