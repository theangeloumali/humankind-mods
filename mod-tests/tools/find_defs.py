"""Read-only: which mods in an order (or one bundle) define the named objects, with the status and
effect names in each definition's blob. find_defs.py <order.txt | bundle> <name>..."""
import glob
import re
import sys

import hkpaths  # noqa: E402

import UnityPy

UTF16 = re.compile(rb"(?:[\x20-\x7e]\x00){3,}")
ROOTS = ["/mnt/d/SteamLibrary/steamapps/workshop/content/1124300", hkpaths.community()]


def scan(label, bundle, names):
    for obj in UnityPy.load(bundle).objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            tree = obj.read_typetree()
        except Exception:
            continue
        if tree.get("m_Name") not in names:
            continue
        blob = bytes((tree.get("serializationData") or {}).get("SerializedBytes") or b"")
        strings = [s.decode("utf-16-le") for s in UTF16.findall(blob)]
        print(f"{label}: {tree['m_Name']}")
        print("     " + ", ".join(dict.fromkeys(s for s in strings if re.search(r"Status|Effect_|Descriptor", s)))[:900])


def order_bundles(order):
    for position, module in enumerate(order):
        for root in ROOTS:
            for bundle in glob.glob(f"{root}/**/{glob.escape(module)}*.assetbundle", recursive=True):
                stem = bundle.rsplit("/", 1)[1].lower()
                if stem == f"{module}.assetbundle" or re.match(re.escape(module) + r"\.[0-9a-f]{32}\.", stem):
                    yield f"#{position:>2} {module}", bundle


if __name__ == "__main__":
    names = set(sys.argv[2:])
    if sys.argv[1].endswith(".assetbundle"):
        scan(sys.argv[1].rsplit("/", 1)[1], sys.argv[1], names)
    else:
        order = [line.strip() for line in open(sys.argv[1], encoding="utf-8") if line.strip()]
        for label, bundle in order_bundles(order):
            scan(label, bundle, names)
