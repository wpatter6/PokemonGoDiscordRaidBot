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

        public DbSet<ChannelEntity> Channels { get; set; }
        public DbSet<PokemonEntity> Pokemon { get; set; }
        public DbSet<RaidPostEntity> RaidPosts { get; set; }
        public DbSet<DiscordServerEntity> Servers { get; set; }
        public DbSet<DiscordUserEntity> Users { get; set; }
    }
}
