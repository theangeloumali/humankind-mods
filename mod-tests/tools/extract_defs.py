"""Extracts, per listed mod, every TechnologyDefinition's unlock groups and every constructible's
family, so load orders can be checked offline: extract_defs.py <report.json> <out.json>."""
import glob
import json
import os
import re
import sys

import hkpaths  # noqa: E402

import UnityPy

ROOTS = {
    "Documents": hkpaths.community(),
    "GameModio": hkpaths.community(),
    "Workshop": "/mnt/d/SteamLibrary/steamapps/workshop/content/1124300",
}
UTF16 = re.compile(rb"(?:[\x20-\x7e]\x00){3,}")


def unlock_groups(blob):
    """Each UnlockConstructible effect's constructible names, in order. Odin names a repeated
    effect type only once, but writes every effect's `ConstructibleReferences` field."""
    strings = [s.decode("utf-16-le") for s in UTF16.findall(blob)]
    groups, current = [], None
    for index, value in enumerate(strings):
        if value == "ConstructibleReferences":
            current = []
            groups.append(current)
        elif value == "SimulationEffectDescriptionOverride":
            current = None
        elif current is not None and value == "serializableElementName" and index + 1 < len(strings):
            name = strings[index + 1]
            if name and not name.startswith("Amplitude."):
                current.append(name)
    return [g for g in groups if g]


def read_bundles(bundles):
    techs, families = {}, {}
    for bundle in bundles:
        for obj in UnityPy.load(bundle).objects:
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                tree = obj.read_typetree()
            except Exception:
                continue
            name = tree.get("m_Name", "")
            if "SerializableFamily" in tree:
                factions = (tree.get("FactionPrerequisite") or {}).get("serializableFactionNames") or []
                # [family, level, emblematic]: DataController groups a family's members by level.
                families[name] = [tree["SerializableFamily"], tree.get("Level"), bool(factions)]
            if name.startswith("Technology_") and "TechnologyTier" in tree and "serializationData" in tree:
                techs[name] = unlock_groups(bytes(tree["serializationData"]["SerializedBytes"]))
    return {"techs": techs, "families": families}


if __name__ == "__main__":
    if sys.argv[1] == "add":
        # add <defs.json> <module id> <bundle>: merges one built module (a fork or patch) into defs.
        defs_path, module_id, bundle = sys.argv[2:5]
        defs = json.load(open(defs_path))
        defs[module_id] = read_bundles([bundle])
        json.dump(defs, open(defs_path, "w"))
        print(f"{module_id}: {len(defs[module_id]['families'])} families, {len(defs[module_id]['techs'])} techs")
        sys.exit(0)
    report, out = sys.argv[1], sys.argv[2]
    if report.endswith(".assetbundle"):
        json.dump(read_bundles([report]), open(out, "w"))
        sys.exit(0)
    mods = sorted((m for m in json.load(open(report))["mods"] if m["listed_at"] is not None), key=lambda m: m["listed_at"])
    result = {}
    for mod in mods:
        folder = os.path.join(ROOTS[mod["location"]], mod["folder"])
        result[mod["id"]] = read_bundles(glob.glob(os.path.join(folder, "*.assetbundle")))
        print(f"{mod['listed_at']:>2} {mod['id']}: {len(result[mod['id']]['techs'])} techs", flush=True)
    json.dump(result, open(out, "w"))
