using Discord;
using System;
using System.Threading.Tasks;

namespace PokemonGoRaidBot.Services
{
    public class ConsoleLogger
    {
        public Task Log(string src, string msg)
        {
            return Log(new LogMessage(LogSeverity.Info, src, msg));
        }
        public Task Log(LogMessage lmsg)
        {
            var cc = Console.ForegroundColor;
            switch (lmsg.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }
            var message = lmsg.Exception != null ? lmsg.Exception.Message : lmsg.Message;
            Console.WriteLine($"{DateTime.Now} [{lmsg.Severity,8}] {lmsg.Source}: {message}");
            Console.ForegroundColor = cc;
            return Task.CompletedTask;
        }
    }
}
