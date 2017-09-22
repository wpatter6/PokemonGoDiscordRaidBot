using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects;

namespace PokemonGoRaidBot.Tests.MockedObjects
{
    class MockedBotConfiguration : IBotConfiguration
    {
        public string Version { get; set; }
        public string Prefix { get; set; }
        public string Token { get; set; }
        public string SlackToken { get; set; }
        public string OutputChannel { get; set; }
        public string DefaultLanguage { get; set; }
        public string StatDBConnectionString { get; set; }
        public List<IBotServerConfiguration> GuildConfigs => new List<IBotServerConfiguration>();
        public List<ulong> NoDMUsers => new List<ulong>();
        public string GoogleApiKey { get; set; }

        public string GetConnectionString()
        {
            return StatDBConnectionString;
        }

        public IBotServerConfiguration GetServerConfig(ulong id, ChatTypes chatType)
        {
            throw new NotImplementedException();
        }

        public bool HasGuildConfig(ulong id)
        {
            throw new NotImplementedException();
        }

        public void Save(string dir = @"Configuration\config.json")
        {
            throw new NotImplementedException();
        }
    }
}
