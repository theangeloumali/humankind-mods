#!/usr/bin/env bash
# Lets the AI play the newest save up to a target turn, relaunching after a crash.
# Usage: autoplay.sh --turns 350 [--relaunches 3]
set -euo pipefail
. "$(dirname "$0")/lib.sh"

turns=""
relaunches=3
while [ $# -gt 0 ]; do
  case "$1" in
    --turns) turns="$2"; shift 2 ;;
    --relaunches) relaunches="$2"; shift 2 ;;
    *) echo "Unknown option $1" >&2; exit 2 ;;
  esac
done
[[ "$turns" =~ ^[1-9][0-9]*$ ]] || { echo "Usage: autoplay.sh --turns N [--relaunches N]" >&2; exit 2; }

require_game_dir
require_game_closed
[ -f "$HUMANKIND_DIR/BepInEx/plugins/ZKidz/HumankindAutoPlay.dll" ] || { echo "Run deploy-plugins.sh first." >&2; exit 1; }

mkdir -p "$AUTOPLAY_DIR"
rm -f "$AUTOPLAY_DIR/done.txt"
printf '%s\n' "$turns" > "$AUTOPLAY_DIR/request.txt"

wait_for_start() {
  for _ in $(seq 1 60); do game_running && return 0; sleep 5; done
  return 1
}

launches=0
while :; do
  launches=$((launches + 1))
  echo "$(date '+%F %T') launch $launches (target turn $turns)"
  "$STEAM_EXE" -applaunch 1124300 --loadlatest --novideo >/dev/null 2>&1 &
  wait_for_start || { echo "Humankind did not start within 5 minutes." >&2; break; }
  while game_running; do sleep 30; done

  [ -f "$AUTOPLAY_DIR/done.txt" ] && { echo "Target reached: $(cat "$AUTOPLAY_DIR/done.txt")"; break; }
  [ -f "$AUTOPLAY_DIR/request.txt" ] || { echo "Autoplay was stopped in game."; break; }
  [ "$launches" -le "$relaunches" ] || { echo "Game exited $launches times; giving up." >&2; break; }
  echo "$(date '+%F %T') game exited before the target; relaunching from the latest autosave"
  sleep 20
done

echo "--- autoplay progress (last 5) ---"
tail -n 5 "$AUTOPLAY_DIR/progress.log" 2>/dev/null || true
echo "--- diagnostics (last 5) ---"
tail -n 5 "$DIAGNOSTICS_DIR/diagnostics.log" 2>/dev/null || true
echo "--- reports ---"
ls -1t "$DIAGNOSTICS_DIR"/report-*.txt 2>/dev/null | head -5 || true
