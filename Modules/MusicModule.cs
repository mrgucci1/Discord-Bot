using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;

namespace DiscordBot.Modules;

public class MusicModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly MusicService _music;

    public MusicModule(MusicService music)
    {
        _music = music;
    }

    [SlashCommand("play", "Play a song from YouTube (search or URL)")]
    public async Task PlayAsync(
        [Summary(description: "YouTube URL or search query")] string query)
    {
        var voiceChannel = (Context.User as SocketGuildUser)?.VoiceChannel;
        if (voiceChannel is null)
        {
            await RespondAsync("❌ You must be in a voice channel to use this command!", ephemeral: true);
            return;
        }

        await DeferAsync();

        var track = await _music.PlayAsync(
            Context.Guild.Id,
            voiceChannel,
            query,
            Context.User.Username);

        if (track is null)
        {
            await FollowupAsync("❌ Could not find or play that track. Make sure `yt-dlp` and `ffmpeg` are installed.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(_music.GetQueue(Context.Guild.Id).Count > 0 ? "📋 Added to Queue" : "🎵 Now Playing")
            .WithDescription($"[{track.Title}]({track.Url})")
            .AddField("Duration", track.Duration, inline: true)
            .AddField("Requested By", track.RequestedBy, inline: true)
            .WithColor(Color.Green)
            .Build();

        await FollowupAsync(embed: embed);
    }

    [SlashCommand("skip", "Skip the currently playing song")]
    public async Task SkipAsync()
    {
        var currentTrack = _music.GetCurrentTrack(Context.Guild.Id);
        if (currentTrack is null)
        {
            await RespondAsync("❌ Nothing is currently playing!", ephemeral: true);
            return;
        }

        var skipped = _music.Skip(Context.Guild.Id);
        if (skipped)
        {
            await RespondAsync($"⏭️ Skipped **{currentTrack.Title}**");
        }
        else
        {
            await RespondAsync("❌ Could not skip the current track.", ephemeral: true);
        }
    }

    [SlashCommand("stop", "Stop playback and clear the queue")]
    public async Task StopAsync()
    {
        await DeferAsync();
        await _music.StopAsync(Context.Guild.Id);
        await FollowupAsync("⏹️ Stopped playback and cleared the queue.");
    }

    [SlashCommand("queue", "Show the current music queue")]
    public async Task QueueAsync()
    {
        var currentTrack = _music.GetCurrentTrack(Context.Guild.Id);
        var queue = _music.GetQueue(Context.Guild.Id);

        if (currentTrack is null && queue.Count == 0)
        {
            await RespondAsync("📭 The queue is empty. Use `/play` to add songs!", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🎶 Music Queue")
            .WithColor(Color.Blue);

        if (currentTrack is not null)
        {
            embed.AddField("Now Playing", $"**{currentTrack.Title}** ({currentTrack.Duration})");
        }

        if (queue.Count > 0)
        {
            var queueList = string.Join("\n",
                queue.Select((t, i) => $"`{i + 1}.` **{t.Title}** ({t.Duration}) — requested by {t.RequestedBy}"));
            embed.AddField($"Up Next ({queue.Count} track{(queue.Count != 1 ? "s" : "")})", queueList);
        }

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("nowplaying", "Show the currently playing track")]
    public async Task NowPlayingAsync()
    {
        var track = _music.GetCurrentTrack(Context.Guild.Id);

        if (track is null)
        {
            await RespondAsync("🔇 Nothing is currently playing.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🎵 Now Playing")
            .WithDescription($"[{track.Title}]({track.Url})")
            .AddField("Duration", track.Duration, inline: true)
            .AddField("Requested By", track.RequestedBy, inline: true)
            .WithColor(Color.Teal)
            .Build();

        await RespondAsync(embed: embed);
    }
}
