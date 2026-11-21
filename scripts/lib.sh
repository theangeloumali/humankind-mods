#!/usr/bin/env bash
# Shared paths and guards for the Humankind mod scripts.
HUMANKIND_DIR="${HUMANKIND_DIR:-/mnt/d/SteamLibrary/steamapps/common/Humankind}"
BEPINEX_VERSION="5.4.23.6"
INSTALLED_LIST="$HUMANKIND_DIR/BepInEx/.zk-installed-files"

require_game_closed() {
  if game_running; then
    echo "Humankind.exe is running. Quit the game first." >&2
    exit 1
  fi
}

require_game_dir() {
  [ -f "$HUMANKIND_DIR/Humankind.exe" ] || {
    echo "Humankind.exe not found in $HUMANKIND_DIR (set HUMANKIND_DIR)." >&2
    exit 1
  }
}

STEAM_EXE="${STEAM_EXE:-/mnt/c/Program Files (x86)/Steam/steam.exe}"
AUTOPLAY_DIR="$HUMANKIND_DIR/BepInEx/AutoPlay"
DIAGNOSTICS_DIR="$HUMANKIND_DIR/BepInEx/CityRedraw"

game_running() { tasklist.exe 2>/dev/null | grep -qi '^Humankind.exe'; }
