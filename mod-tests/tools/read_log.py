"""Summarises Humankind's newest diagnostics log: what loaded, errors, and the warnings that matter."""
import glob
import html
import os
import re
import sys

import hkpaths  # noqa: E402

LOGS = hkpaths.logs()
ENTRY = re.compile(r'<p id="(\w+)"[^>]*><span class="time">([^<]*)</span>.*?</button>(.*?)</p>', re.S)
ROUTINE = re.compile(
    r"has overriden an element|is already registered|\[Localization\] Unknown key|Audio bank|"
    r"StateShareHeader|UIMaterialModifier|missing loc keys",
    re.I,
)

newest = sys.argv[1] if len(sys.argv) > 1 else max(glob.glob(os.path.join(LOGS, "Diagnostics (*).html")), key=os.path.getmtime)
text = open(newest, encoding="utf-8", errors="replace").read()
entries = [(level, time, html.unescape(re.sub(r"<[^>]+>", "", body)).strip()) for level, time, body in ENTRY.findall(text)]
print(f"log: {os.path.basename(newest)} ({len(entries)} entries)")

configs = [msg for _, _, msg in entries if "Set up new configuration" in msg]
for msg in configs:
    listed = msg.split("configuration", 1)[1].strip().split(",")
    print(f"configuration: {len(listed)} modules")
loaded = [re.search(r"'([^']*)'", msg).group(1) for _, _, msg in entries if msg.startswith("Loading runtime module")]
print(f"loaded runtime modules: {len(loaded)}")
if configs:
    wanted = configs[-1].split("configuration", 1)[1].strip().split(",")
    not_loaded = [m for m in wanted if m not in loaded]
    print(f"listed but not loaded: {not_loaded or 'none'}")
print("main menu reached:", any("Finished loading out-game windows" in msg for _, _, msg in entries))
print("in-game reached:", any("Finished loading in-game windows" in msg for _, _, msg in entries))

seen = set()
# Simulation exceptions in game are logged as Critical; they count as errors (the turn-10 hang).
for level, levels in (("Error", {"Critical", "Error"}), ("Exception", {"Exception"}), ("Warning", {"Warning"})):
    rows = [(t, m) for lvl, t, m in entries if lvl in levels and not ROUTINE.search(m)]
    print(f"== {level}: {len(rows)} (routine overrides excluded) ==")
    for t, m in rows:
        key = re.sub(r"\d+", "#", m)[:160]
        if key in seen:
            continue
        seen.add(key)
        print(f"  {t} {m[:240]}")
        if len(seen) > 60:
            break
flagged = [m for _, _, m in entries if re.search(r"mismatch|exception|crash|failed to|corrupt", m, re.I)]
print(f"== mismatch/exception/failed mentions: {len(flagged)} ==")
for m in dict.fromkeys(flagged):
    print("  " + m[:240])
