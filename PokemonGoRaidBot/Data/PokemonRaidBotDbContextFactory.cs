using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Configuration;
using PokemonGoRaidBot.Services;
using Microsoft.EntityFrameworkCore.Design;

namespace PokemonGoRaidBot.Data
{
    public class PokemonRaidBotDbContextFactory : IDesignTimeDbContextFactory<PokemonRaidBotDbContext>
    {
        public PokemonRaidBotDbContext CreateDbContext(string[] args)
        {
            var config = BotConfiguration.Load();

            return new PokemonRaidBotDbContext(config, new StatMapper());
        }
    }
}
