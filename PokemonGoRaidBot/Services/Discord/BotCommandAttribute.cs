using System;

namespace PokemonGoRaidBot.Services.Discord
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal class BotCommandAttribute : Attribute
    {
        public readonly string Command;
        public BotCommandAttribute (string command)
        {
            Command = command;
        }
    }
}