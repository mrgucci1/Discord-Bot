using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordBot;

public class Program
{
    public static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "DISCORD_")
            .Build();

        var botToken = configuration["BOT_TOKEN"]
            ?? Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN")
            ?? throw new InvalidOperationException(
                "Bot token not found. Set the DISCORD_BOT_TOKEN environment variable "
                + "or add BOT_TOKEN to appsettings.json.");

        var socketConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
                | GatewayIntents.GuildMessages
                | GatewayIntents.GuildVoiceStates
                | GatewayIntents.MessageContent,
            LogLevel = LogSeverity.Info
        };

        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole())
            .AddSingleton(configuration)
            .AddSingleton(socketConfig)
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton<InteractionService>(sp =>
                new InteractionService(sp.GetRequiredService<DiscordSocketClient>()))
            .AddSingleton<InteractionHandler>()
            .AddSingleton<MusicService>()
            .BuildServiceProvider();

        var client = services.GetRequiredService<DiscordSocketClient>();
        var interactionService = services.GetRequiredService<InteractionService>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        client.Log += msg =>
        {
            logger.LogInformation("{Message}", msg.ToString());
            return Task.CompletedTask;
        };

        interactionService.Log += msg =>
        {
            logger.LogInformation("{Message}", msg.ToString());
            return Task.CompletedTask;
        };

        await services.GetRequiredService<InteractionHandler>().InitializeAsync();

        client.Ready += async () =>
        {
            logger.LogInformation("Bot is ready! Registering slash commands...");
            await interactionService.RegisterCommandsGloballyAsync();
            logger.LogInformation("Slash commands registered.");
        };

        await client.LoginAsync(TokenType.Bot, botToken);
        await client.StartAsync();

        await Task.Delay(Timeout.Infinite);
    }
}
