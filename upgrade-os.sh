#!/usr/bin/env bash
# ------------------------------------------------------------------
# Upgrade Raspberry Pi OS (or Debian) from bookworm -> trixie so that
# the native `libdave.so` shipped with Discord.Net can load (it needs
# glibc >= 2.38 and GLIBCXX_3.4.32, which only exist on Debian 13+).
#
# After the upgrade, the bot is rebuilt and restarted under PM2 with
# DAVE voice encryption enabled.
#
# This is an IN-PLACE upgrade: your files, users, SSH keys, PM2 apps,
# and appsettings.json are preserved. It is NOT a wipe. BUT distro
# upgrades can still fail on flaky power / bad SD cards. Back up any
# irreplaceable data first (e.g. clone the SD card with `rpi-clone`
# or `dd`, or `rsync` /home to another machine).
# ------------------------------------------------------------------
set -euo pipefail

GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; NC='\033[0m'
log()  { echo -e "${GREEN}==>${NC} $*"; }
warn() { echo -e "${YELLOW}!!${NC} $*"; }
die()  { echo -e "${RED}xx${NC} $*" >&2; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

[ "$(id -u)" -eq 0 ] && die "Run as your normal user; the script will sudo when needed."
command -v sudo >/dev/null 2>&1 || die "sudo is required."

# ---- Detect current codename -------------------------------------
CURRENT_CODENAME="$(. /etc/os-release && echo "${VERSION_CODENAME:-unknown}")"
log "Current OS codename: $CURRENT_CODENAME"

if [ "$CURRENT_CODENAME" = "trixie" ] || [ "$CURRENT_CODENAME" = "forky" ]; then
  log "Already on trixie or newer; no OS upgrade needed."
  SKIP_UPGRADE=1
elif [ "$CURRENT_CODENAME" != "bookworm" ]; then
  die "Unsupported starting codename '$CURRENT_CODENAME'. This script only upgrades bookworm -> trixie."
else
  SKIP_UPGRADE=0
fi

# ---- Confirm -----------------------------------------------------
if [ "$SKIP_UPGRADE" -eq 0 ]; then
  cat <<EOF

This will upgrade Raspberry Pi OS from bookworm (Debian 12) to
trixie (Debian 13). It takes 20-60 minutes depending on SD speed
and network. You may be asked to confirm replacing config files;
the safe default is usually to keep your local version (N).

Recommended: make sure the Pi is plugged into power (not running
on a laptop battery) and that you can reach it via SSH or locally
if the session drops.

EOF
  read -r -p "Proceed with the dist-upgrade? [y/N] " CONFIRM
  [ "${CONFIRM,,}" = "y" ] || die "Aborted."
fi

# ---- Stop the bot so publish/ isn't locked during upgrade --------
if command -v pm2 >/dev/null 2>&1; then
  log "Stopping the bot under PM2 for the duration of the upgrade..."
  pm2 stop discord-bot 2>/dev/null || true
fi

if [ "$SKIP_UPGRADE" -eq 0 ]; then
  # ---- Fully update current release first ------------------------
  log "Refreshing bookworm packages before the jump..."
  sudo apt-get update
  sudo apt-get -y full-upgrade
  sudo apt-get -y autoremove --purge

  # ---- Point APT at trixie --------------------------------------
  log "Switching APT sources from bookworm to trixie..."
  sudo sed -i.bak 's/\bbookworm\b/trixie/g' /etc/apt/sources.list
  if [ -f /etc/apt/sources.list.d/raspi.list ]; then
    sudo sed -i.bak 's/\bbookworm\b/trixie/g' /etc/apt/sources.list.d/raspi.list
  fi
  # Some Pi OS installs also have per-file sources under sources.list.d
  for f in /etc/apt/sources.list.d/*.list; do
    [ -f "$f" ] && sudo sed -i.bak 's/\bbookworm\b/trixie/g' "$f"
  done

  # ---- Do the dist-upgrade --------------------------------------
  log "Updating package index for trixie..."
  sudo apt-get update

  log "Running minimal upgrade (this is the long step)..."
  sudo DEBIAN_FRONTEND=noninteractive apt-get -y \
    -o Dpkg::Options::="--force-confdef" \
    -o Dpkg::Options::="--force-confold" \
    upgrade --without-new-pkgs

  log "Running full dist-upgrade..."
  sudo DEBIAN_FRONTEND=noninteractive apt-get -y \
    -o Dpkg::Options::="--force-confdef" \
    -o Dpkg::Options::="--force-confold" \
    full-upgrade

  sudo apt-get -y autoremove --purge
  sudo apt-get clean

  log "Distro upgrade finished."
fi

# ---- Verify glibc / libstdc++ versions ---------------------------
log "Verifying glibc and libstdc++ versions..."
GLIBC_VER="$(ldd --version | head -1 | awk '{print $NF}')"
echo "  glibc: $GLIBC_VER"
if strings /lib/aarch64-linux-gnu/libstdc++.so.6 2>/dev/null | grep -q 'GLIBCXX_3\.4\.32'; then
  echo "  GLIBCXX_3.4.32: FOUND"
else
  warn "GLIBCXX_3.4.32 not found in /lib/aarch64-linux-gnu/libstdc++.so.6."
  warn "You may need to reboot before rebuilding: 'sudo reboot'."
fi

# ---- Rebuild and restart the bot ---------------------------------
if command -v dotnet >/dev/null 2>&1; then
  log "Republishing the bot..."
  dotnet publish -c Release -o publish --nologo
fi

if command -v pm2 >/dev/null 2>&1; then
  log "Starting the bot under PM2 with DAVE enabled..."
  # DAVE is on by default in Program.cs once you're on trixie; make sure
  # any previous DISABLE_DAVE override is cleared.
  unset DISABLE_DAVE
  pm2 restart discord-bot --update-env 2>/dev/null \
    || pm2 start ecosystem.config.js --update-env
  pm2 save
fi

echo
log "All done. A reboot is strongly recommended to pick up the new kernel/libs:"
echo "  sudo reboot"
echo
log "After reboot, check the bot with:"
echo "  pm2 status"
echo "  pm2 logs discord-bot"
