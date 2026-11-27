"""Read-only: for mounted presentation units, prints how many mount variations each bundle gives
them. Zero makes PresentationUnit.InstantiatePawn throw, which jams the map's update for the rest of
the session (soak c). mount_check.py <bundle> [<bundle> ...]"""
import sys

import UnityPy

CAVALRY, CHARIOT = 1, 2  # UnitSpecification values that need mounts; checked against the base game below


def mounted_units(bundle):
    found = {}
    for obj in UnityPy.load(bundle).objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            tree = obj.read_typetree()
        except Exception:
            continue
        if not tree.get("m_Name", "").startswith("PresentationLandUnit") or "UnitSpecification" not in tree:
            continue
        mounts = tree.get("MountDefinitionsPerVisualAffinities")
        found[tree["m_Name"]] = (tree["UnitSpecification"], None if mounts is None else len(mounts))
    return found


for bundle in sys.argv[1:]:
    units = mounted_units(bundle)
    print(f"{bundle.rsplit('/', 1)[-1][:60]}: {len(units)} presentation units")
    for name, (specification, mounts) in sorted(units.items()):
        if mounts is None or mounts == 0 or name.endswith(("Cataphracts_Default", "EgyptianKingdom_Chariots")):
            print(f"   {name}: specification {specification}, mount sets {mounts}")
