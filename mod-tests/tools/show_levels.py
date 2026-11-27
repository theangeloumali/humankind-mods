"""Prints the entries of the given levels (Critical, Error, ...) with their stack traces, from the
newest diagnostics log or from every part of one session, in order.
show_levels.py [--session <stamp, e.g. 2249>] [--first N] Critical Error"""
import argparse
import glob
import html
import os
import re

import hkpaths  # noqa: E402

LOGS = hkpaths.logs()
ENTRY = re.compile(r'<p id="(\w+)"[^>]*>(.*?)</p>\s*(?:<pre[^>]*>(.*?)</pre>)?', re.S)


def clean(text):
    return html.unescape(re.sub(r"<[^>]+>", " ", text or "")).strip()


def session_parts(stamp):
    """The session's log, then its parts p2, p3, ... (the game splits long logs)."""
    files = glob.glob(os.path.join(LOGS, f"Diagnostics (*@{stamp}*).html"))
    return sorted(files, key=lambda f: int(re.search(r"\)p(\d+)\.html$", f).group(1)) if re.search(r"\)p\d+\.html$", f) else 1)


parser = argparse.ArgumentParser()
parser.add_argument("--session")
parser.add_argument("--first", type=int, default=0)
parser.add_argument("levels", nargs="+")
args = parser.parse_args()
files = session_parts(args.session) if args.session else [
    max(glob.glob(os.path.join(LOGS, "Diagnostics (*).html")), key=os.path.getmtime)]
shown = 0
for path in files:
    for level, body, trace in ENTRY.findall(open(path, encoding="utf-8", errors="replace").read()):
        if level not in args.levels:
            continue
        print(f"== {level}: {clean(body)[:500]}")
        for line in clean(trace).splitlines()[:14]:
            print("     " + line.strip()[:200])
        shown += 1
        if args.first and shown >= args.first:
            raise SystemExit(0)
