using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace Discord_Bot___Bob
{
    class Program
    {
        static void Main(string[] args) => new Program().RunBotAsync().GetAwaiter().GetResult();

        private DiscordSocketClient disClient;
        private CommandService disCommands;
        private IServiceProvider disServices;

        public async Task RunBotAsync()
        {
            disClient = new DiscordSocketClient();
            disCommands = new CommandService();
            disServices = new ServiceCollection()
                .AddSingleton(disClient)
                .AddSingleton(disCommands)
                .BuildServiceProvider();

            string botToken = "";

            disClient.Log += DisClient_Log;

            await RegisterCommandsAsync();

            await disClient.LoginAsync(TokenType.Bot, botToken);

            await disClient.StartAsync();

            await Task.Delay(-1);
        }

        private Task DisClient_Log(LogMessage arg)
        {
            Console.WriteLine(arg);
            return Task.CompletedTask;
        }

        public async Task RegisterCommandsAsync()
        {
            disClient.MessageReceived += HandleCommandAsync;
            await disCommands.AddModulesAsync(Assembly.GetEntryAssembly(), disServices);
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            var context = new SocketCommandContext(disClient, message);
            if (message.Author.IsBot) return;

            int argPos = 0;
            if (message.HasStringPrefix(".", ref argPos))
            {
                var result = await disCommands.ExecuteAsync(context, argPos, disServices);
                if (!result.IsSuccess) Console.WriteLine(result.ErrorReason);

            }

        }
    }

    

}
