#!/usr/bin/env bash
# Plays a new game of one order to a turn, then screenshots the map, counts stuck-presentation
# exceptions and prints the district census (CityRedraw report: art found / loaded for every district).
# Usage: visual_check.sh <order.txt> <turn> <out.png>
set -uo pipefail
H="$(cd "$(dirname "$0")" && pwd)"
ROOT="$H/../.."
. "$ROOT/scripts/lib.sh"
require_windows_home
PROGRESS=/mnt/d/SteamLibrary/steamapps/common/Humankind/BepInEx/AutoPlay/progress.log
PLAYER_LOG="$HK_LOCALLOW/Player.log"
order="$1" turn="$2" out="$3"
alive() { /mnt/c/Windows/System32/tasklist.exe /FI "IMAGENAME eq Humankind.exe" /NH | grep -qi humankind; }
close_game() {
  /mnt/c/Windows/System32/taskkill.exe /IM Humankind.exe >/dev/null 2>&1
  for _ in $(seq 1 30); do alive || return 0; sleep 2; done
  /mnt/c/Windows/System32/taskkill.exe /IM Humankind.exe /F >/dev/null 2>&1
}

bash "$H/run_combo.sh" "$order" | grep -E "^(OFFLINE|LOCK|MENU|GAME)"
close_game
seen=$(wc -l < "$PROGRESS")
bash "$ROOT/scripts/autoplay.sh" --turns $(( turn + 3 )) --relaunches 0 >/dev/null 2>&1 &
for _ in $(seq 1 240); do
  tail -n +"$((seen + 1))" "$PROGRESS" | grep -qE "turn $turn \(" && break
  sleep 5
done
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$H/screen.ps1")" shot "$(wslpath -w "$out")" >/dev/null
"$H/../.venv/bin/python" -c "import sys; from PIL import Image; Image.open(sys.argv[1]).resize((1280, 540)).save(sys.argv[1].replace('.png', '-small.png'))" "$out"
REPORTS=/mnt/d/SteamLibrary/steamapps/common/Humankind/BepInEx/CityRedraw
before="$(ls -t "$REPORTS"/report-*.txt 2>/dev/null | head -n 1)"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$H/screen.ps1")" ctrlf 10 >/dev/null
for _ in $(seq 1 15); do
  report="$(ls -t "$REPORTS"/report-*.txt 2>/dev/null | head -n 1)"
  [ -n "$report" ] && [ "$report" != "$before" ] && break
  sleep 2
done
[ "$report" != "$before" ] && sed -n '/^districts /,$p' "$report" | head -n 40 || echo "census: no report written"
echo "turn: $(tail -n 1 "$PROGRESS")"
echo "stuck-presentation exceptions: $(grep -c 'PresentationDataChangedCollection.Invoke' "$PLAYER_LOG")"
rm -f /mnt/d/SteamLibrary/steamapps/common/Humankind/BepInEx/AutoPlay/request.txt
close_game
wait
