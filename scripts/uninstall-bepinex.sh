#!/usr/bin/env bash
# Removes BepInEx and everything under it (plugins, config, logs) from the game folder.
set -euo pipefail
. "$(dirname "$0")/lib.sh"
require_game_dir
require_game_closed

[ -f "$INSTALLED_LIST" ] || { echo "No install record at $INSTALLED_LIST; nothing to do."; exit 0; }
while read -r rel; do
  case "$rel" in BepInEx/*) ;; *) rm -f "$HUMANKIND_DIR/$rel" ;; esac
done < "$INSTALLED_LIST"
rm -rf "$HUMANKIND_DIR/BepInEx"
echo "BepInEx removed from $HUMANKIND_DIR"
