"""Prints the log entries just before the newest log's first crash or error, with their stack traces."""
import glob
import html
import os
import re

import hkpaths  # noqa: E402

LOGS = hkpaths.logs()
newest = max(glob.glob(os.path.join(LOGS, "Diagnostics (*).html")), key=os.path.getmtime)
text = open(newest, encoding="utf-8", errors="replace").read()
pattern = re.compile(
    r'<p id="(\w+)"[^>]*><span class="time">([^<]*)</span>.*?toggle\(\'(trace\d+)\'\).*?</button>(.*?)</p>\s*(?:<pre id="\3"[^>]*>(.*?)</pre>)?',
    re.S,
)
entries = [
    (level, time, html.unescape(re.sub(r"<[^>]+>", "", body)).strip(), html.unescape(re.sub(r"<[^>]+>", "", trace or "")).strip())
    for level, time, _, body, trace in pattern.findall(text)
]
for index, (level, time, message, trace) in enumerate(entries):
    if level == "Error" or "crash" in message.lower():
        print(f"===== {level} {time}: {message[:300]}")
        for _, t, m, _ in entries[max(0, index - 6):index]:
            if "overriden an element" not in m:
                print(f"   before {t}: {m[:200]}")
        print("   trace:")
        for line in trace.splitlines()[:25]:
            print("     " + line.strip()[:200])
