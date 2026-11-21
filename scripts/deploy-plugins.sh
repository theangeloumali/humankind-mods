#!/usr/bin/env bash
# Builds the plugins in Release; the DeployToGame target (mods/Plugin.props) copies them into BepInEx/plugins/ZKidz.
set -euo pipefail
. "$(dirname "$0")/lib.sh"
require_game_dir
require_game_closed
[ -f "$HUMANKIND_DIR/BepInEx/core/BepInEx.dll" ] || { echo "Run install-bepinex.sh first." >&2; exit 1; }

export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build "$(dirname "$0")/../mods/HumankindMods.sln" -c Release -p:HumankindDir="$HUMANKIND_DIR" -p:Deploy=true | tail -3
ls -1 "$HUMANKIND_DIR/BepInEx/plugins/ZKidz"
