using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using PokemonGoRaidBot.Data.Entities;
using PokemonGoRaidBot.Objects;
using PokemonGoRaidBot.Objects.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Data
{
    public class PokemonRaidBotDbContext : DbContext
    {
        private string _cstr;
        private IStatMapper _mapper;

        public PokemonRaidBotDbContext() { }

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

            modelBuilder.Entity<DiscordServerEntity>()
                .HasMany(s => s.Posts)
                .WithOne(p => p.Server)
                .HasForeignKey(p => p.ServerId);
            
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
        
        public async Task AddOrUpdateGuild(SocketGuild guild, string city = null)
        {
            var serverEntity = Servers.FirstOrDefault(x => x.Id == guild.Id);

            if (serverEntity == null)
            {
                serverEntity = _mapper.Map<DiscordServerEntity>(guild);
                serverEntity.FirstSeenDate = DateTime.Now;
                Servers.Add(serverEntity);
            }

            serverEntity.LastSeenDate = DateTime.Now;
            //serverEntity.City = city ?? serverEntity.City;

            await SaveChangesAsync();
        }

        public async Task AddOrUpdateChannel(SocketGuildChannel channel, string city = null)
        {
            var channelEntity = Channels.FirstOrDefault(x => x.Id == channel.Id);

            if (channelEntity == null)
            {
                channelEntity = _mapper.Map<DiscordChannelEntity>(channel);
                channelEntity.FirstSeenDate = DateTime.Now;
                Channels.Add(channelEntity);
            }

            channelEntity.LastSeenDate = DateTime.Now;
            //channelEntity.City = city ?? channelEntity.City;

            await SaveChangesAsync();
        }

        public async Task<PokemonRaidPost> AddOrUpdatePost(PokemonRaidPost post)
        {
            var locationEntity = await Locations.SingleOrDefaultAsync(x => x.ServerId == post.GuildId && x.Name == post.Location);
            if (locationEntity == null)
            {
                var newLoc = _mapper.Map<RaidPostLocationEntity>(post);
                locationEntity = Locations.Add(newLoc).Entity;
                await SaveChangesAsync();
            }

            var bossEntity = await Pokemon.SingleOrDefaultAsync(x => x.Id == post.PokemonId);
            if(bossEntity == null)
            {
                var newBoss = _mapper.Map<PokemonEntity>(post);
                bossEntity = Pokemon.Add(newBoss).Entity;
            }

            var postEntity = new RaidPostEntity();
            if (post.DbId != default(ulong))
                postEntity = await Posts.FirstOrDefaultAsync(x => x.Id == post.DbId);

            _mapper.Map(post, postEntity);

            postEntity.Pokemon = bossEntity;
            postEntity.PokemonId = bossEntity.Id;

            postEntity.Location = locationEntity;
            postEntity.LocationId = locationEntity.Id;

            if(post.DbId == default(ulong))
            {
                Add(postEntity);
                await SaveChangesAsync();
                post.DbId = postEntity.Id;
            }

            foreach (var channelId in post.ChannelMessages.Keys.Where(x => ChannelPosts.Where(y => y.ChannelId == x && y.RaidPostId == post.DbId).Count() == 0))
            {
                var channelPost = new RaidPostChannelEntity() { ChannelId = channelId, RaidPostId = post.DbId };
                Add(channelPost);
            }

            await SaveChangesAsync();

            post.DbLocationId = locationEntity.Id;
            return post;
        }

        public async Task MarkPostDeleted(PokemonRaidPost post)
        {
            if(post.DbId != default(ulong))
            {
                var postEntity = await Posts.FirstOrDefaultAsync(x => x.Id == post.DbId);

                if(postEntity != null)
                {
                    postEntity.Deleted = true;
                    postEntity.EndDate = DateTime.Now;
                    await SaveChangesAsync();
                }
            }
        }

        public DbSet<DiscordServerEntity> Servers { get; set; }
        public DbSet<DiscordChannelEntity> Channels { get; set; }

        public DbSet<PokemonEntity> Pokemon { get; set; }
        public DbSet<RaidPostEntity> Posts { get; set; }
        public DbSet<RaidPostLocationEntity> Locations { get; set; }

        public DbSet<RaidPostChannelEntity> ChannelPosts { get; set; }
        //public DbSet<DiscordUserEntity> Users { get; set; }
    }
}
