#!/usr/bin/env bash
# Full test of one load order: offline check, write, main menu, new game, 3 turns.
# Usage: run_combo.sh <order.txt>   Prints MENU/GAME verdicts and the log summary.
set -uo pipefail
H="$(dirname "$0")"
. "$H/../../scripts/lib.sh"
require_windows_home
order="$1"
label="$(basename "$order" .txt)"
LOGS="$HK_DOCS/Temporary Files"
ps() { powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$H/screen.ps1")" "$@" >/dev/null; }
alive() { /mnt/c/Windows/System32/tasklist.exe /FI "IMAGENAME eq Humankind.exe" /NH | grep -qi humankind; }

if alive; then
  /mnt/c/Windows/System32/taskkill.exe /IM Humankind.exe >/dev/null
  for _ in $(seq 1 30); do alive || break; sleep 2; done
fi
echo "== offline check"
python3 "$H/check_order.py" "$H/../data/defs.json" "$H/../data/vanilla.json" "$order" | tail -6 || { echo "OFFLINE: FAIL"; exit 1; }
"$H/../.venv/bin/python" -W ignore "$H/status_check.py" "$order" | tail -6 || { echo "OFFLINE: FAIL (status targets)"; exit 1; }
"$H/../.venv/bin/python" -W ignore "$H/presentation_check.py" "$order" | tail -6 || { echo "OFFLINE: FAIL (unit presentations)"; exit 1; }
"$H/../.venv/bin/python" -W ignore "$H/descriptor_check.py" "$order" | tail -6 || { echo "OFFLINE: FAIL (effect starting points)"; exit 1; }
# Not fatal: CityRedraw.Diagnostics' MinorFactionUpgradeGuard keeps these from breaking a turn.
"$H/../.venv/bin/python" -W ignore "$H/unlock_check.py" "$order" | tail -3 || echo "OFFLINE: note: units no technology unlocks (guarded in game)"
"$H/hk-order/target/debug/hk-order" write "$order" || exit 1

menu="$(bash "$H/to_menu.sh" "$label" 2>&1)"
echo "$menu" | sed -n '1,12p'
if echo "$menu" | grep -q "Game lock held"; then echo "LOCK BUSY: another run holds the game"; exit 2; fi
configs="$(echo "$menu" | grep -c '^configuration:')"
errors="$(echo "$menu" | sed -n 's/^== Error: \([0-9]*\).*/\1/p')"
if [ "$configs" != 1 ] || [ "$errors" != 0 ]; then
  echo "MENU: FAIL (configurations=$configs errors=$errors)"
  python3 "$H/crash_context.py" | head -16
  exit 1
fi
echo "MENU: PASS"

ps click 2600 1400; sleep 2          # focus the game
ps click 400 932; sleep 2            # Play
ps click 1260 932; sleep 8           # New Game
ps click 4708 2020                   # Start
started="$(date '+%Y-%m-%d %H:%M:%S')"
for _ in $(seq 1 40); do
  newest="$(ls -t "$LOGS"/Diagnostics*.html | head -1)"
  grep -q "Finished loading in-game windows" "$newest" && break
  alive || break
  sleep 10
done
sleep 60                             # the world reveal runs after the windows load
ps click 3016 868; sleep 2           # close the intro popup
ps click 4872 1996; sleep 3          # End turn selects the idle starting army first
ps click 4280 720; sleep 2           # then auto-explore it, so turns can end
turns="$(bash "$H/end_turns.sh" 3)"  # counted by the autosave each turn writes
bash "$H/step.sh" "$label-game" >/dev/null
game="$(python3 "$H/read_log.py")"
echo "$game" | sed -n '1,10p'
gerrors="$(echo "$game" | sed -n 's/^== Error: \([0-9]*\).*/\1/p')"
echo "turns played (new autosaves): $turns"
if echo "$game" | grep -q "in-game reached: True" && [ "$gerrors" = 0 ] && [ "$turns" -ge 2 ] && alive; then
  echo "GAME: PASS"
else
  echo "GAME: FAIL (errors=$gerrors)"
  python3 "$H/crash_context.py" | head -16
fi
echo "screenshot: $H/$label-game-small.png"
