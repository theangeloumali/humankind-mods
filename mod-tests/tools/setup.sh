#!/usr/bin/env bash
# One-time setup: the Python venv for bundle tools and the hk-order registry driver.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
python3 -m venv "$here/../.venv"
"$here/../.venv/bin/pip" install -q -r "$here/requirements.txt"
# A .pth import line runs at every venv start, so the memory cap also covers one-off "python -c" scans.
# (Not sitecustomize: Debian's /usr/lib/python3.x/sitecustomize.py shadows a venv one.)
site_packages="$("$here/../.venv/bin/python" -c 'import sysconfig; print(sysconfig.get_paths()["purelib"])')"
cp "$here/memory_cap.py" "$site_packages/zk_memory_cap.py"
echo "import zk_memory_cap" > "$site_packages/zk_memory_cap.pth"
cargo build -q --manifest-path "$here/hk-order/Cargo.toml"
echo "ready: $here/../.venv/bin/python, $here/hk-order/target/debug/hk-order"
