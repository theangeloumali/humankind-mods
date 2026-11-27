"""Read-only: for named definitions, prints their field set, pointer references and blob size,
so a patch can tell whether a copy fits another bundle's type. inspect_defs.py <bundle> <name>..."""
import sys

import UnityPy

from module_identity import pointers


names = set(sys.argv[2:])
for obj in UnityPy.load(sys.argv[1]).objects:
    if obj.type.name != "MonoBehaviour":
        continue
    try:
        tree = obj.read_typetree()
        script = obj.read(check_read=False).m_Script.read().m_ClassName
    except Exception:
        continue
    if tree.get("m_Name") not in names or script == "ConstructibleUIMapper":
        continue
    data = tree.get("serializationData") or {}
    own = pointers({k: v for k, v in tree.items() if k not in ("m_Script", "m_GameObject")})
    print(f"{sys.argv[1].split('/')[-2][:12]} {tree['m_Name']} ({script}): {len(tree)} fields, "
          f"blob {len(data.get('SerializedBytes', []))} bytes, unity refs {len(data.get('ReferencedUnityObjects', []))}, "
          f"pointers {len(own)}, level {tree.get('Level')}")
    print("   fields:", ",".join(sorted(tree))[:400])
