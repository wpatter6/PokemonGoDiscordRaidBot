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
        private List<string> GoodStringsEnglish = new List<string>(new string[] 
        {
            "Raikou at 17th/stout, looks like I am only one here",
            "Raikou hatches at Potager (11th/Ogden) in 58 min. Anyone up for 3:30 raid there?",
            "Tyranitar at Broadway and 18th",
            "ttar at 3rd and Broadwäy",
            "Tyranitar 5th and Bannock. By Denver Health. 46 min. I know it's super early",
            "Raikou at Dayton and Girard.  Moon Rock.  36 minutes left.  Need one more.",

        });
        private List<string> BadStringsEnglish = new List<string>(new string[]
        {
            "Raikou is cool, google.com/maps",
            "BLAHBLAHBLAH",
        });


        private List<string> GoodStringsDutch = new List<string>(new string[]
        {
            "Ttar in Jlianapark nog 56 minuten",
            "Snorlax naast Centraal Station 20 min resterend",
            "Lapras bij Longboat nog 76 mins",
            "Magikarp bij de Dom 2 en een half uur resterend",
            "Tyranitar in Julianapark 1 uur en 20 minuten resterend",
            "Tyranitar at Centraal Station nog 30 minuten",
        });

        private List<string> BadStringsDutch = new List<string>(new string[]
        {
            "Raikou is cool, google.com/maps",
            "BLAHBLAHBLAH",
        });

        #region en-US
        [TestMethod]
        public void TestGoodStringsEnglish()
        {
            var parser = GetParser();
            foreach (var str in GoodStringsEnglish)
            {
                var post = parser.ParsePost(new MockedChatMessage(str, Objects.ChatTypes.Discord));

                if(!post.IsValid)
                    Assert.IsTrue(post.IsValid);
            }
        }

        [TestMethod]
        public void TestBadStringsEnglish()
        {
            var parser = GetParser();
            foreach (var str in BadStringsEnglish)
            {
                var post = parser.ParsePost(new MockedChatMessage(str, Objects.ChatTypes.Discord));

                if (post.IsValid)
                    Assert.IsFalse(post.IsValid);
            }
        }
        #endregion

        #region nl-NL
        [TestMethod]
        public void TestGoodStringsDutch()
        {
            var parser = GetParser("nl-NL");
            foreach (var str in GoodStringsDutch)
            {
                var post = parser.ParsePost(new MockedChatMessage(str, Objects.ChatTypes.Discord));

                if (!post.IsValid)
                    Assert.IsTrue(post.IsValid);
            }
        }

        [TestMethod]
        public void TestBadStringsDutch()
        {
            var parser = GetParser("nl-NL");
            foreach (var str in BadStringsDutch)
            {
                var post = parser.ParsePost(new MockedChatMessage(str, Objects.ChatTypes.Discord));

                if (post.IsValid)
                    Assert.IsFalse(post.IsValid);
            }
        }
        #endregion

        #region util
        private MessageParser GetParser(string language)
        {
            return GetParser(new MockedBotServerConfiguration()
            {
                Language = language
            });
        }

        private MessageParser GetParser(MockedBotServerConfiguration serverConfig = null)
        {
            var parser = new MessageParser(serverConfig ?? new MockedBotServerConfiguration(), serverConfig?.Language ?? "en-us", 0);

            return parser;
        }
        #endregion
    }
}
