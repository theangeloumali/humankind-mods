"""Reads a built module back and checks its identity agrees everywhere the game looks.
verify_module.py <module dir> <donor name> [<definition>.<field>=<int>|@<name>|json:<value> ...]; exits 1 on any problem."""
import os
import sys

import UnityPy

from module_edits import get_field, parse_edit
from module_identity import BUNDLE, module_files, reachable, script_name

module_dir, donor = sys.argv[1], sys.argv[2].lower()
expected = [parse_edit(text) for text in sys.argv[3:]]
long_name, bundle_name = module_files(module_dir)
stem = bundle_name[: -len(BUNDLE)]
problems = []
if not os.path.isfile(os.path.join(module_dir, f"{bundle_name}.manifest")):
    problems.append("no <bundle>.manifest beside the bundle")

env = UnityPy.load(os.path.join(module_dir, bundle_name))
if any(name.startswith("CAB-") and donor in name.lower() for name in env.file.files):
    problems.append("CAB name still the donor's")
objects = {obj.path_id: obj for obj in env.objects}
found = {}
for obj in env.objects:
    if obj.type.name == "AssetBundle":
        tree = obj.read_typetree()
        if tree["m_Name"] != bundle_name.lower():
            problems.append(f"AssetBundle m_Name {tree['m_Name']!r}")
        problems += [f"container path names the donor: {p}" for p, _ in tree["m_Container"] if donor in p.lower()]
        # Unity loads only an asset's preload range, so a reference outside it comes back null.
        preload = tree["m_PreloadTable"]
        for path, info in tree["m_Container"]:
            listed = {p["m_PathID"] for p in preload[info["preloadIndex"]: info["preloadIndex"] + info["preloadSize"]]}
            missing = reachable(objects, info["asset"]["m_PathID"]) - listed
            problems += [f"{path}: references {pid} outside its preload range" for pid in sorted(missing)]
    elif obj.type.name == "MonoBehaviour":
        script, tree = script_name(obj), obj.read_typetree()
        if script == "RuntimeModule" and tree["m_Name"].lower() != stem:
            problems.append(f"RuntimeModule m_Name {tree['m_Name']!r} != bundle stem {stem!r}")
        if script == "AssetBundleManifest" and tree["AssetBundleName"] != long_name:
            problems.append(f"AssetBundleManifest AssetBundleName {tree['AssetBundleName']!r}")
        for target, field, value in expected:
            if tree.get("m_Name") == target and field.split(".")[0] in tree:
                actual = get_field(tree, field)
                found[target, field] = {"ref": actual["serializableElementName"]} if isinstance(value, dict) and "ref" in value else actual
for target, field, value in expected:
    if found.get((target, field)) != value:
        problems.append(f"{target}.{field} is {found.get((target, field))}, expected {value}")

print("\n".join(problems) or f"OK {long_name}")
sys.exit(1 if problems else 0)
