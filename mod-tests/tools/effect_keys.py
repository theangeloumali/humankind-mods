"""Prints each EffectMapperConfiguration_Default's description-template keys: effect_keys.py <bundle>..."""
import sys

import UnityPy

for bundle in sys.argv[1:]:
    for obj in UnityPy.load(bundle).objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            tree = obj.read_typetree()
        except Exception:
            continue
        if tree.get("m_Name") != "EffectMapperConfiguration_Default":
            continue
        extra = tree.get("AdditionalEffectKeys") or []
        flags = sorted(entry.get("Flags") for entry in extra)
        print(f"{bundle.split('/')[-1][:44]}: {len(extra)} extra keys; flags={flags}")
        others = {k: (len(v) if isinstance(v, list) else v) for k, v in tree.items() if k not in ("AdditionalEffectKeys", "m_GameObject", "m_Script", "m_Enabled", "m_Name")}
        print(f"   other fields: {others}")
