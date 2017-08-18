using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using PokemonGoRaidBot.Objects;

namespace PokemonGoRaidBot.Config
{
    public class BotConfig
    {
        [JsonIgnore]
        public static readonly string appdir = AppContext.BaseDirectory;

        public string Prefix { get; set; }
        public string Token { get; set; }
        public string LinkUrl { get; set; }
        public string OutputChannel { get; set; }
        
        public List<PokemonInfo> PokemonInfoList { get; set; }

        public BotConfig()
        {
            Prefix = "!";
            Token = "";
        }

        public void Save(string dir = "configuration/config.json")
        {
            string file = Path.Combine(appdir, dir);
            File.WriteAllText(file, ToJson());
        }
        public static BotConfig Load(string dir = "configuration/config.json")
        {
            string file = Path.Combine(appdir, dir);
            var result = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText(file));
            if (result.PokemonInfoList == null) result.PokemonInfoList = GetDefaultPokemonInfoList();
            if (string.IsNullOrEmpty(result.LinkUrl)) result.LinkUrl = "https://pokemongo.gamepress.gg/pokemon/{0}";
            if (string.IsNullOrEmpty(result.OutputChannel)) result.OutputChannel = "raid-bot";
            
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