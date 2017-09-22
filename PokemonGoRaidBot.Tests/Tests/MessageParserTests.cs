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
        private List<string> GoodTests = new List<string>(new string[] 
        {
            "Raikou hatches at Potager (11th/Ogden) in 58 min. Anyone up for 3:30 raid there?",
            "@Tyranitar at Broadway and 18th"

        });
        private List<string> BadTests = new List<string>(new string[] 
        {
        });


        [TestMethod]
        public void TestMethod1()
        {
            var parser = GetParser();

            var message = new MockedChatMessage("BLAHBLAHBLAH", Objects.ChatTypes.Discord);

            var config = new MockedBotConfiguration();

            var post = parser.ParsePost(message, config);

            Assert.IsNotNull(post);
        }

        private MessageParser GetParser(string language = "en-us")
        {
            var languageFilePath = Path.Combine(AppContext.BaseDirectory, string.Format(@"..\PokemonGoRaidBot\Configuration\Languages\{0}.json", "en-us"));

            var parser = new MessageParser("en-us", 0, languageFilePath);
            
            return parser;
        }
    }
}
