using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using PokemonGoRaidBot.Objects;

namespace PokemonGoRaidBot.Config
{
    public class BotConfig
    {
        public string Prefix { get; set; }
        public string Token { get; set; }
        public string LinkUrl { get; set; }
        public string OutputChannel { get; set; }

        public Dictionary<ulong, ulong> ServerChannels { get; set; }
        public Dictionary<ulong, string> ServerLanguages { get; set; }
        public Dictionary<ulong, int> ServerTimezones { get; set; }
        
        public List<PokemonInfo> PokemonInfoList { get; set; }
        public List<ulong> PinChannels { get; set; }

        public BotConfig()
        {
            Prefix = "!";
            Token = "";
            ServerChannels = new Dictionary<ulong, ulong>();
            ServerLanguages = new Dictionary<ulong, string>();
            ServerTimezones = new Dictionary<ulong, int>();
            PokemonInfoList = new List<PokemonInfo>();
            PinChannels = new List<ulong>();
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
            if (string.IsNullOrEmpty(result.LinkUrl)) result.LinkUrl = "https://pokemongo.gamepress.gg/pokemon/{0}";
            if (string.IsNullOrEmpty(result.OutputChannel)) result.OutputChannel = "raid-bot";

            if (result.ServerChannels == null) result.ServerChannels = new Dictionary<ulong, ulong>();
            if (result.ServerLanguages == null) result.ServerLanguages = new Dictionary<ulong, string>();
            if (result.ServerTimezones == null) result.ServerTimezones = new Dictionary<ulong, int>();
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