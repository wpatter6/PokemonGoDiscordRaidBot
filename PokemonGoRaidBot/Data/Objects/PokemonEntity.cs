using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Data.Objects
{
    public class PokemonEntity
    {
        public PokemonEntity(int id, string name)
        {
            Id = id;
            Name = name;
        }
        public int Id;
        public string Name;
    }
}
