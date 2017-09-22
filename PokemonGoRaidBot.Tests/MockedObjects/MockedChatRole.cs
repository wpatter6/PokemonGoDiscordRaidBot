using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Tests.MockedObjects
{
    class MockedChatRole : IChatRole
    {
        public MockedChatRole(ulong id = 0, string name = null)
        {
            Id = id;
            Name = name;
        }
        public ulong Id { get; set; }

        public string Name { get; set; }
    }
}
