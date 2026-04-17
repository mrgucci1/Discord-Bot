#!/usr/bin/env bash
# ------------------------------------------------------------------
# Discord-Bot — one-shot setup for Raspberry Pi / Debian / Ubuntu.
# Installs: .NET 8 SDK, FFmpeg, yt-dlp, Node.js + PM2
# Builds the bot, prompts for the Discord token, and starts it via PM2.
# ------------------------------------------------------------------
set -euo pipefail

GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; NC='\033[0m'
log()  { echo -e "${GREEN}==>${NC} $*"; }
warn() { echo -e "${YELLOW}!!${NC} $*"; }
die()  { echo -e "${RED}xx${NC} $*" >&2; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# ---- OS / arch detection -----------------------------------------
ARCH="$(uname -m)"
case "$ARCH" in
  aarch64|arm64) DOTNET_ARCH="arm64" ;;
  armv7l|armv6l) DOTNET_ARCH="arm"   ;;
  x86_64)        DOTNET_ARCH="x64"   ;;
  *) die "Unsupported architecture: $ARCH" ;;
esac
log "Detected architecture: $ARCH (dotnet: $DOTNET_ARCH)"

if ! command -v apt-get >/dev/null 2>&1; then
  die "This script targets Debian/Ubuntu/Raspberry Pi OS (apt-get required)."
fi

SUDO=""
if [ "$(id -u)" -ne 0 ]; then
  command -v sudo >/dev/null 2>&1 || die "sudo is required (or run as root)."
  SUDO="sudo"
fi

# ---- System packages ---------------------------------------------
log "Updating apt & installing base packages (ffmpeg, python3, curl, etc.)..."
$SUDO apt-get update -y
$SUDO apt-get install -y --no-install-recommends \
  ca-certificates curl wget gnupg lsb-release \
  ffmpeg python3 python3-pip

# ---- yt-dlp -------------------------------------------------------
if ! command -v yt-dlp >/dev/null 2>&1; then
  log "Installing yt-dlp (latest, via official binary)..."
  $SUDO curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp \
    -o /usr/local/bin/yt-dlp
  $SUDO chmod a+rx /usr/local/bin/yt-dlp
else
  log "yt-dlp already installed: $(yt-dlp --version)"
fi

# ---- .NET 8 SDK ---------------------------------------------------
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  log "Installing .NET 8 SDK via dotnet-install.sh..."
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  chmod +x /tmp/dotnet-install.sh
  /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet" --architecture "$DOTNET_ARCH"

  # Persist PATH + DOTNET_ROOT across login AND non-login shells.
  # (~/.profile is only read by login shells; most SSH sessions on Pi OS
  #  end up in a non-login interactive shell that reads ~/.bashrc.)
  for rc in "$HOME/.profile" "$HOME/.bashrc"; do
    [ -f "$rc" ] || touch "$rc"
    if ! grep -q 'DOTNET_ROOT' "$rc" 2>/dev/null; then
      {
        echo ''
        echo '# .NET SDK'
        echo 'export DOTNET_ROOT="$HOME/.dotnet"'
        echo 'export PATH="$DOTNET_ROOT:$PATH"'
      } >> "$rc"
    fi
  done
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$DOTNET_ROOT:$PATH"
else
  log ".NET 8 SDK already installed: $(dotnet --version)"
fi

# ---- Node.js + PM2 -----------------------------------------------
if ! command -v node >/dev/null 2>&1; then
  log "Installing Node.js LTS..."
  curl -fsSL https://deb.nodesource.com/setup_lts.x | $SUDO -E bash -
  $SUDO apt-get install -y nodejs
else
  log "Node.js already installed: $(node --version)"
fi

if ! command -v pm2 >/dev/null 2>&1; then
  log "Installing PM2 globally..."
  $SUDO npm install -g pm2
else
  log "PM2 already installed: $(pm2 --version)"
fi

# ---- Configure token ---------------------------------------------
if [ -z "${DISCORD_BOT_TOKEN:-}" ] && [ ! -f appsettings.json ]; then
  echo
  read -r -p "Paste your Discord bot token (leave blank to skip): " TOKEN_INPUT
  if [ -n "$TOKEN_INPUT" ]; then
    cat > appsettings.json <<EOF
{
  "BOT_TOKEN": "$TOKEN_INPUT"
}
EOF
    chmod 600 appsettings.json
    log "Wrote appsettings.json (chmod 600). It is gitignored."
  else
    warn "No token provided — set DISCORD_BOT_TOKEN before running the bot."
  fi
fi

# ---- Build --------------------------------------------------------
log "Publishing the bot (Release) to ./publish ..."
dotnet publish -c Release -o publish --nologo

mkdir -p logs

# ---- Start via PM2 ------------------------------------------------
log "Starting the bot under PM2..."
pm2 start ecosystem.config.js --update-env
pm2 save

echo
log "Done! Useful commands:"
echo "  pm2 status              # see the bot"
echo "  pm2 logs discord-bot    # tail logs"
echo "  pm2 restart discord-bot # restart"
echo "  pm2 stop discord-bot    # stop"
echo
warn "To start on boot, run:  pm2 startup   (follow the printed command)"
