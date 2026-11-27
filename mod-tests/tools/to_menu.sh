#!/usr/bin/env bash
# Launches Humankind under the shared game lock and waits for the main menu: to_menu.sh <label>
set -uo pipefail
. "$(dirname "$0")/../../scripts/lib.sh"
GAME_LOCK_SESSION="humankind-load-order-fix"
HERE="$(dirname "$0")"
LOGS="$HK_DOCS/Temporary Files"
label="${1:-run}"

require_game_dir
require_game_closed
# A lock this session's earlier run left behind is renewed, never another session's.
if ! acquire_game_lock 90; then
  grep -q "\"session\":\"$GAME_LOCK_SESSION\"" "$GAME_LOCK/owner.json" 2>/dev/null || exit 1
  renew_game_lock 90
fi
trap release_game_lock EXIT

before="$(ls -t "$LOGS"/Diagnostics*.html 2>/dev/null | head -1)"
"$STEAM_EXE" -applaunch 1124300 --novideo >/dev/null 2>&1 &
for _ in $(seq 1 60); do game_running && break; sleep 5; done
game_running || { echo "Humankind did not start within 5 minutes" >&2; exit 1; }

# The newest log is the run's once it differs from the one before launch.
for _ in $(seq 1 120); do
  newest="$(ls -t "$LOGS"/Diagnostics*.html 2>/dev/null | head -1)"
  if [ "$newest" != "$before" ] && grep -q "Finished loading out-game windows" "$newest" 2>/dev/null; then
    echo "main menu at $(date +%T)"
    break
  fi
  game_running || { echo "Humankind exited before the main menu" >&2; break; }
  renew_game_lock 90
  sleep 10
done
sleep 8
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$HERE/screen.ps1")" shot "$(wslpath -w "$HERE")\\$label-menu.png"
python3 -I "$HERE/read_log.py"
