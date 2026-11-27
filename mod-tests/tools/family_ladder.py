"""Prints a family's upgrade ladder after an order is merged: family_ladder.py <order.txt> <family>..."""
import json
import os
import sys
from collections import defaultdict

from rules import merged

DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")

defs = json.load(open(os.path.join(DATA, "defs.json")))
vanilla = json.load(open(os.path.join(DATA, "vanilla.json")))
order = [l.strip() for l in open(sys.argv[1], encoding="utf-8") if l.strip()]
_, _, family, owner = merged(order, defs, vanilla)
for wanted in sys.argv[2:]:
    levels = defaultdict(list)
    for name, (fam, level, emblematic) in family.items():
        if fam == wanted and level is not None:
            levels[level].append(f"{name}{'*' if emblematic else ''} [{owner[name]}]")
    print(f"== {wanted}")
    for level in sorted(levels):
        print(f"  {level:>3}: {', '.join(levels[level])}")
