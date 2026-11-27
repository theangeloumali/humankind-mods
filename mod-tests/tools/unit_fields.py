"""Prints the plain (non-blob) fields of named definitions in a bundle: unit_fields.py <bundle> <name>..."""
import sys

import UnityPy

bundle, names = sys.argv[1], set(sys.argv[2:])
for obj in UnityPy.load(bundle).objects:
    if obj.type.name != "MonoBehaviour":
        continue
    try:
        tree = obj.read_typetree()
    except Exception:
        continue
    if tree.get("m_Name") not in names or "serializationData" not in tree:
        continue
    plain = {k: v for k, v in tree.items() if k not in ("serializationData", "m_GameObject", "m_Script", "m_Enabled")}
    print(f"{bundle.split('/')[-2][:12]} {tree['m_Name']}: {plain}")
