"""Searches for the load order with the fewest data-check problems (rules.py) that keeps every
author-stated "A before B" rule. Usage: search_order.py <defs.json> <vanilla.json> <order.txt> [drop ids...]"""
import json
import random
import sys

from rules import conflicts

defs = json.load(open(sys.argv[1]))
vanilla = json.load(open(sys.argv[2]))
current = [l.strip() for l in open(sys.argv[3], encoding="utf-8") if l.strip()]
dropped = set(sys.argv[4:])
base, mods = current[0], [m for m in current[1:] if m not in dropped]

CHAINS = [  # each from the mod authors' own pages
    ["sigma pack", "super pack", "vip modpack", "encreload", "comp triple alliance"],
    ["super pack", "vip modpack", "comp super pack & vip"],
    ["super pack", "vip modpack", "additional mechanic complexity", "vip_amc_csp"],
    ["more cultures asian dynasties", "more immersive cultures", "more cultures asian dynasties mic+"],
    ["more cultures asian dynasties", "vip modpack", "more cultures asian dynasties vip+"],
    # Run 05: MIC's description-template table must be the last one loaded.
    ["vip_amc_csp", "more immersive cultures"],
    ["more cultures rise of rajas", "more cultures rise of rajas vip+"],
    ["vip modpack", "more cultures rise of rajas vip+"],
    ["vip modpack", "infrastructure rework", "infrastructure rework vip"],
    ["vip modpack", "non-obsoleting emblematic districts"],
    # NO NEOLITHIC (humankindmoddingtest) works by redefining the Neolithic era stars, which VIP and
    # ENCReload also define; loaded before them it is silently undone (order 11: Neolithic at turn 30).
    ["vip modpack", "encreload", "humankindmoddingtest"],
    # Our patches move units off ENCReload's ladder levels, so they follow both the mod they edit and ENC.
    ["additional mechanic complexity", "encreload", "zk enc compat amc"],
    ["sigma pack", "encreload", "zk enc compat sigma"],
]
BEFORE = set()
for chain in CHAINS:
    chain = [m for m in chain if m in mods]
    for i, a in enumerate(chain):
        for b in chain[i + 1:]:
            BEFORE.add((a, b))
FIRST = "deepsea reloaded"  # "load DeepSea before any other mods"


def cost(order):
    return len(conflicts([base] + order, defs, vanilla))


def legal(order):
    position = {m: i for i, m in enumerate(order)}
    if FIRST in position and position[FIRST] != 0:
        return False
    return all(position[a] < position[b] for a, b in BEFORE)


def repair(order):
    """Moves rule-breakers forward until every rule holds; the rules are acyclic, so this ends."""
    order = list(order)
    if FIRST in order:
        order.remove(FIRST)
        order.insert(0, FIRST)
    changed = True
    while changed:
        changed = False
        position = {m: i for i, m in enumerate(order)}
        for a, b in BEFORE:
            if position[a] > position[b]:
                order.remove(a)
                order.insert(order.index(b), a)
                changed = True
                break
    return order


random.seed(7)
best = repair(mods)
best_cost = cost(best)
for restart in range(20):
    if best_cost == 0:
        break
    candidate = repair(random.sample(mods, len(mods)) if restart else best)
    candidate_cost = cost(candidate)
    for _ in range(800):
        i, j = random.sample(range(len(candidate)), 2)
        trial = list(candidate)
        trial.insert(j, trial.pop(i))
        if not legal(trial):
            continue
        trial_cost = cost(trial)
        if trial_cost <= candidate_cost:
            candidate, candidate_cost = trial, trial_cost
    if candidate_cost < best_cost:
        best, best_cost = candidate, candidate_cost
print(f"best: {best_cost} problem(s)")
for line in conflicts([base] + best, defs, vanilla):
    print("  " + line[:220])
print("\n".join([base] + best))
