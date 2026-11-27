"""Offline rule: a narrative event must apply a status to the kind of entity the status starts on.
A unit status applied to an army indexes past the army's properties (AddPropertyEffects) and every
later simulation refresh throws, so the turn never ends (run 10b soak, Eventful's Neolithic05).
status_check.py <order.txt>; exits 1 on a mismatch."""
import json
import sys

import UnityPy

from find_defs import order_bundles
from odin_blob import apply_status_effects, declared_variables

BASE = "/mnt/d/SteamLibrary/steamapps/common/Humankind/AssetBundles/MercuryDatabases/mercurydatabases.assetbundle"
# The entity kind a target names, by its last segment; unknown targets are not judged. Empire targets
# aren't judged either: the base game applies city statuses to "Empire" (NarrativeEvent_Regular13).
TARGET_KIND = {"Army": "Army", "Armies": "Army", "Unit": "Unit", "Units": "Unit", "City": "Settlement",
               "Cities": "Settlement", "Settlement": "Settlement"}
START_KIND = {"Army": "Army", "Unit": "Unit", "Settlement": "Settlement", "MajorEmpire": "Empire", "Empire": "Empire"}


def read(bundle):
    statuses, events = {}, {}
    for obj in UnityPy.load(bundle).objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            tree = obj.read_typetree()
        except Exception:
            continue
        name = tree.get("m_Name", "")
        if "StartingType" in tree and name.startswith("Status_"):
            statuses[name] = tree["StartingType"].split(",")[0].rsplit(".", 1)[-1]
        elif name.startswith("NarrativeEvent") and "serializationData" in tree:
            blob = bytes(tree["serializationData"]["SerializedBytes"])
            declared = declared_variables(blob)
            # A declared variable's type says what it holds; otherwise judge by the name's last word.
            events[name] = [(target, declared.get(target, target.rsplit(".", 1)[-1]), status)
                            for _, target, status in apply_status_effects(blob)]
    return statuses, events


def main():
    order = [line.strip() for line in open(sys.argv[1], encoding="utf-8") if line.strip()]
    statuses, events, owner = {}, {}, {}
    for label, bundle in [("base game", BASE), *order_bundles(order)]:
        found_statuses, found_events = read(bundle)
        statuses.update(found_statuses)
        events.update(found_events)
        owner.update({name: label for name in found_events})
    problems = []
    for event, effects in sorted(events.items()):
        for target, holds, status in effects:
            target_kind = TARGET_KIND.get(holds)
            start_kind = START_KIND.get(statuses.get(status, ""))
            if target_kind and start_kind and target_kind != start_kind:
                problems.append({"event": event, "mod": owner[event], "target": target, "status": status,
                                 "status starts on": start_kind})
    for problem in problems:
        print(json.dumps(problem))
    print(f"{len(problems)} problem(s) in {len(events)} narrative events")
    sys.exit(1 if problems else 0)


if __name__ == "__main__":
    main()
