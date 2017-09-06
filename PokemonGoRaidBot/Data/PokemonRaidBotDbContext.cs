using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PokemonGoRaidBot.Data.Objects;
using PokemonGoRaidBot.Objects;
using PokemonGoRaidBot.Config;
using PokemonGoRaidBot.Interfaces;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Data
{
    public class PokemonRaidBotDbContext : DbContext
    {
        private string _cstr;
        private IStatMapper _mapper;
        public PokemonRaidBotDbContext(IConnectionString cstr, IStatMapper mapper)
        {
            _cstr = cstr.GetConnectionString();

            _mapper = mapper;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_cstr);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DiscordServerEntity>().HasKey(t =>  t.Id);
            modelBuilder.Entity<DiscordChannelEntity>().HasKey(t => t.Id);
            modelBuilder.Entity<PokemonEntity>().HasKey(t => t.Id);
            modelBuilder.Entity<RaidPostLocationEntity>().HasKey(t => t.Id);
            modelBuilder.Entity<RaidPostEntity>().HasKey(t => t.Id);
            modelBuilder.Entity<RaidPostChannelEntity>().HasKey(t => t.Id);


            modelBuilder.Entity<DiscordServerEntity>()
                .HasMany(s => s.Channels)
                .WithOne(c => c.Server)
                .HasForeignKey(c => c.ServerId);

            modelBuilder.Entity<DiscordChannelEntity>()
                .HasMany(c => c.PostChannels)
                .WithOne(p => p.Channel)
                .HasForeignKey(p => p.ChannelId);

            modelBuilder.Entity<RaidPostLocationEntity>()
                .HasMany(l => l.Posts)
                .WithOne(p => p.Location)
                .HasForeignKey(p => p.LocationId);

            modelBuilder.Entity<PokemonEntity>()
                .HasMany(p => p.Posts)
                .WithOne(p => p.Pokemon)
                .HasForeignKey(p => p.PokemonId);

            modelBuilder.Entity<RaidPostEntity>()
                .HasMany(p => p.ChannelPosts)
                .WithOne(c => c.Post)
                .HasForeignKey(c => c.RaidPostId);
        }

        public async Task<PokemonRaidPost> AddPost(PokemonRaidPost post)
        {
            var locationEntity = await RaidPostLocations.SingleOrDefaultAsync(x => x.Name == post.Location);
            if (locationEntity == null)
            {
                var newLoc = new RaidPostLocationEntity()
                {
                    Name = post.Location,
                    Latitude = post.LatLong?.Latitude,
                    Longitude = post.LatLong?.Longitude
                };
                locationEntity = RaidPostLocations.Add(newLoc).Entity;
                await SaveChangesAsync();
            }
            
            var postEntity = _mapper.Map<PokemonRaidPost, RaidPostEntity>(post);
            postEntity.LocationId = locationEntity.Id;

            Add(postEntity);
            await SaveChangesAsync();

            foreach(var channelId in post.ChannelMessages.Keys)
            {
                var channelPost = new RaidPostChannelEntity() { ChannelId = channelId, RaidPostId = postEntity.Id };
                Add(channelPost);
            }
            await SaveChangesAsync();

            post.DbId = postEntity.Id;
            post.DbLocationId = locationEntity.Id;
            return post;
        }
        public DbSet<DiscordServerEntity> Servers { get; set; }
        public DbSet<DiscordChannelEntity> Channels { get; set; }

        public DbSet<PokemonEntity> Pokemon { get; set; }
        public DbSet<RaidPostEntity> RaidPosts { get; set; }
        public DbSet<RaidPostLocationEntity> RaidPostLocations { get; set; }
        //public DbSet<DiscordUserEntity> Users { get; set; }
    }
}
