"""Offline rule: a common unit the minor factions' army upgrade can step onto must be unlocked by a technology.
Only a technology sets a unit's EraOfUnlock (DataController.SetConstructibleAsNeededToBeUnlocked), and the
upgrade (BaseHumanMinorFactionUtils.GetUpgradedUnitDefinition) reads it unchecked on every rung it steps onto
at an era change, so the turn never begins (soak e, turn 88). The walk starts one rung above a unit and only
enters a family's bottom rung when the family before it leads there (SerializableNextFamilyName).
unlock_check.py <order.txt>; exits 1 on a problem."""
import sys
from collections import defaultdict

import UnityPy

from extract_defs import read_bundles
from find_defs import order_bundles
from module_identity import script_name
from status_check import BASE

UNIT_PREFIXES = ("LandUnit_", "NavalUnit_", "NavalTransport_", "AirUnit_", "MissileUnit_")


def problems(techs, families, next_family):
    """techs: {name: [unlock groups]}; families: {constructible: [family, level, emblematic]};
    next_family: {family: the family its ladder continues into}. Final definers only."""
    unlocked = {name for groups in techs.values() for group in groups for name in group}
    common = defaultdict(list)
    for name, (family, level, emblematic) in families.items():
        if name.startswith(UNIT_PREFIXES) and family and level is not None and not emblematic:
            common[family].append((level, name))
    entered = set(next_family.values())
    found = []
    for family, rungs in sorted(common.items()):
        bottom = min(level for level, _ in rungs)
        for level, name in sorted(rungs):
            reachable = level > bottom or family in entered
            if reachable and name not in unlocked:
                found.append(f"{name} (common, {family}#{level}) isn't unlocked by any technology")
    return found


def read_next_families(bundle, next_family):
    for obj in UnityPy.load(bundle).objects:
        if obj.type.name != "MonoBehaviour" or script_name(obj) != "UnitFamilyDefinition":
            continue
        tree = obj.read_typetree()
        if tree.get("SerializableNextFamilyName"):
            next_family[tree["m_Name"]] = tree["SerializableNextFamilyName"]
        else:
            next_family.pop(tree["m_Name"], None)


if __name__ == "__main__":
    order = [line.strip() for line in open(sys.argv[1], encoding="utf-8") if line.strip()]
    techs, families, next_family = {}, {}, {}
    for _, bundle in [("base game", BASE), *order_bundles(order)]:
        found = read_bundles([bundle])
        techs.update(found["techs"])
        families.update(found["families"])
        read_next_families(bundle, next_family)
    found = problems(techs, families, next_family)
    for problem in found:
        print(problem)
    print(f"{len(found)} problem(s) in {sum(1 for n in families if n.startswith(UNIT_PREFIXES))} units")
    sys.exit(1 if found else 0)
