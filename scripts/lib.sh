#!/usr/bin/env bash
# Shared paths and guards for the Humankind mod scripts.
HUMANKIND_DIR="${HUMANKIND_DIR:-/mnt/d/SteamLibrary/steamapps/common/Humankind}"
STEAM_EXE="${STEAM_EXE:-/mnt/c/Program Files (x86)/Steam/steam.exe}"
# Humankind's Windows folders, derived from %USERPROFILE% (override with WIN_HOME, HK_DOCS or HK_LOCALLOW).
WIN_HOME="${WIN_HOME:-$(wslpath "$(cd /mnt/c && cmd.exe /c 'echo %USERPROFILE%' 2>/dev/null | tr -d '\r')" 2>/dev/null)}"
[ -n "${HK_DOCS:-}" ] && HK_DOCS_SET=1
HK_DOCS="${HK_DOCS:-$WIN_HOME/Documents/Humankind}"
HK_LOCALLOW="${HK_LOCALLOW:-$WIN_HOME/AppData/LocalLow/Amplitude Studios/Humankind}"

# Tools that read the Windows folders call this first, so a missing cmd.exe stops them instead of reading "/Documents".
require_windows_home() {
  [ -n "$WIN_HOME" ] || [ -n "${HK_DOCS_SET:-}" ] || {
    echo "Could not find the Windows home folder (is cmd.exe reachable?): set WIN_HOME or HK_DOCS." >&2
    exit 1
  }
}
BEPINEX_VERSION="5.4.23.6"
INSTALLED_LIST="$HUMANKIND_DIR/BepInEx/.zk-installed-files"
AUTOPLAY_DIR="$HUMANKIND_DIR/BepInEx/AutoPlay"
DIAGNOSTICS_DIR="$HUMANKIND_DIR/BepInEx/CityRedraw"
# Shared with the other game sessions on this PC (one game at a time: Steam, screen and focus are shared).
LEGACY_LOCK_DIR="$HOME/.claude/state"
[ -d "$LEGACY_LOCK_DIR" ] && DEFAULT_LOCK="$LEGACY_LOCK_DIR/game-access.lock" || DEFAULT_LOCK="${XDG_STATE_HOME:-$HOME/.local/state}/game-access.lock"
GAME_LOCK="${GAME_LOCK:-$DEFAULT_LOCK}"
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

# True while the lock's holder is live: not yet expired, or its pid still running. An unreadable
# owner file counts as held for 10 minutes, so a failed read never hands over a live lock, while
# one a crash left half-written still frees up.
lock_is_held() {
  local fields expires pid
  if ! fields="$(python3 -c 'import json,sys; o=json.load(open(sys.argv[1])); print(int(o.get("expires") or 0), int(o.get("pid") or 0))' \
    "$GAME_LOCK/owner.json" 2>/dev/null)"; then
    [ -n "$(find "$GAME_LOCK" -maxdepth 1 -mmin -10 -print -quit 2>/dev/null)" ]
    return
  fi
  read -r expires pid <<<"$fields"
  [ "$expires" -gt "$(date +%s)" ] || { [ "$pid" -gt 0 ] && kill -0 "$pid" 2>/dev/null; }
}

# mkdir is not atomic in the sandbox these scripts run in (49 of 50 races had two winners), so
# every check-then-write on the lock runs under a kernel flock on a file beside it.
with_lock_mutex() {
  mkdir -p "$(dirname "$GAME_LOCK")"
  (
    flock -w 10 9 || { echo "Couldn't get the game-lock mutex" >&2; exit 2; }
    "$@"
  ) 9>"$GAME_LOCK.mutex"
}

take_lock_unguarded() {
  if [ -d "$GAME_LOCK" ] && lock_is_held; then
    echo "Game lock held: $(cat "$GAME_LOCK/owner.json" 2>/dev/null)" >&2
    return 1
  fi
  [ -d "$GAME_LOCK" ] && echo "Taking over expired game lock: $(cat "$GAME_LOCK/owner.json" 2>/dev/null)" >&2
  mkdir -p "$GAME_LOCK"
  write_lock_owner "$1"
}

renew_lock_unguarded() {
  if [ ! -d "$GAME_LOCK" ] || grep -q "\"session\":\"$GAME_LOCK_SESSION\"" "$GAME_LOCK/owner.json" 2>/dev/null; then
    mkdir -p "$GAME_LOCK"
    write_lock_owner "$1"
  else
    echo "Game lock is now held by another session; not renewing it" >&2
  fi
}

# Takes the lock for <minutes>; a lock past its expiry whose holder pid is dead is taken over.
acquire_game_lock() { with_lock_mutex take_lock_unguarded "$1"; }

# Never fails the caller: a run in progress must not stop because the lock moved.
renew_game_lock() {
  with_lock_mutex renew_lock_unguarded "$1"
  return 0
}

release_game_lock() {
  grep -q "\"session\":\"$GAME_LOCK_SESSION\"" "$GAME_LOCK/owner.json" 2>/dev/null && rm -rf "$GAME_LOCK"
  return 0
}
