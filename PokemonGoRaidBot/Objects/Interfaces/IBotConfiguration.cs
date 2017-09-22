using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects.Interfaces
{
    public interface IBotConfiguration : IConnectionString
    {
        string Version { get; set; }
        string Prefix { get; set; }
        string Token { get; set; }
        string SlackToken { get; set; }
        string OutputChannel { get; set; }
        string DefaultLanguage { get; set; }
        string StatDBConnectionString { get; set; }

        List<IBotServerConfiguration> GuildConfigs { get; }
        List<ulong> NoDMUsers { get; }
        string GoogleApiKey { get; set; }


        void Save(string dir = "Configuration/config.json");
        bool HasGuildConfig(ulong id);
        IBotServerConfiguration GetServerConfig(ulong id, ChatTypes chatType);
    }
}
