using System;

namespace PokemonGoRaidBot
{
    [AttributeUsage(AttributeTargets.Class |
       AttributeTargets.Constructor |
       AttributeTargets.Field |
       AttributeTargets.Method |
       AttributeTargets.Property,
       AllowMultiple = false)]
    internal class RaidBotCommandAttribute : Attribute
    {
        public readonly string Command;
        public RaidBotCommandAttribute (string command)
        {
            Command = command;
        }
    }
}