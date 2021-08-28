using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Bot.Services;
using Discord.Net;
using Newtonsoft.Json;
using System.Linq;

namespace Bot
{
    class Program
    {
        private DiscordSocketClient _client;

        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            // You should dispose a service provider created using ASP.NET
            // when you are finished using it, at the end of your app's lifetime.
            // If you use another dependency injection framework, you should inspect
            // its documentation for the best way to do this.
            using var services = ConfigureServices();

            _client = services.GetRequiredService<DiscordSocketClient>();

            _client.Log += LogAsync;
            _client.Ready += Client_Ready;
            services.GetRequiredService<CommandService>().Log += LogAsync;

            // Grab the token from our environment variable
            var token = Environment.GetEnvironmentVariable("Token");
            if (token == null)
            {
                Console.Write("No token found.");
                await Task.Delay(5000);
                return;
            }

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Here we initialize the logic required to register our commands.
            await services.GetRequiredService<CommandHandlingService>().InitializeAsync();
            await services.GetRequiredService<ActionService>().InitializeAsync();
            services.GetRequiredService<RequestContextService>().Initialize();

            await Task.Delay(Timeout.Infinite);
        }

        private Task Client_Ready()
        {
            _client.Ready -= Client_Ready;
			
			//

            return Task.CompletedTask;
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }

        private ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig()
                {
                    AlwaysAcknowledgeInteractions = false,
                    GatewayIntents = GatewayIntents.All,
                    AlwaysDownloadUsers = true
                }))
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<ActionService>()
                .AddSingleton<HttpClient>()
                .AddScoped<RequestContextService>()
                .BuildServiceProvider();
        }
    }
}
