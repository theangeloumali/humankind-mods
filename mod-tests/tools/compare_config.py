"""Compares an order file with the configuration the game loaded: compare_config.py <order.txt>"""
import glob
import html
import os
import re
import sys

import hkpaths  # noqa: E402

LOGS = hkpaths.logs()
REGISTRY = hkpaths.registry()
newest = max(glob.glob(os.path.join(LOGS, "Diagnostics (*).html")), key=os.path.getmtime)
loaded = html.unescape(re.search(r"Set up new configuration ([^<]*)", open(newest, encoding="utf-8", errors="replace").read()).group(1)).split(",")
wanted = [l.strip() for l in open(sys.argv[1], encoding="utf-8") if l.strip()]
# Both files escape names ("comp super pack &amp; vip").
registry = html.unescape(re.search(r"<RuntimeModuleConfiguration>([^<]*)", open(REGISTRY, encoding="utf-8-sig").read()).group(1)).split("/")
print(f"order {len(wanted)}, registry {len(registry)}, loaded {len(loaded)}")
print("in order, not loaded:", [m for m in wanted if m not in loaded])
print("loaded, not in order:", [m for m in loaded if m not in wanted])
print("in order, not in registry:", [m for m in wanted if m not in registry])
