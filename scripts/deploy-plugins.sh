#!/usr/bin/env bash
# Builds the plugins in Release and copies them into BepInEx/plugins/ZKidz.
set -euo pipefail
. "$(dirname "$0")/lib.sh"
require_game_dir
require_game_closed
[ -f "$HUMANKIND_DIR/BepInEx/core/BepInEx.dll" ] || { echo "Run install-bepinex.sh first." >&2; exit 1; }

repo="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build "$repo/mods/HumankindMods.sln" -c Release -p:HumankindDir="$HUMANKIND_DIR" | tail -3

target="$HUMANKIND_DIR/BepInEx/plugins/ZKidz"
mkdir -p "$target"
for plugin in CityRedraw.Diagnostics HumankindAutoPlay ModListGuard; do
  out="$repo/mods/$plugin/bin/Release/net472"
  [ -d "$out" ] || continue
  cp "$out/$plugin.dll" "$target/"
done
cp "$repo/mods/CityRedraw.Diagnostics/bin/Release/net472/HumankindMods.Core.dll" "$target/"
ls -1 "$target"
