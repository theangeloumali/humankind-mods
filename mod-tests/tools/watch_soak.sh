#!/usr/bin/env bash
# Streams soak events, one line each: every 10th turn, the Neolithic05 events, presentation
# handler exceptions, a simulation exception, a stall (no turn for 10 minutes), and the finish.
set -uo pipefail
D=/mnt/d/SteamLibrary/steamapps/common/Humankind/BepInEx
. "$(dirname "$0")/../../scripts/lib.sh"
LOGS="$HK_DOCS/Temporary Files"
start="$(date '+%F %T')"
progress_seen=$(wc -l < "$D/AutoPlay/progress.log")
narrative_seen=$(wc -l < "$D/CityRedraw/diagnostics.log")
crash_reported=""
stall_reported=0
while true; do
  new="$(tail -n +"$((progress_seen + 1))" "$D/AutoPlay/progress.log")"
  if [ -n "$new" ]; then
    progress_seen=$(wc -l < "$D/AutoPlay/progress.log")
    stall_reported=0
    echo "$new" | grep -E "turn [0-9]*0 \(|autoplay off|armed" || true
  fi
  narrative="$(tail -n +"$((narrative_seen + 1))" "$D/CityRedraw/diagnostics.log")"
  narrative_seen=$(wc -l < "$D/CityRedraw/diagnostics.log")
  echo "$narrative" | grep -A2 "Neolithic05" | cut -c1-160 || true
  echo "$narrative" | grep -A6 "presentation handler threw\|session start" | cut -c1-300 || true
  for log in $(find "$LOGS" -name 'Diagnostics (*).html' -newermt "$start" -printf '%f\n' | tr ' ' '?'); do
    if [ "$crash_reported" != "$log" ] && grep -q "Index was outside the bounds" "$LOGS"/$log; then
      echo "CRASH: simulation exception in $log"; crash_reported="$log"
    fi
  done
  age=$(( $(date +%s) - $(stat -c %Y "$D/AutoPlay/progress.log") ))
  if [ "$age" -gt 600 ] && [ "$stall_reported" = 0 ]; then echo "STALL: no turn for ${age}s"; stall_reported=1; fi
  [ -f "$D/AutoPlay/done.txt" ] && { echo "DONE: $(cat "$D/AutoPlay/done.txt")"; exit 0; }
  sleep 30
done
