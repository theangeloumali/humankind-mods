#!/usr/bin/env bash
# Waits for the next game session to reach the map, lets the loading screen clear, then saves a
# full screenshot and a quarter-size copy: shot_after_load.sh <out.png> [settle seconds]
set -uo pipefail
H="$(cd "$(dirname "$0")" && pwd)"
. "$H/../../scripts/lib.sh"
require_windows_home
LOGS="$HK_DOCS/Temporary Files"
start="$(date '+%F %T')"
for _ in $(seq 1 120); do
  log="$(find "$LOGS" -name 'Diagnostics (*).html' -newermt "$start" | head -n 1)"
  [ -n "$log" ] && grep -q "Finished loading in-game windows" "$log" && break
  sleep 5
done
sleep "${2:-45}"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$H/screen.ps1")" shot "$(wslpath -w "$1")" >/dev/null
"$H/../.venv/bin/python" -c "import sys; from PIL import Image; Image.open(sys.argv[1]).resize((1280, 540)).save(sys.argv[1].replace('.png', '-small.png'))" "$1"
echo "shot $1"
