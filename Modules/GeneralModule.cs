using Discord;
using Discord.Interactions;

namespace DiscordBot.Modules;

public class GeneralModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("spam", "Repeats a message 10 times")]
    public async Task SpamAsync(
        [Summary(description: "The text to repeat")] string text)
    {
        await DeferAsync();
        await FollowupAsync($"Spamming: **{text}**");

        for (int i = 0; i < 9; i++)
        {
            await Task.Delay(1000);
            await Context.Channel.SendMessageAsync(text);
        }
    }

    [SlashCommand("game", "Randomly picks a game from a comma-separated list")]
    public async Task ChooseGameAsync(
        [Summary(description: "Comma-separated list of games")] string games)
    {
        await DeferAsync();

        var options = games.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (options.Length == 0)
        {
            await FollowupAsync("Please provide at least one game option!");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🎮 Game Chooser")
            .WithDescription($"**Options:** {string.Join(", ", options)}")
            .WithColor(Color.Blue)
            .Build();

        await FollowupAsync(embed: embed);

        await Task.Delay(1500);
        await Context.Channel.SendMessageAsync("Choosing... 🎲");
        await Task.Delay(1500);
        await Context.Channel.SendMessageAsync("......................");
        await Task.Delay(1500);

        var choice = options[Random.Shared.Next(options.Length)];

        var resultEmbed = new EmbedBuilder()
            .WithTitle("🏆 The Game Is:")
            .WithDescription($"**{choice}**")
            .WithColor(Color.Gold)
            .Build();

        await Context.Channel.SendMessageAsync(embed: resultEmbed);
    }

    [SlashCommand("ping", "Check if the bot is alive")]
    public async Task PingAsync()
    {
        var latency = Context.Client.Latency;
        await RespondAsync($"🏓 Pong! Latency: **{latency}ms**");
    }

    [SlashCommand("info", "Show information about the bot")]
    public async Task InfoAsync()
    {
        var embed = new EmbedBuilder()
            .WithTitle("🤖 Discord Bot")
            .WithDescription("A modern Discord bot with music playback and fun commands.")
            .AddField("Framework", ".NET 8 + Discord.Net", inline: true)
            .AddField("Commands", "Use `/` to see all available commands", inline: true)
            .WithColor(Color.Purple)
            .WithFooter("Built with ❤️ using Discord.Net")
            .WithCurrentTimestamp()
            .Build();

        await RespondAsync(embed: embed);
    }
}
