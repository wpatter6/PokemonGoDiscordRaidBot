using PokemonGoRaidBot.Configuration;
using PokemonGoRaidBot.Data.Entities;
using PokemonGoRaidBot.Services.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Objects.Interfaces
{
    public interface IChatMessageHandler
    {
        Task ConfigureAsync();
        Task PurgePosts();
        
        Task DoPost(PokemonRaidPost post, IChatMessage message, MessageParser parser, IChatChannel outputchannel, bool force = false);
        Task<List<IChatMessage>> MakePost(PokemonRaidPost post, MessageParser parser);
        Task<bool> DeletePost(PokemonRaidPost post, ulong userId, bool isAdmin, bool purge = true);
        Task MakeCommandMessage(IChatChannel channel, string message);
        List<IGrouping<PokemonEntity, RaidPostEntity>> GetBossAggregates(int count = 5, Expression<Func<RaidPostEntity, bool>> where = null);
        void DoError(Exception e, string source = "Handler");

        PokemonRaidPost MergePosts(PokemonRaidPost post1, PokemonRaidPost post2);
        int GetPostCount(int days, ulong serverId);

        PokemonRaidPost AddPost(PokemonRaidPost post, MessageParser parser, IChatMessage message, bool add = true, bool force = false);

        BotConfiguration Config { get; }
    }
}
