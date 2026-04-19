# Discord Bot

A modern Discord bot built with **.NET 8** and **Discord.Net** featuring slash commands and YouTube music playback.

## Features

- **Slash Commands** — Uses Discord's modern interaction system (no prefix needed, just type `/`)
- **Music Playback** — Play music from YouTube in voice channels
  - `/play <query or URL>` — Search YouTube or provide a direct URL to play
  - `/skip` — Skip the current track
  - `/stop` — Stop playback and clear the queue
  - `/queue` — View the current music queue
  - `/nowplaying` — Show the currently playing track
  - `/seek <seconds>` — Skip ahead in the current track by a number of seconds
- **Fun Commands**
  - `/game <game1, game2, ...>` — Randomly pick a game from a comma-separated list
  - `/spam <text>` — Repeat a message 10 times
  - `/ping` — Check bot latency
  - `/info` — Show bot information

---

## Quick Start (Raspberry Pi / Debian / Ubuntu) — one command

If you're on a Raspberry Pi or any Debian/Ubuntu machine, a single script
installs every prerequisite (.NET 8 SDK, FFmpeg, yt-dlp, Node.js, PM2),
prompts for your Discord bot token, builds the bot, and launches it under
[PM2](https://pm2.keymetrics.io/) so it restarts on crash and on reboot:

```bash
git clone https://github.com/mrgucci1/Discord-Bot.git
cd Discord-Bot
chmod +x setup.sh
./setup.sh
```

> **Recommended OS**: Ubuntu Server 24.04 LTS (64-bit) — required for Discord DAVE voice encryption (needs glibc ≥ 2.38).

That's it. When it finishes:

```bash
pm2 status                # is it running?
pm2 logs discord-bot      # tail the logs
pm2 restart discord-bot   # restart after code changes
pm2 stop discord-bot      # stop the bot
pm2 startup               # run the printed command to auto-start on boot
pm2 save                  # persist the running app list
```

**Supported hardware** (script auto-detects architecture):
- Raspberry Pi 3 / 4 / 5 (64-bit Ubuntu Server 24.04 recommended) — `arm64`
- Raspberry Pi Zero 2 W (64-bit OS) — `arm64`
- Any x86_64 Debian/Ubuntu server or desktop

**Updating later:**

```bash
git pull
dotnet publish -c Release -o publish
pm2 restart discord-bot --update-env
```

If you'd prefer to install the prerequisites yourself, see the
[manual setup](#prerequisites) below.

---

## Prerequisites

Before you can run the bot, you need the following installed on your system:

### 1. .NET 8 SDK

Download and install from [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0).

Verify the installation:

```bash
dotnet --version
```

### 2. FFmpeg (required for music)

FFmpeg is used to transcode audio streams for Discord voice channels.

- **Windows**: Download from [https://ffmpeg.org/download.html](https://ffmpeg.org/download.html) and add to your system PATH.
- **macOS**: `brew install ffmpeg`
- **Ubuntu/Debian**: `sudo apt update && sudo apt install ffmpeg`

Verify the installation:

```bash
ffmpeg -version
```

### 3. yt-dlp (required for music)

yt-dlp is used to search and fetch audio from YouTube.

- **All platforms (pip)**: `pip install yt-dlp`
- **Windows (Scoop)**: `scoop install yt-dlp`
- **macOS**: `brew install yt-dlp`
- **Ubuntu/Debian**: `sudo apt install yt-dlp` (or install via pip for the latest version)

Verify the installation:

```bash
yt-dlp --version
```

### 4. Discord Bot Token

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications).
2. Click **New Application** and give it a name.
3. Go to the **Bot** tab and click **Add Bot**.
4. Click **Reset Token** and copy the token — you'll need it later.
5. Under **Privileged Gateway Intents**, enable:
   - **Message Content Intent**
   - **Server Members Intent** (optional)
6. Go to the **OAuth2 → URL Generator** tab:
   - Under **Scopes**, select `bot` and `applications.commands`.
   - Under **Bot Permissions**, select:
     - Send Messages
     - Embed Links
     - Connect (voice)
     - Speak (voice)
     - Use Slash Commands
   - Copy the generated URL and open it in your browser to invite the bot to your server.

---

## Setup & Running

### 1. Clone the Repository

```bash
git clone https://github.com/mrgucci1/Discord-Bot.git
cd Discord-Bot
```

### 2. Configure the Bot Token

You can provide the bot token in one of two ways:

**Option A: Environment Variable (recommended)**

```bash
# Linux / macOS
export DISCORD_BOT_TOKEN="your-bot-token-here"

# Windows (Command Prompt)
set DISCORD_BOT_TOKEN=your-bot-token-here

# Windows (PowerShell)
$env:DISCORD_BOT_TOKEN = "your-bot-token-here"
```

**Option B: Configuration File**

Create an `appsettings.json` file in the project root:

```json
{
  "BOT_TOKEN": "your-bot-token-here"
}
```

> ⚠️ **Important**: Never commit your bot token to version control. The `appsettings.json` approach is fine for local development but use environment variables for production.

### 3. Restore Dependencies & Build

```bash
dotnet restore
dotnet build
```

### 4. Run the Bot

```bash
dotnet run
```

You should see output like:

```
Discord.Net v3.x (API v10)
Bot is ready! Registering slash commands...
Slash commands registered.
```

> **Note**: Slash commands may take up to an hour to appear globally in Discord. For instant updates during development, you can modify the code to register commands to a specific guild instead.

---

## Hosting in Production

### Option 1: PM2 (recommended — works great on a Raspberry Pi)

PM2 is a Node.js-based process manager that keeps the bot alive across
crashes and reboots. The repo ships a ready-to-use `ecosystem.config.js`.

```bash
# One-time (only if you didn't use setup.sh)
sudo npm install -g pm2

# Build a release bundle
dotnet publish -c Release -o publish

# Start under PM2
pm2 start ecosystem.config.js
pm2 save
pm2 startup     # follow the printed command to enable auto-start on boot
```

Logs go to `./logs/out.log` and `./logs/err.log`. Common commands:

```bash
pm2 status
pm2 logs discord-bot
pm2 restart discord-bot --update-env   # pick up new env vars / new build
pm2 stop discord-bot
pm2 delete discord-bot
```

### Option 2: Run as a systemd Service (Linux)

Create a service file at `/etc/systemd/system/discord-bot.service`:

```ini
[Unit]
Description=Discord Bot
After=network.target

[Service]
Type=simple
User=youruser
WorkingDirectory=/path/to/Discord-Bot
ExecStart=/usr/bin/dotnet run --project /path/to/Discord-Bot/Discord-Bot.csproj
Environment=DISCORD_BOT_TOKEN=your-bot-token-here
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

Then enable and start:

```bash
sudo systemctl enable discord-bot
sudo systemctl start discord-bot
sudo systemctl status discord-bot
```

### Option 3: Docker

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
RUN apt-get update && apt-get install -y ffmpeg python3-pip && pip3 install yt-dlp --break-system-packages
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Discord-Bot.dll"]
```

Build and run:

```bash
docker build -t discord-bot .
docker run -d -e DISCORD_BOT_TOKEN=your-bot-token-here --name discord-bot discord-bot
```

### Option 4: Cloud Hosting

The bot can run on any cloud provider that supports .NET 8:

- **Azure App Service** or **Azure Container Apps**
- **AWS EC2** or **AWS ECS**
- **Google Cloud Run** or **GCE**
- **Railway**, **Render**, **Fly.io**

Set the `DISCORD_BOT_TOKEN` environment variable in your hosting provider's configuration.

---

## Project Structure

```
Discord-Bot/
├── Discord-Bot.csproj        # Project file with dependencies
├── Program.cs                # Application entry point and DI setup
├── Modules/
│   ├── GeneralModule.cs      # Fun/utility slash commands
│   └── MusicModule.cs        # Music playback slash commands
├── Services/
│   ├── InteractionHandler.cs # Slash command routing
│   └── MusicService.cs       # YouTube audio streaming logic
├── ecosystem.config.js       # PM2 process config (production hosting)
├── setup.sh                  # One-shot installer for Pi / Debian / Ubuntu
├── README.md
└── .gitignore
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Slash commands don't appear | Wait up to 1 hour for global registration, or register per-guild for instant updates |
| Bot can't play music | Ensure `ffmpeg` and `yt-dlp` are installed and in your system PATH |
| `ffmpeg` / `yt-dlp` not on PATH | Set `FFMPEG_PATH` and/or `YTDLP_PATH` env vars to the absolute binary paths |
| "Bot token not found" error | Set the `DISCORD_BOT_TOKEN` environment variable or create `appsettings.json` |
| Bot doesn't join voice channel | Ensure the bot has **Connect** and **Speak** permissions in the voice channel |
| Audio cuts out or stutters | Check your network connection and server resources; the bot needs a stable connection |
| PM2 doesn't restart on reboot | Run `pm2 startup` and follow the printed command, then `pm2 save` |
| Raspberry Pi build fails | Make sure you're on 64-bit OS for Pi 3/4/5 |
| DAVE voice encryption fails | Use Ubuntu Server 24.04+ (glibc ≥ 2.38). Pi OS Bookworm's glibc 2.36 is too old. |
| PM2 says "Script not found: dotnet" | The ecosystem.config.js resolves `~/.dotnet/dotnet` automatically. Run `pm2 delete discord-bot && pm2 start ecosystem.config.js && pm2 save`. |

## License

This project is provided as-is for educational and personal use.
