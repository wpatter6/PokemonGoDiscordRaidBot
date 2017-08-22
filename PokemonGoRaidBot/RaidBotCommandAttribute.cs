using System;

namespace PokemonGoRaidBot
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal class RaidBotCommandAttribute : Attribute
    {
        public readonly string Command;
        public RaidBotCommandAttribute (string command)
        {
            Command = command;
        }
    }
}