"""Lists a bundle's objects by type and name, with its container paths: list_bundle.py <bundle>"""
import collections
import sys

import UnityPy

env = UnityPy.load(sys.argv[1])
for file in env.files.values():
    print("file:", type(file).__name__, getattr(file, "name", ""))
counts = collections.Counter()
for obj in env.objects:
    name = ""
    script = ""
    try:
        tree = obj.read_typetree()
        name = tree.get("m_Name", "")
        if obj.type.name == "MonoBehaviour":
            script = obj.read(check_read=False).m_Script.read().m_ClassName
    except Exception as error:
        name = f"<{error}>"
    counts[(obj.type.name, script)] += 1
    if obj.type.name in ("MonoBehaviour", "AssetBundle", "MonoScript") and counts[(obj.type.name, script)] <= 3:
        print(f"  {obj.path_id} {obj.type.name} {script} {name}")
print({f"{t}/{s}": n for (t, s), n in counts.items()})
for path in list(env.container)[:10]:
    print("container:", path)
