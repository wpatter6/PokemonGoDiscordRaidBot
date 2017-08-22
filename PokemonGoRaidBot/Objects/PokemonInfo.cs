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

        [JsonIgnore]
        public string BossNameFormatted
        {
            get
            {
                var spaces = new string(' ', 4 - Id.ToString().Length);
                return string.Format("#{0}{1}{2}", Id, spaces, Name);
            }
        }
    }
}