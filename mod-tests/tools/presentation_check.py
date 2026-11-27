"""Offline rule: every presentation that names a unit must have figures. The game gives a unit the last
presentation naming it (DataController.InitializeUnitDefinition) and fills a presentation's figure list
from the figures pointing back at it; drawing a unit whose presentation has none throws, and the throw
stops the map updating cities (Sigma Pack's Uybabaete took over the common Archers, soak c).
presentation_check.py <order.txt>; exits 1 on a problem."""
import sys

import UnityPy

from find_defs import order_bundles
from status_check import BASE


def problems(presentations, figures):
    """presentations: {name: {unit, wip}}; figures: {name: {presentation, wip, weight}}, final definers only."""
    counts = {}
    for figure in figures.values():
        if not figure["wip"] and figure["weight"] > 0:
            counts[figure["presentation"]] = counts.get(figure["presentation"], 0) + 1
    return [f"{name} names {entry['unit']} but has no figures, so that unit can't be drawn"
            for name, entry in presentations.items()
            if entry["unit"] and not entry["wip"] and not counts.get(name)]


def read(bundle, presentations, figures):
    """Adds the bundle's definitions; a later bundle's definition replaces an earlier one of that name."""
    for obj in UnityPy.load(bundle).objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            tree = obj.read_typetree()
        except Exception:
            continue
        name = tree.get("m_Name", "")
        if "UnitDefinitionReference" in tree and "PresentationFormationDefinition" in tree:
            presentations[name] = {"unit": tree["UnitDefinitionReference"]["serializableElementName"],
                                   "wip": bool(tree.get("IsWorkInProgress"))}
        elif "SubPawnComposition" in tree and isinstance(tree.get("PresentationUnitDefinition"), dict):
            figures[name] = {"presentation": tree["PresentationUnitDefinition"]["serializableElementName"],
                             "wip": bool(tree.get("IsWorkInProgress")), "weight": tree.get("SpawnWeight", 10)}


if __name__ == "__main__":
    order = [line.strip() for line in open(sys.argv[1], encoding="utf-8") if line.strip()]
    presentations, figures = {}, {}
    for _, bundle in [("base game", BASE), *order_bundles(order)]:
        read(bundle, presentations, figures)
    found = problems(presentations, figures)
    for problem in found:
        print(problem)
    print(f"{len(found)} problem(s) in {len(presentations)} unit presentations")
    sys.exit(1 if found else 0)
