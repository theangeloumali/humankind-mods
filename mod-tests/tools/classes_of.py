"""Prints the script class of named objects in a bundle: classes_of.py <bundle> <name>..."""
import sys

import UnityPy

names = set(sys.argv[2:])
for obj in UnityPy.load(sys.argv[1]).objects:
    if obj.type.name != "MonoBehaviour":
        continue
    try:
        data = obj.read(check_read=False)
        if data.m_Name in names:
            print(data.m_Name, "->", data.m_Script.read().m_ClassName, obj.path_id)
    except Exception:
        pass
