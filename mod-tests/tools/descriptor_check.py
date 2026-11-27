"""Offline rule: compile every effect the way the game does, against the simulation's own entity contracts
(mod-tests/data/entity-contracts.json, written by CityRedraw.Diagnostics). Two failures, both seen in game:
- load: a path step or target property missing from the declared starting type makes the game drop every mod;
- runtime: the compiled reference and property indexes belong to the starting type, so a district or unit
  carrying an effect declared for another type misreads them and breaks the turn when it is built (Sigma Pack's
  Wetu, soak d turn 54). The base game never does this; an effect with no path and no properties reads nothing.
descriptor_check.py <order.txt>; exits 1 on a problem."""
import json
import os
import sys

import UnityPy

from find_defs import order_bundles
from status_check import BASE

CONTRACTS = os.path.join(os.path.dirname(__file__), "..", "data", "entity-contracts.json")
OWNER_KINDS = {"District": ("Extension_", "District_", "Exploitation"),
               "Unit": ("LandUnit_", "NavalUnit_", "NavalTransport_", "AirUnit_", "MissileUnit_")}


def owner_kind(name):
    return next((kind for kind, prefixes in OWNER_KINDS.items() if name.startswith(prefixes)), None)


def walk(contracts, start, path):
    """(target type, None), or (type reached, the step it lacks)."""
    current = start
    for step in path:
        target = contracts[current]["references"].get(step.lower())
        if target is None or target not in contracts:
            return current, step.lower()
        current = target
    return current, None


def compile_problems(contracts, start, effect):
    """What fails when one effect is compiled from `start`, or None."""
    target, missing = walk(contracts, start, effect["path"])
    if missing:
        return f"path step '{missing}' doesn't exist on {target}"
    # The path may name the concrete type it reaches (a MajorEmpire behind an Empire link), as GeneratePath honours.
    if effect.get("specific") in contracts:
        target = effect["specific"]
    holder = start if effect["on_source"] else target
    absent = [prop.lower() for prop in effect["properties"] if prop.lower() not in contracts[holder]["properties"]]
    if absent:
        return f"{holder} has no {', '.join(absent)}"
    # A formula reads from the target, the source (the starting type) or, with no operations, the holder.
    for side, prop in effect.get("reads", []):
        owner = {"target": target, "source": start}.get(side, holder)
        if prop.lower() not in contracts[owner]["properties"]:
            return f"its formula reads {prop.lower()}, which {owner} doesn't have"
    return None


def problems(owners, effects, contracts):
    """owners: {name: [effect names]}; effects: {name: (starting type, [{path, properties, on_source}])}."""
    found = []
    for name, (start, entries) in effects.items():
        if start not in contracts:
            continue
        for entry in entries:
            problem = compile_problems(contracts, start, entry)
            if problem:
                found.append(f"load: {name} from {start}: {problem}")
                break
    for owner, references in owners.items():
        kind = owner_kind(owner)
        if kind is None:
            continue
        for reference in references:
            start, entries = effects.get(reference, (None, []))
            if start == kind or not any(e["path"] or e["properties"] for e in entries):
                continue
            found.append(f"runtime: {owner} ({kind}) carries {reference}, declared for {start or 'no type'}")
    return found


def read(bundle, owners, effects):
    """Adds the bundle's definitions; a later bundle's definition replaces an earlier one of that name."""
    for obj in UnityPy.load(bundle).objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            tree = obj.read_typetree()
        except Exception:
            continue
        name = tree.get("m_Name", "")
        if "OwnDescriptorReferences" in tree:
            owners[name] = [ref["serializableElementName"] for ref in tree["OwnDescriptorReferences"]]
        if "Effects" in tree and "startingType" in tree:
            start = (tree.get("startingType") or "").split(",")[0].rsplit(".", 1)[-1]
            effects[name] = (start, [{"path": e["Path"]["PropertyToFollow"], "on_source": bool(e.get("ApplyEffectOnSource")),
                                      "specific": (e["Path"].get("specificTargetType") or "").split(",")[0].rsplit(".", 1)[-1],
                                      "properties": [p["TargetProperty"] for p in e.get("PropertyEffects") or []],
                                      "reads": [read for p in e.get("PropertyEffects") or [] for read in formula_reads(p)]}
                                     for e in tree["Effects"]])


GET_FROM_TARGET, GET_FROM_SOURCE = 8, 9  # Amplitude.Framework.Simulation.Operation


def formula_reads(property_effect):
    """(side, property) for each property a formula reads, consumed in order as the game's compiler does."""
    names = property_effect.get("PropertyLocalName") or []
    operations = property_effect.get("RpnOperationStack") or []
    if names and not operations:
        return [("local", names[0])]
    sides = [{GET_FROM_TARGET: "target", GET_FROM_SOURCE: "source"}[op] for op in operations if op in (GET_FROM_TARGET, GET_FROM_SOURCE)]
    return list(zip(sides, names))


if __name__ == "__main__":
    order = [line.strip() for line in open(sys.argv[1], encoding="utf-8") if line.strip()]
    with open(CONTRACTS, encoding="utf-8") as handle:
        contracts = json.load(handle)
    owners, effects = {}, {}
    for _, bundle in [("base game", BASE), *order_bundles(order)]:
        read(bundle, owners, effects)
    found = problems(owners, effects, contracts)
    for problem in found:
        print(problem)
    print(f"{len(found)} problem(s) in {len(effects)} effects")
    sys.exit(1 if found else 0)
