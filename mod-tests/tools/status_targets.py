"""Read-only: for every narrative event in a bundle that applies one of the named statuses, prints
each ApplyStatus effect's status and target. status_targets.py <bundle> <status>..."""
import re
import sys

import UnityPy

UTF16 = re.compile(rb"(?:[\x20-\x7e]\x00){1,}")
statuses = set(sys.argv[2:])
for obj in UnityPy.load(sys.argv[1]).objects:
    if obj.type.name != "MonoBehaviour":
        continue
    try:
        tree = obj.read_typetree()
    except Exception:
        continue
    name = tree.get("m_Name", "")
    if not name.startswith("NarrativeEvent"):
        continue
    blob = bytes((tree.get("serializationData") or {}).get("SerializedBytes") or b"")
    strings = [s.decode("utf-16-le") for s in UTF16.findall(blob)]
    if not statuses & set(strings):
        continue
    # Odin writes each ApplyStatus effect's fields in order; show the strings from each effect on.
    for index, value in enumerate(strings):
        if value.endswith("SimulationEventEffect_ApplyStatus"):
            print(name, strings[index + 1: index + 9])
