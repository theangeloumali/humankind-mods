"""Read-only: shows how a module registers data: the Amplitude AssetBundleManifest object and one
collection's Odin blob and references. Usage: inspect_registration.py <bundle> <collection name>"""
import json
import re
import sys

import UnityPy

env = UnityPy.load(sys.argv[1])
names = {}
for obj in env.objects:
    if obj.type.name == "MonoBehaviour":
        try:
            names[obj.path_id] = obj.read_typetree().get("m_Name")
        except Exception:
            pass
for obj in env.objects:
    if obj.type.name != "MonoBehaviour":
        continue
    tree = obj.read_typetree()
    script = obj.read(check_read=False).m_Script.read().m_ClassName
    if script == "AssetBundleManifest":
        print("## AssetBundleManifest keys:", list(tree))
        print(json.dumps({k: (v if not isinstance(v, list) else v[:4]) for k, v in tree.items()}, ensure_ascii=False)[:1500])
    if tree.get("m_Name") == sys.argv[2] and "serializationData" in tree:
        data = tree["serializationData"]
        blob = bytes(data["SerializedBytes"])
        refs = data.get("ReferencedUnityObjects", [])
        print(f"## collection {sys.argv[2]} ({script}): blob {len(blob)} bytes, {len(refs)} refs")
        print("   refs ->", [names.get(r["m_PathID"]) for r in refs][:10])
        print("   blob strings:", [s.decode("utf-16-le") for s in re.findall(rb"(?:[\x20-\x7e]\x00){3,}", blob)][:20])
        print("   blob head:", blob[:80].hex())
        print("   other keys:", [k for k in tree if k not in ("serializationData",)])
