using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PokemonGoRaidBot.Objects;

namespace PokemonGoRaidBot.Config
{
    public class BotConfig
    {
        public string Prefix { get; set; }
        public string Token { get; set; }
        public string OutputChannel { get; set; }

        public List<GuildConfig> GuildConfigs { get; set; }

        //public Dictionary<ulong, ulong> ServerChannels { get; set; }
        //public Dictionary<ulong, string> ServerLanguages { get; set; }
        //public Dictionary<ulong, int> ServerTimezones { get; set; }

        public List<PokemonInfo> PokemonInfoList { get; set; }
        public List<ulong> PinChannels { get; set; }

        public string LinkFormat { get; set; }

        public string GoogleApiKey { get; set; }

        public GuildConfig GetGuildConfig(ulong id)
        {
            var result = GuildConfigs.FirstOrDefault(x => x.Id == id);
            if(result == null)
            {
                result = new GuildConfig()
                {
                    Id = id
                };
                GuildConfigs.Add(result);
            }
            return result;
        }

        public BotConfig()
        {
            Prefix = "!";
            Token = "";
            GuildConfigs = new List<GuildConfig>();
            PinChannels = new List<ulong>();

            //ServerChannels = new Dictionary<ulong, ulong>();
            //ServerLanguages = new Dictionary<ulong, string>();
            //ServerTimezones = new Dictionary<ulong, int>();
        }

        public void Save(string dir = "configuration/config.json")
        {
            string file = Path.Combine(AppContext.BaseDirectory, dir);
            File.WriteAllText(file, ToJson());
        }
        public static BotConfig Load(string dir = "configuration/config.json")
        {
            string file = Path.Combine(AppContext.BaseDirectory, dir);
            var result = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText(file));

            /*Make sure all properties are populated*/
            if (result.PokemonInfoList == null) result.PokemonInfoList = GetDefaultPokemonInfoList();
            if (string.IsNullOrEmpty(result.OutputChannel)) result.OutputChannel = "raid-bot";

            if (string.IsNullOrEmpty(result.LinkFormat)) result.LinkFormat = "https://pokemongo.gamepress.gg/pokemon/{0}#raid-boss-counters";

            if (result.GuildConfigs == null) result.GuildConfigs = new List<GuildConfig>();

            if (result.PinChannels == null) result.PinChannels = new List<ulong>();

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
    }
}