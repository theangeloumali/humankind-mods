"""Lists listed mods that ship a MonoBehaviour of a given script type: find_script.py <report.json> <ScriptName>"""
import glob
import json
import os
import sys

import hkpaths  # noqa: E402

import UnityPy

ROOTS = {
    "Documents": hkpaths.community(),
    "GameModio": hkpaths.community(),
    "Workshop": "/mnt/d/SteamLibrary/steamapps/workshop/content/1124300",
}
report, script = sys.argv[1], sys.argv[2]
for mod in sorted((m for m in json.load(open(report))["mods"] if m["listed_at"] is not None), key=lambda m: m["listed_at"]):
    folder = os.path.join(ROOTS[mod["location"]], mod["folder"])
    for bundle in glob.glob(os.path.join(folder, "*.assetbundle")):
        env = UnityPy.load(bundle)
        names = {}
        for obj in env.objects:
            if obj.type.name == "MonoScript":
                try:
                    names[obj.path_id] = obj.read().m_ClassName
                except Exception:
                    pass
        for obj in env.objects:
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                data = obj.read(check_read=False)
                script_name = names.get(data.m_Script.path_id) or data.m_Script.read().m_ClassName
            except Exception:
                continue
            if script_name == script:
                print(f"{mod['listed_at']:>2} {mod['id']}: {getattr(data, 'm_Name', '?')}")
