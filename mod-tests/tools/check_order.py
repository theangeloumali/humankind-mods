"""Replays a load order offline against the game's data checks (rules.py).
Usage: check_order.py <defs.json> <vanilla.json> <order.txt>  (one module id per line, base first)"""
import json
import sys

from rules import conflicts

defs = json.load(open(sys.argv[1]))
vanilla = json.load(open(sys.argv[2]))
order = [line.strip() for line in open(sys.argv[3], encoding="utf-8") if line.strip()]
found = conflicts(order, defs, vanilla)
for line in found:
    print(line[:300])
print(f"{len(found)} problem(s)")
sys.exit(1 if found else 0)
