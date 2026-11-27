"""Dumps objects by path id (references left as ids): dump_ids.py <bundle> <path_id>..."""
import json
import sys

import UnityPy

env = UnityPy.load(sys.argv[1])
wanted = {int(x) for x in sys.argv[2:]}
for obj in env.objects:
    if obj.path_id in wanted:
        tree = obj.read_typetree()
        tree.pop("serializationData", None)
        print(f"### {obj.path_id} {obj.type.name}")
        print(json.dumps(tree, ensure_ascii=False)[:2500])
