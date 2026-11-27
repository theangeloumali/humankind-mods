"""Read-only: prints the AssetBundle container (path, asset id, preload range) with asset names."""
import sys

import UnityPy

env = UnityPy.load(sys.argv[1])
names = {}
for obj in env.objects:
    try:
        names[obj.path_id] = (obj.type.name, obj.read_typetree().get("m_Name", ""))
    except Exception:
        names[obj.path_id] = (obj.type.name, "?")
for obj in env.objects:
    if obj.type.name != "AssetBundle":
        continue
    tree = obj.read_typetree()
    print("preload table size:", len(tree["m_PreloadTable"]), "| keys:", [k for k in tree if k != "m_PreloadTable"])
    for path, info in tree["m_Container"]:
        asset = info["asset"]["m_PathID"]
        print(f"  {path} -> {names.get(asset)} preload[{info['preloadIndex']}:{info['preloadIndex'] + info['preloadSize']}]")
    print("main asset:", tree.get("m_MainAsset"), "| dependencies:", tree.get("m_Dependencies"))
