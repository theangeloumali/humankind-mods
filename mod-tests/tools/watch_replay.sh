#!/usr/bin/env bash
# Watches an autoplay replay: prints CRASH on the simulation exception, PROGRESSED once the turn
# after <turn> is played, then the narrative choices logged and the modules the game loaded.
# Usage: watch_replay.sh <turn>
set -uo pipefail
D=/mnt/d/SteamLibrary/steamapps/common/Humankind/BepInEx
. "$(dirname "$0")/../../scripts/lib.sh"
require_windows_home
LOGS="$HK_DOCS/Temporary Files"
start="$(date '+%F %T')"
next=$(( $1 + 1 ))
seen=$(wc -l < "$D/AutoPlay/progress.log")  # judge only lines this run writes
log=""
for _ in $(seq 1 100); do
  log="$(find "$LOGS" -name 'Diagnostics (*).html' -newermt "$start" | head -n 1)"
  if [ -n "$log" ] && grep -q "Index was outside the bounds" "$log"; then echo CRASH; break; fi
  if tail -n +"$((seen + 1))" "$D/AutoPlay/progress.log" | grep -qE "turn $next \(|autoplay off"; then echo PROGRESSED; break; fi
  sleep 6
done
tail -n 6 "$D/AutoPlay/progress.log"
grep -E "narrative choice|applies status" "$D/CityRedraw/diagnostics.log" | tail -n 8 | cut -c1-200
[ -n "$log" ] && grep -o "Set up new configuration [^<]*" "$log" | tail -n 1 | tr ',' '\n' | tail -n 3
