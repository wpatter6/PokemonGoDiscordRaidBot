using Newtonsoft.Json;
using System.Collections.Generic;

namespace PokemonGoRaidBot.Objects
{
    public class PokemonInfo
    {
        public int Id;
        public int Tier;
        public string Name;
        public int BossCP;
        public int MinCP;
        public int MaxCP;
        public double CatchRate;
        public List<string> Aliases = new List<string>();
        //public List<KeyValuePair<ulong, string>> ServerAliases = new List<KeyValuePair<ulong, string>>();

        [JsonIgnore]
        public string BossNameFormatted
        {
            get
            {
                return string.Format("#{0} {1}", Id, Name);
            }
        }
    }
}