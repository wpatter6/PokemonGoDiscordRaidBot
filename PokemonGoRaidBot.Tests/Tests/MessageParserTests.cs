using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using PokemonGoRaidBot.Tests.MockedObjects;
using PokemonGoRaidBot.Services.Parsing;
using System.IO;
using System;

namespace PokemonGoRaidBot.Tests
{
    [TestClass]
    public class MessageParserTests
    {
        private List<string> GoodStrings = new List<string>(new string[] 
        {
            "Raikou at 17th/stout, looks like I am only one here",
            "Raikou hatches at Potager (11th/Ogden) in 58 min. Anyone up for 3:30 raid there?",
            "Tyranitar at Broadway and 18th",
            "ttar at 3rd and Broadwäy",
            "Tyranitar 5th and Bannock. By Denver Health. 46 min. I know it's super early",
            "Raikou at Dayton and Girard.  Moon Rock.  36 minutes left.  Need one more."

        });
        private List<string> BadStrings = new List<string>(new string[] 
        {
            "BLAHBLAHBLAH"
        });

        [TestMethod]
        public void TestBadStrings()
        {
            var parser = GetParser();
            foreach (var str in BadStrings)
            {
                var post = parser.ParsePost(new MockedChatMessage(str, Objects.ChatTypes.Discord));

                Assert.IsFalse(post.IsValid);
            }
        }

        [TestMethod]
        public void TestGoodStrings()
        {
            var parser = GetParser();
            foreach (var str in GoodStrings)
            {
                var post = parser.ParsePost(new MockedChatMessage(str, Objects.ChatTypes.Discord));

                Assert.IsTrue(post.IsValid);
            }
        }

        private MessageParser GetParser(MockedBotServerConfiguration serverConfig = null)
        {
            if (serverConfig == null) serverConfig = new MockedBotServerConfiguration();
            var languageFilePath = Path.Combine(AppContext.BaseDirectory, string.Format(@"..\PokemonGoRaidBot\Configuration\Languages\{0}.json", serverConfig.Language ?? "en-us"));

            var parser = new MessageParser(serverConfig, serverConfig.Language ?? "en-us", 0, languageFilePath);

            return parser;
        }
    }
}
