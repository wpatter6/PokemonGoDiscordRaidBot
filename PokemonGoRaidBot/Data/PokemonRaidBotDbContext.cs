using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PokemonGoRaidBot.Data.Objects;

namespace PokemonGoRaidBot.Data
{
    public class PokemonRaidBotDbContext : DbContext
    {

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(@"DataSource=pogoraids.db;");
        }

        public DbSet<ChannelDTO> Channels { get; set; }
        public DbSet<PokemonDTO> Pokemon { get; set; }
        public DbSet<RaidPostDTO> RaidPosts { get; set; }
        public DbSet<DiscordServerDTO> Servers { get; set; }
        public DbSet<DiscordUserDTO> Users { get; set; }
    }
}
