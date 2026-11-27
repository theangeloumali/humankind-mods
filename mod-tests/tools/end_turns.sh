#!/usr/bin/env bash
# Plays N turns, counting each one by the autosave the game writes: end_turns.sh <count>
# Prints the number of turns that completed.
H="$(dirname "$0")"
. "$H/../../scripts/lib.sh"
require_windows_home
SAVES="$HK_DOCS/Save Files"
ps() { powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$H/screen.ps1")" "$@" >/dev/null; }
saves() { find "$SAVES" -name 'auto-save*.ctr' -newermt "$1" | wc -l; }
start="$(date '+%Y-%m-%d %H:%M:%S')"
done_turns=0
for _ in $(seq 1 "$1"); do
  for _ in 1 2 3 4 5 6; do
    ps click 2360 1376; sleep 1      # "Enter a new Era": Remind me later
    ps click 2560 1684; sleep 1      # Close a tutorial card / Confirm a narrative choice
    ps click 4088 2076; sleep 1      # Acknowledge a notification
    ps click 4872 1996               # End turn (or select the next idle army)
    for _ in $(seq 1 12); do
      sleep 5
      [ "$(saves "$start")" -gt "$done_turns" ] && break
    done
    [ "$(saves "$start")" -gt "$done_turns" ] && break
  done
  done_turns="$(saves "$start")"
done
echo "$done_turns"
