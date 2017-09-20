using System;
using Slackbot;
using System.Collections.Generic;
using System.Text;
using PokemonGoRaidBot.Objects.Interfaces;
using PokemonGoRaidBot.Configuration;
using PokemonGoRaidBot.Data.Entities;
using PokemonGoRaidBot.Objects;
using PokemonGoRaidBot.Services.Parsing;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Services.Slack
{
    public class SlackMessageHandler : IChatMessageHandler
    {
        public SlackMessageHandler(IServiceProvider provider)
        {

        }

        public BotConfiguration Config => throw new NotImplementedException();

        public PokemonRaidPost AddPost(PokemonRaidPost post, MessageParser parser, IChatMessage message, bool add = true, bool force = false)
        {
            throw new NotImplementedException();
        }

        public Task ConfigureAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeletePost(PokemonRaidPost post, ulong userId, bool isAdmin, bool purge = true)
        {
            throw new NotImplementedException();
        }

        public void DoError(Exception e, string source = "Handler")
        {
            throw new NotImplementedException();
        }

        public Task DoPost(PokemonRaidPost post, IChatMessage message, MessageParser parser, IChatChannel outputchannel, bool force = false)
        {
            throw new NotImplementedException();
        }

        public List<IGrouping<PokemonEntity, RaidPostEntity>> GetBossAggregates(int count = 5, Expression<Func<RaidPostEntity, bool>> where = null)
        {
            throw new NotImplementedException();
        }

        public int GetPostCount(int days, ulong serverId)
        {
            throw new NotImplementedException();
        }

        public Task MakeCommandMessage(IChatChannel channel, string message)
        {
            throw new NotImplementedException();
        }

        public Task<List<IChatMessage>> MakePost(PokemonRaidPost post, MessageParser parser)
        {
            throw new NotImplementedException();
        }

        public PokemonRaidPost MergePosts(PokemonRaidPost post1, PokemonRaidPost post2)
        {
            throw new NotImplementedException();
        }

        public Task PurgePosts()
        {
            throw new NotImplementedException();
        }
    }
}
