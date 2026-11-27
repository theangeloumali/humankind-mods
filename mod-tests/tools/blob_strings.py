"""Read-only: prints the strings in named definitions' Odin blobs, from an optional marker on.
blob_strings.py <bundle> <name>[,<name>...] [<marker> [<count>]]"""
import re
import sys

import UnityPy

UTF16 = re.compile(rb"(?:[\x20-\x7e]\x00){2,}")
names = set(sys.argv[2].split(","))
marker = sys.argv[3] if len(sys.argv) > 3 else None
count = int(sys.argv[4]) if len(sys.argv) > 4 else 400
shown = 0
for obj in UnityPy.load(sys.argv[1]).objects:
    if obj.type.name != "MonoBehaviour":
        continue
    try:
        tree = obj.read_typetree()
    except Exception:
        continue
    name = tree.get("m_Name", "")
    if name not in names and not ("*" in names and name.startswith("NarrativeEvent")):
        continue
    strings = [s.decode("utf-16-le") for s in UTF16.findall(bytes((tree.get("serializationData") or {}).get("SerializedBytes") or b""))]
    if marker:
        hits = [i for i, s in enumerate(strings) if marker in s]
        if not hits:
            continue
        strings = strings[hits[0]:hits[0] + count]
    print(f"{name}: " + " | ".join(strings[:count]))
    shown += 1
    if "*" in names and shown >= 2:
        break
