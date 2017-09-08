using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using PokemonGoRaidBot.Configuration;
using System.IO;
using Microsoft.EntityFrameworkCore;
using PokemonGoRaidBot.Data;
using PokemonGoRaidBot.Services;
using PokemonGoRaidBot.Objects.Interfaces;
using PokemonGoRaidBot.Services.Discord;

namespace PokemonGoRaidBot
{
    public class Program
    {
        //public static void Main(string[] args) =>
        //    new Program().Start().GetAwaiter().GetResult();
        public static void Main(string[] args)
        {
            new Program().Start().GetAwaiter().GetResult();
        }

        private DiscordSocketClient client;
        private BotConfiguration config;
        private ConsoleLogger logger;

        public async Task Start()
        {
            EnsureBotConfigExists(); // Ensure that the bot configuration json file has been created.

            client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Verbose,
            });

            logger = new ConsoleLogger();

            client.Log += logger.Log;
            config = BotConfiguration.Load();

            await logger.Log("Startup", $"PokemonDiscordRaidBot: Configuration has been loaded.  Version {config.Version}.");
            
            await client.LoginAsync(TokenType.Bot, config.Token);
            await client.StartAsync();

            var serviceProvider = ConfigureServices();

            await EnsureDatabaseExists(serviceProvider);

            await logger.Log("Startup", $"PokemonDiscordRaidBot: Stat database exists.");

            var handler = serviceProvider.GetService<MessageHandler>();

            //handler = new CommandHandler(serviceProvider, config, logger);
            await handler.ConfigureAsync();
            
            await Task.Delay(5000);//let everything get started up before 

            //Block this program until it is closed
            while (1 == 1)
            {
                await handler.PurgePosts();
                await Task.Delay(60000);
            }
        }

        private async Task EnsureDatabaseExists(IServiceProvider provider)
        {
            var dbContext = provider.GetService<PokemonRaidBotDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        private static void EnsureBotConfigExists()
        {
            if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Configuration")))
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Configuration"));

            string loc = Path.Combine(AppContext.BaseDirectory, "Configuration/config.json");

            if (!File.Exists(loc))// Check if the configuration file exists.
            {
                var config = new BotConfiguration();// Create a new configuration object.

                Console.WriteLine("Please enter the following information to save into your Configuration/config.json file");

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

                Console.Write("Bot statistics sqlite DB connection string (blank for default): ");
                config.StatDBConnectionString = Console.ReadLine();//Read sqlite connection string from console

                if (string.IsNullOrWhiteSpace(config.DefaultLanguage))//not gonna bother with being too overly secure... shouldn't be storing anything sensitive
                    config.StatDBConnectionString = "Data Source=raidstats.db;"; 

                config.Save();//Save the new configuration object to file.
            }
        }
        
        public IServiceProvider ConfigureServices()
        {

            var services = new ServiceCollection()
                .AddDbContext<PokemonRaidBotDbContext>()
                .AddSingleton(client)
                .AddSingleton(config)
                .AddSingleton(logger)
                .AddSingleton<IConnectionString>(config)
                .AddSingleton<IStatMapper>(new StatMapper())
                .AddSingleton(new CommandService(new CommandServiceConfig { CaseSensitiveCommands = false }))
                .AddSingleton<MessageHandler>();
            
            var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
            
            return provider;
        }


    }
}
