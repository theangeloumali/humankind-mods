#!/usr/bin/env bash
# Installs BepInEx into the game folder. Adds files only; never overwrites one.
set -euo pipefail
. "$(dirname "$0")/lib.sh"
require_game_dir
require_game_closed

if [ -f "$INSTALLED_LIST" ]; then
  echo "BepInEx already installed by this script ($INSTALLED_LIST)."
  exit 0
fi

work="$(mktemp -d "${TMPDIR:-/tmp}/bepinex.XXXXXX")"
trap 'rm -rf "$work"' EXIT
asset="BepInEx_win_x64_${BEPINEX_VERSION}.zip"
release_json="$(curl -fsSL "https://api.github.com/repos/BepInEx/BepInEx/releases/tags/v${BEPINEX_VERSION}")"
expected="$(printf '%s' "$release_json" | python3 -c "import json,sys; print(next(a['digest'] for a in json.load(sys.stdin)['assets'] if a['name']=='$asset').split(':',1)[1])")"

mkdir "$work/extract"
curl -fsSL -o "$work/$asset" "https://github.com/BepInEx/BepInEx/releases/download/v${BEPINEX_VERSION}/$asset"
actual="$(sha256sum "$work/$asset" | cut -d' ' -f1)"
if [ "$actual" != "$expected" ]; then
  echo "SHA-256 mismatch: expected $expected, got $actual" >&2
  exit 1
fi
echo "SHA-256 OK: $actual"

unzip -q "$work/$asset" -d "$work/extract"
(cd "$work/extract" && find . -type f | sed 's|^\./||' | sort) > "$work/files"
while read -r rel; do
  if [ -e "$HUMANKIND_DIR/$rel" ]; then
    echo "Refusing to overwrite existing $rel" >&2
    exit 1
  fi
done < "$work/files"

(cd "$work/extract" && cp -r . "$HUMANKIND_DIR/")
cp "$work/files" "$INSTALLED_LIST"
echo "Installed BepInEx $BEPINEX_VERSION ($(wc -l < "$work/files") files) into $HUMANKIND_DIR"
