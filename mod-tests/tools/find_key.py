"""Lists which listed mods' bundles define or mention a database key: find_key.py <report.json> <key>."""
import glob
import json
import os
import re
import sys

import hkpaths  # noqa: E402

import UnityPy

ROOTS = {
    "Documents": hkpaths.community(),
    "GameModio": hkpaths.community(),
    "Workshop": "/mnt/d/SteamLibrary/steamapps/workshop/content/1124300",
}
report, key = sys.argv[1], sys.argv[2].encode()
mods = sorted((m for m in json.load(open(report))["mods"] if m["listed_at"] is not None), key=lambda m: m["listed_at"])
for mod in mods:
    folder = os.path.join(ROOTS[mod["location"]], mod["folder"])
    for bundle in glob.glob(os.path.join(folder, "*.assetbundle")):
        env = UnityPy.load(bundle)
        for obj in env.objects:
            try:
                raw = obj.get_raw_data()
            except Exception:
                continue
            if key not in raw:
                continue
            strings = [s.decode() for s in re.findall(rb"[A-Za-z][A-Za-z0-9_]{5,}", raw)]
            name = strings[0] if strings else "?"
            defines = name == key.decode()
            interesting = [s for s in dict.fromkeys(strings) if re.match(r"(District|Extension|Unit|Infrastructure|Building|Constructible|Wonder|Improvement|Era\d)", s)]
            print(f"{mod['listed_at']:>2} {mod['id']}: {obj.type.name} {name}{' (DEFINES)' if defines else ''}")
            if defines:
                print("     unlocks: " + ", ".join(interesting[:20]))
