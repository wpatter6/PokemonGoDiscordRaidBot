using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PokemonGoRaidBot.Objects;

namespace PokemonGoRaidBot.Parsing
{
    public class ParserLanguage
    {
        private dynamic Language;
        public ParserLanguage(string language = "en-us")
        {
            string file = Path.Combine(AppContext.BaseDirectory, string.Format("Languages/{0}.json", language));
            if (!File.Exists(file))
                file = Path.Combine(AppContext.BaseDirectory, "Languages/en-us.json");

           Language = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(file));
        }

        private Dictionary<string, Regex> _regularExpressions;
        public Dictionary<string, Regex> RegularExpressions
        {
            get
            {
                if (_regularExpressions != null) return _regularExpressions;

                var result = new Dictionary<string, Regex>();

                var regList = (JObject)Language.regularExpressions;

                foreach(var reg in regList)
                {
                    result.Add(reg.Key, new Regex((string)reg.Value, RegexOptions.IgnoreCase));
                }

                return _regularExpressions = result;
            }
        }

        public Regex CombineRegex(string seperator, params string[] keys)
        {
            var regList = (JObject)Language.regularExpressions;
            var results = new List<string>();
            foreach (var reg in regList)
            {
                if(keys.Contains(reg.Key))
                {
                    results.Add((string)reg.Value);
                }
            }
            
            return new Regex(string.Join(seperator, results), RegexOptions.IgnoreCase);
        }

        private Dictionary<string, string> _formats;
        public Dictionary<string, string> Formats
        {
            get
            {
                if (_formats != null) return _formats;

                var result = new Dictionary<string, string>();
                var formatList = (JObject)Language.formats;
                foreach(var f in formatList)
                {
                    result.Add(f.Key, (string)f.Value);
                }

                return _formats = result;
            }
        }

        private Dictionary<string, string> _strings;
        public Dictionary<string, string> Strings
        {
            get
            {
                if (_strings != null) return _strings;

                var result = new Dictionary<string, string>();
                var formatList = (JObject)Language.strings;
                foreach (var f in formatList)
                {
                    result.Add(f.Key, (string)f.Value);
                }

                return _strings = result;
            }
        }

        private List<PokemonInfo> _pokemon;
        public List<PokemonInfo> Pokemon
        {
            get
            {
                if (_pokemon != null) return _pokemon;
                
                return (_pokemon = ((JArray)Language.pokemon).ToObject<List<PokemonInfo>>());
            }
        }
    }
}
