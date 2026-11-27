#!/usr/bin/env bash
# One UI step: optional click at full-resolution x y, wait, then a quarter-size screenshot.
# Usage: step.sh <name> [x y] [wait seconds]
H="$(dirname "$0")"
name="$1"; x="${2:-}"; y="${3:-}"; wait="${4:-3}"
ps() { powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$H/screen.ps1")" "$@"; }
[ -n "$x" ] && ps click "$x" "$y"
sleep "$wait"
ps shot "$(wslpath -w "$H")\\$name.png" >/dev/null
"$H/../.venv/bin/python" -c "
from PIL import Image; im=Image.open('$H/$name.png'); im.resize((im.width//4, im.height//4)).save('$H/$name-small.png')"
echo "$H/$name-small.png"
