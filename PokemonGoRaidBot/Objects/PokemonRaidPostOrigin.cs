using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonRaidPostOrigin
    {
        public ulong MessageId;
        public ulong UserId;

        public PokemonRaidPostOrigin(ulong messageId, ulong userId)
        {
            MessageId = messageId;
            UserId = userId;
        }
    }
}
