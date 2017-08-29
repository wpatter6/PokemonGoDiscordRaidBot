using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using PokemonGoRaidBot.Config;
using System.IO;
using System.Collections.Generic;

namespace PokemonGoRaidBot
{
    public class Program
    {
        public static void Main(string[] args) =>
            new Program().Start().GetAwaiter().GetResult();

        private DiscordSocketClient client;
        private CommandHandler handler;

        public async Task Start()
        {
            EnsureBotConfigExists(); // Ensure that the bot configuration json file has been created.

            client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Verbose,
            });

            var logger = new RaidLogger();

            client.Log += logger.Log;
            var config = BotConfig.Load();

            await logger.Log("Startup", $"PokemonDiscordRaidBot: Configuration has been loaded.  version {config.Version}.  Copyright 2017 wpatter6.");
            await client.LoginAsync(TokenType.Bot, config.Token);
            await client.StartAsync();

            var serviceProvider = ConfigureServices();
            handler = new CommandHandler(serviceProvider, config, logger);
            await handler.ConfigureAsync();

            await Task.Delay(5000);
            //Block this program until it is closed
            while (1 == 1)
            {
                handler.PurgePosts();
                await Task.Delay(60000);
            }
        }
        

        public static void EnsureBotConfigExists()
        {
            if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "configuration")))
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "configuration"));

            string loc = Path.Combine(AppContext.BaseDirectory, "configuration/config.json");

            if (!File.Exists(loc))                              // Check if the configuration file exists.
            {
                var config = new BotConfig();               // Create a new configuration object.

                Console.WriteLine("Please enter the following information to save into your configuration/config.json file");

                Console.Write("Bot Token: ");
                config.Token = Console.ReadLine();//Read the bot token from console.

                Console.Write("Google Geocoding Api Key: ");
                config.GoogleApiKey = Console.ReadLine();//Read google API key from console

                Console.Write("Bot Command Prefix (blank for !): ");
                config.Prefix = Console.ReadLine();//Read the bot prefix from console.
                if (string.IsNullOrWhiteSpace(config.Prefix)) config.Prefix = "!";

                Console.Write("Bot Default Output Channel Name (blank for raid-bot): ");
                config.OutputChannel = Console.ReadLine();//Read output channel name from console
                if (string.IsNullOrWhiteSpace(config.OutputChannel)) config.OutputChannel = "raid-bot";

                Console.Write("Bot Default Language (blank for en-us): ");
                config.DefaultLanguage = Console.ReadLine();//Read output channel name from console
                if (string.IsNullOrWhiteSpace(config.DefaultLanguage)) config.DefaultLanguage = "en-us";

                config.Save();//Save the new configuration object to file.
            }
        }




        public IServiceProvider ConfigureServices()
        {

            var services = new ServiceCollection()
                .AddSingleton(client)
                 .AddSingleton(new CommandService(new CommandServiceConfig { CaseSensitiveCommands = false }));
            var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);




            return provider;
        }


    }
}
