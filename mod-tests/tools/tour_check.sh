#!/usr/bin/env bash
# Plays a new game of one order to a turn, stops autoplay, writes the district census, then steps the
# CityRedraw camera tour (Ctrl+F11) and screenshots each stop, so bare tiles can be matched to the census.
# Usage: tour_check.sh <order.txt> <turn> <out dir> [stops]
set -uo pipefail
H="$(cd "$(dirname "$0")" && pwd)"
ROOT="$H/../.."
GAME=/mnt/d/SteamLibrary/steamapps/common/Humankind
PROGRESS="$GAME/BepInEx/AutoPlay/progress.log"
order="$1" turn="$2" out="$3" stops="${4:-12}"
mkdir -p "$out"
screen() { powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$H/screen.ps1")" "$@" >/dev/null; }
alive() { /mnt/c/Windows/System32/tasklist.exe /FI "IMAGENAME eq Humankind.exe" /NH | grep -qi humankind; }
shrink() { "$H/../.venv/bin/python" -c "import sys; from PIL import Image; im = Image.open(sys.argv[1]); w, h = im.size; im.crop((0, 0, w // 2, h)).resize((w // 4, h // 2)).save(sys.argv[1].replace('.png', '-left.png'))" "$1"; }
close_game() {
  /mnt/c/Windows/System32/taskkill.exe /IM Humankind.exe >/dev/null 2>&1
  for _ in $(seq 1 30); do alive || return 0; sleep 2; done
  /mnt/c/Windows/System32/taskkill.exe /IM Humankind.exe /F >/dev/null 2>&1
}

bash "$H/run_combo.sh" "$order" | grep -E "^(OFFLINE|LOCK|MENU|GAME)"
close_game
seen=$(wc -l < "$PROGRESS")
# The target is past the tour turn: reaching it quits the game, so Ctrl+F12 stops autoplay at the tour turn instead.
bash "$ROOT/scripts/autoplay.sh" --turns $(( turn + 20 )) --relaunches 0 >/dev/null 2>&1 &
for _ in $(seq 1 360); do
  tail -n +"$((seen + 1))" "$PROGRESS" | grep -qE "turn $turn \(" && break
  sleep 5
done
screen ctrlf 12
sleep 20
log_start=$(wc -l < "$GAME/BepInEx/CityRedraw/diagnostics.log")
screen ctrlf 10
sleep 6
for i in $(seq -w 1 "$stops"); do
  # Never capture the desktop: stop the moment the game is gone.
  alive || { echo "game closed at stop $i"; break; }
  screen ctrlf 11
  sleep 5
  alive || { echo "game closed at stop $i"; break; }
  screen shot "$(wslpath -w "$out/stop-$i.png")"
  shrink "$out/stop-$i.png"
done
tail -n +"$((log_start + 1))" "$GAME/BepInEx/CityRedraw/diagnostics.log" | grep -E "report|camera tour"
report="$(ls -t "$GAME"/BepInEx/CityRedraw/report-*.txt | head -n 1)"
sed -n '/^districts /,$p' "$report" | head -n 40
echo "turn: $(tail -n 1 "$PROGRESS")"
close_game
wait
