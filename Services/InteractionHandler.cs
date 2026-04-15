using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Services;

public class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly ILogger<InteractionHandler> _logger;

    public InteractionHandler(
        DiscordSocketClient client,
        InteractionService interactions,
        IServiceProvider services,
        ILogger<InteractionHandler> logger)
    {
        _client = client;
        _interactions = interactions;
        _services = services;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        _client.InteractionCreated += HandleInteractionAsync;
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        var context = new SocketInteractionContext(_client, interaction);
        var result = await _interactions.ExecuteCommandAsync(context, _services);

        if (!result.IsSuccess)
        {
            _logger.LogError("Interaction error: {ErrorReason}", result.ErrorReason);

            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                if (!interaction.HasResponded)
                {
                    await interaction.RespondAsync(
                        $"An error occurred: {result.ErrorReason}",
                        ephemeral: true);
                }
                else
                {
                    await interaction.FollowupAsync(
                        $"An error occurred: {result.ErrorReason}",
                        ephemeral: true);
                }
            }
        }
    }
}
