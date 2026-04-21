using System.Collections.Concurrent;
using System.Diagnostics;
using Discord;
using Discord.Audio;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Services;

public sealed class TrackInfo
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public required string Duration { get; init; }
    public required string RequestedBy { get; init; }
}

public sealed class GuildMusicState
{
    public IAudioClient? AudioClient { get; set; }
    public IVoiceChannel? VoiceChannel { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    public ConcurrentQueue<TrackInfo> Queue { get; } = new();
    public TrackInfo? CurrentTrack { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsSkipping { get; set; }
    public bool IsSeeking { get; set; }
    public int SeekSeconds { get; set; }
}

public class MusicService
{
    private readonly ConcurrentDictionary<ulong, GuildMusicState> _guildStates = new();
    private readonly ILogger<MusicService> _logger;

    // Resolve executables from env vars (YTDLP_PATH / FFMPEG_PATH) or fall back to
    // the binary name, which relies on the system PATH. This keeps the bot portable
    // across Windows, Linux, macOS, and Raspberry Pi (ARM).
    private static readonly string YtDlpPath =
        Environment.GetEnvironmentVariable("YTDLP_PATH") ?? "yt-dlp";
    private static readonly string FfmpegPath =
        Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? "ffmpeg";
    // Optional: path to a Netscape-format cookies file to pass to yt-dlp.
    // Set YTDLP_COOKIES_PATH to avoid YouTube bot-detection blocks.
    private static readonly string? YtDlpCookiesArg =
        Environment.GetEnvironmentVariable("YTDLP_COOKIES_PATH") is { Length: > 0 } p
            ? $"--cookies \"{p}\""
            : null;

    public MusicService(ILogger<MusicService> logger)
    {
        _logger = logger;
    }

    private GuildMusicState GetOrCreateState(ulong guildId)
    {
        return _guildStates.GetOrAdd(guildId, _ => new GuildMusicState());
    }

    public TrackInfo? GetCurrentTrack(ulong guildId)
    {
        var state = GetOrCreateState(guildId);
        return state.CurrentTrack;
    }

    public IReadOnlyList<TrackInfo> GetQueue(ulong guildId)
    {
        var state = GetOrCreateState(guildId);
        return state.Queue.ToArray();
    }

    public async Task<TrackInfo?> PlayAsync(
        ulong guildId,
        IVoiceChannel voiceChannel,
        string query,
        string requestedBy)
    {
        var state = GetOrCreateState(guildId);

        var trackInfo = await GetTrackInfoAsync(query, requestedBy);
        if (trackInfo is null)
            return null;

        state.Queue.Enqueue(trackInfo);

        if (!state.IsPlaying)
        {
            state.VoiceChannel = voiceChannel;
            _ = Task.Run(async () =>
            {
                try
                {
                    await PlayQueueAsync(guildId, voiceChannel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in music playback for guild {GuildId}", guildId);
                }
            });
        }

        return trackInfo;
    }

    public bool Skip(ulong guildId)
    {
        var state = GetOrCreateState(guildId);
        if (!state.IsPlaying)
            return false;

        state.IsSkipping = true;
        state.CancellationTokenSource?.Cancel();
        return true;
    }

    public bool Seek(ulong guildId, int seconds)
    {
        var state = GetOrCreateState(guildId);
        if (!state.IsPlaying || state.CurrentTrack is null)
            return false;

        state.IsSeeking = true;
        state.SeekSeconds = seconds;
        state.CancellationTokenSource?.Cancel();
        return true;
    }

    public async Task StopAsync(ulong guildId)
    {
        var state = GetOrCreateState(guildId);

        // Clear the queue
        while (state.Queue.TryDequeue(out _)) { }

        state.CancellationTokenSource?.Cancel();
        state.IsPlaying = false;
        state.CurrentTrack = null;

        if (state.AudioClient is not null)
        {
            await state.AudioClient.StopAsync();
            state.AudioClient.Dispose();
            state.AudioClient = null;
        }
    }

    private async Task PlayQueueAsync(ulong guildId, IVoiceChannel voiceChannel)
    {
        var state = GetOrCreateState(guildId);

        try
        {
            if (state.AudioClient is null || state.AudioClient.ConnectionState != ConnectionState.Connected)
            {
                state.AudioClient = await voiceChannel.ConnectAsync(selfDeaf: true);
            }

            state.IsPlaying = true;

            while (state.Queue.TryDequeue(out var track))
            {
                state.CurrentTrack = track;
                state.IsSkipping = false;
                state.IsSeeking = false;
                state.SeekSeconds = 0;
                state.CancellationTokenSource = new CancellationTokenSource();

                _logger.LogInformation("Now playing: {Title}", track.Title);

                var seekOffset = 0;
                var keepPlaying = true;
                while (keepPlaying)
                {
                    try
                    {
                        await StreamAudioAsync(
                            state.AudioClient,
                            track.Url,
                            seekOffset,
                            state.CancellationTokenSource.Token);
                        keepPlaying = false;
                    }
                    catch (OperationCanceledException)
                    {
                        if (state.IsSeeking)
                        {
                            seekOffset = state.SeekSeconds;
                            state.IsSeeking = false;
                            state.CancellationTokenSource.Dispose();
                            state.CancellationTokenSource = new CancellationTokenSource();
                            _logger.LogInformation("Seeking to {Seconds}s in: {Title}", seekOffset, track.Title);
                        }
                        else
                        {
                            _logger.LogInformation("Track skipped/stopped: {Title}", track.Title);
                            keepPlaying = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error playing track: {Title}", track.Title);
                        keepPlaying = false;
                    }
                }

                state.CancellationTokenSource?.Dispose();
                state.CancellationTokenSource = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in play queue for guild {GuildId}", guildId);
        }
        finally
        {
            state.IsPlaying = false;
            state.CurrentTrack = null;

            if (state.AudioClient is not null)
            {
                try
                {
                    await state.AudioClient.StopAsync();
                    state.AudioClient.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disconnecting audio client");
                }
                state.AudioClient = null;
            }
        }
    }

    private async Task StreamAudioAsync(
        IAudioClient audioClient,
        string url,
        int seekSeconds,
        CancellationToken cancellationToken)
    {
        var cookies = YtDlpCookiesArg is not null ? $"{YtDlpCookiesArg} " : "";
        var ytdlpArgs = $"{cookies}-f bestaudio --no-playlist --no-warnings -o - \"{url}\"";
        if (seekSeconds > 0)
        {
            ytdlpArgs = $"{cookies}-f bestaudio --no-playlist --no-warnings --download-sections \"*{seekSeconds}-\" -o - \"{url}\"";
        }

        using var ytdlp = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = YtDlpPath,
                Arguments = ytdlpArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        using var ffmpeg = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                Arguments = "-hide_banner -loglevel warning -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        ytdlp.Start();
        ffmpeg.Start();

        // Log ffmpeg stderr in background
        _ = Task.Run(async () =>
        {
            var err = await ffmpeg.StandardError.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(err))
                _logger.LogWarning("ffmpeg stderr: {Error}", err);
        });

        // Log yt-dlp stderr in background
        _ = Task.Run(async () =>
        {
            var err = await ytdlp.StandardError.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(err))
                _logger.LogWarning("yt-dlp stderr: {Error}", err);
        });

        // Pipe yt-dlp stdout to ffmpeg stdin in background
        _ = Task.Run(async () =>
        {
            try
            {
                await ytdlp.StandardOutput.BaseStream.CopyToAsync(ffmpeg.StandardInput.BaseStream, cancellationToken);
            }
            catch (OperationCanceledException) { }
            finally
            {
                try { ffmpeg.StandardInput.Close(); } catch { }
            }
        });

        await using var discord = audioClient.CreatePCMStream(AudioApplication.Music);

        try
        {
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = await ffmpeg.StandardOutput.BaseStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await discord.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }
        finally
        {
            await discord.FlushAsync(CancellationToken.None);
            if (!ffmpeg.HasExited) ffmpeg.Kill();
            if (!ytdlp.HasExited) ytdlp.Kill();
        }
    }

    private async Task<TrackInfo?> GetTrackInfoAsync(string query, string requestedBy)
    {
        var searchQuery = query.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? query
            : $"ytsearch:{query}";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = YtDlpPath,
                Arguments = $"{YtDlpCookiesArg ?? ""}{(YtDlpCookiesArg is not null ? " " : "")}--print title --print webpage_url --print duration_string " +
                            $"--no-playlist --no-warnings -f bestaudio \"{searchQuery}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("yt-dlp failed with exit code {ExitCode}", process.ExitCode);
            return null;
        }

        var lines = output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3)
        {
            _logger.LogWarning("yt-dlp returned unexpected output: {Output}", output);
            return null;
        }

        return new TrackInfo
        {
            Title = lines[0].Trim(),
            Url = lines[1].Trim(),
            Duration = lines[2].Trim(),
            RequestedBy = requestedBy
        };
    }

    private static async Task<string> GetAudioStreamUrlAsync(string url)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = YtDlpPath,
                Arguments = $"-f bestaudio --get-url --no-playlist --no-warnings \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output.Trim();
    }

    private static Process StartFfmpeg(string audioUrl)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                Arguments = $"-hide_banner -loglevel panic -reconnect 1 -reconnect_streamed 1 " +
                            $"-reconnect_delay_max 5 -i \"{audioUrl}\" " +
                            $"-ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        return process;
    }
}
