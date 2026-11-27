#!/usr/bin/env bash
# Opens the newest save by hand (autoplay idle) and holds the shared game lock until the game closes.
# Prints IN-GAME once the map has loaded, so a caller can start inspecting.
set -euo pipefail
. "$(dirname "$0")/../../scripts/lib.sh"
LOGS="$HK_DOCS/Temporary Files"

require_game_closed
rm -f "$AUTOPLAY_DIR/request.txt"
acquire_game_lock 60 || exit 1
trap release_game_lock EXIT

started="$(date '+%F %T')"
"$STEAM_EXE" -applaunch 1124300 --loadlatest --novideo >/dev/null 2>&1 &
for _ in $(seq 1 120); do
  log="$(find "$LOGS" -name 'Diagnostics (*).html' -newermt "$started" | head -n 1)"
  [ -n "$log" ] && grep -q "Finished loading in-game windows" "$log" && { echo "IN-GAME $log"; break; }
  sleep 5
done
while game_running; do renew_game_lock 60; sleep 30; done
echo "game closed"
