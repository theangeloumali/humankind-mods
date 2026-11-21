#!/usr/bin/env bash
# Shared paths and guards for the Humankind mod scripts.
HUMANKIND_DIR="${HUMANKIND_DIR:-/mnt/d/SteamLibrary/steamapps/common/Humankind}"
STEAM_EXE="${STEAM_EXE:-/mnt/c/Program Files (x86)/Steam/steam.exe}"
BEPINEX_VERSION="5.4.23.6"
INSTALLED_LIST="$HUMANKIND_DIR/BepInEx/.zk-installed-files"
AUTOPLAY_DIR="$HUMANKIND_DIR/BepInEx/AutoPlay"
DIAGNOSTICS_DIR="$HUMANKIND_DIR/BepInEx/CityRedraw"
# Shared with the other game sessions on this PC (one game at a time: Steam, screen and focus are shared).
GAME_LOCK="$HOME/.claude/state/game-access.lock"
GAME_LOCK_SESSION="humankind-autoplay-bug-hunt"

game_running() { tasklist.exe /FI "IMAGENAME eq Humankind.exe" 2>/dev/null | grep -qi '^Humankind.exe'; }

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

write_lock_owner() {
  printf '{"session":"%s","game":"humankind","pid":%d,"expires":%d}\n' \
    "$GAME_LOCK_SESSION" "$$" $(( $(date +%s) + $1 * 60 )) > "$GAME_LOCK/owner.json"
}

# Takes the lock for <minutes>; a lock past its expiry whose holder pid is dead is taken over.
acquire_game_lock() {
  mkdir -p "$(dirname "$GAME_LOCK")"
  if ! mkdir "$GAME_LOCK" 2>/dev/null; then
    local owner expires pid
    owner="$(cat "$GAME_LOCK/owner.json" 2>/dev/null)"
    expires="$(printf '%s' "$owner" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("expires") or 0)' 2>/dev/null || echo 0)"
    pid="$(printf '%s' "$owner" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("pid") or 0)' 2>/dev/null || echo 0)"
    if [ "$expires" -gt "$(date +%s)" ] || { [ "$pid" -gt 0 ] && kill -0 "$pid" 2>/dev/null; }; then
      echo "Game lock held: $owner" >&2
      return 1
    fi
    echo "Taking over expired game lock: $owner" >&2
  fi
  write_lock_owner "$1"
}

renew_game_lock() { [ -d "$GAME_LOCK" ] && write_lock_owner "$1"; }

release_game_lock() {
  grep -q "\"session\":\"$GAME_LOCK_SESSION\"" "$GAME_LOCK/owner.json" 2>/dev/null && rm -rf "$GAME_LOCK"
  return 0
}
