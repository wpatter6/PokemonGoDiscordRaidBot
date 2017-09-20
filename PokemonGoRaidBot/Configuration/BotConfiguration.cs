using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PokemonGoRaidBot.Objects;
using System.Reflection;
using PokemonGoRaidBot.Objects.Interfaces;

namespace PokemonGoRaidBot.Configuration
{
    public class BotConfiguration : IConnectionString
    {
        public string Version { get; set; }
        public string Prefix { get; set; }
        public string Token { get; set; }
        public string SlackToken { get; set; }
        public string OutputChannel { get; set; }
        public string DefaultLanguage { get; set; }
        public string StatDBConnectionString { get; set; }

        public List<ServerConfiguration> GuildConfigs { get; set; }
        public List<ulong> NoDMUsers { get; set; }

        //public List<PokemonInfo> PokemonInfoList { get; set; }

        public string GoogleApiKey { get; set; }

        public bool HasGuildConfig(ulong id)
        {
            return GuildConfigs.Where(x => x.Id == id).Count() > 0;
        }

        public ServerConfiguration GetServerConfig(ulong id, ChatTypes chatType)
        {
            var result = GuildConfigs.FirstOrDefault(x => x.Id == id && (x.ChatType ?? ChatTypes.Discord) == chatType);
            if(result == null)
            {
                result = new ServerConfiguration()
                {
                    Id = id,
                    ChatType = chatType
                };
                GuildConfigs.Add(result);
            }
            result.ChatType = chatType;
            return result;
        }

        public BotConfiguration()
        {
            Prefix = "!";
            Token = "";
            GuildConfigs = new List<ServerConfiguration>();
        }

        public void Save(string dir = "Configuration/config.json")
        {
            string file = Path.Combine(AppContext.BaseDirectory, dir);
            File.WriteAllText(file, ToJson());
        }

        public static BotConfiguration Load(string dir = "Configuration/config.json")
        {
            string file = Path.Combine(AppContext.BaseDirectory, dir);
            var result = JsonConvert.DeserializeObject<BotConfiguration>(File.ReadAllText(file));

            var version = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            if (result.Version != version)
            {//upgrade occurred
                //do something?
                result.Version = version;
            }

            //Make sure all properties are populated
            //if (result.PokemonInfoList == null) result.PokemonInfoList = GetDefaultPokemonInfoList();

            if (string.IsNullOrEmpty(result.OutputChannel)) result.OutputChannel = "raid-bot";
            if (string.IsNullOrEmpty(result.DefaultLanguage)) result.DefaultLanguage = "en-us";
            if (string.IsNullOrEmpty(result.StatDBConnectionString)) result.StatDBConnectionString = "Data Source=raidstats.db;";

            if (result.GuildConfigs == null) result.GuildConfigs = new List<ServerConfiguration>();
            if (result.NoDMUsers == null) result.NoDMUsers = new List<ulong>();

            result.Save();
            return result;
        }

        public string ToJson()
            => JsonConvert.SerializeObject(this, Formatting.Indented);
        
        private static List<PokemonInfo> GetDefaultPokemonInfoList()
        {
            var result = JsonConvert.DeserializeObject<List<PokemonInfo>>(File.ReadAllText("RaidInfo.json"));

            return result;
        }

        public string GetConnectionString()
        {
            return StatDBConnectionString;
        }
    }
}