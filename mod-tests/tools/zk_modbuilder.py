"""Builds local Humankind fix modules from installed mods, never touching the originals.

fork:  a renamed copy of a whole module, with fields of named definitions edited.
patch: a renamed copy holding only the edited definitions, to load after the original.
  zk_modbuilder.py fork|patch <module dir> <out dir> <new name> [--set <object>.<field>=<int>|@<name>]
      [--clone <object>=<new name>] [--retarget-status <event>:<old target>=<new target>]
      [--add-units-variable <event>:<armies>=<new>]
"@<name>" sets a reference field. --clone copies an object under a new name before any --set runs.
The last two edit narrative events' Odin blobs (odin_blob.py).

The game registers every object of each database type a bundle holds
(LoadAllDatabasesFromAssetBundleRequest), so a fork re-registers its whole donor and a patch
must drop everything it doesn't override. A patch keeps its donor's own type tree, so it is
built from the mod whose definitions it edits.
"""
import argparse
from collections import OrderedDict

from module_edits import apply_blob_edits, apply_edits, blob_edit, clone_objects, parse_clone, parse_edit
from module_identity import load_renamed, pointers, reachable, script_name, write_module

# Module plumbing a patch keeps besides its edited definitions.
PLUMBING = {"RuntimeModule", "DatabasePlugin", "AssetBundleManifest", "AssetBundleLoadingConfiguration",
            "AssetBundleContentDescriptor"}


def fork(module_dir, out_dir, new_name, edits, blob_edits=(), clones=()):
    module = load_renamed(module_dir, new_name)
    clone_objects(module, clones)
    apply_edits(module, edits)
    apply_blob_edits(module, blob_edits)
    return write_module(module, out_dir)


def patch(module_dir, out_dir, new_name, edits, blob_edits=(), clones=()):
    module = load_renamed(module_dir, new_name)
    edited = {**clone_objects(module, clones), **apply_edits(module, edits), **apply_blob_edits(module, blob_edits)}
    files = module.env.file.files
    objects = files[next(k for k in files if not k.endswith(".resS"))].objects
    scripts = {pid: script_name(obj) for pid, obj in objects.items() if obj.type.name == "MonoBehaviour"}

    # A collection tells the game which types to load: it is named after its element class, or its
    # script is "<class>Collection". One ConstructibleDefinitionCollection registers every
    # ConstructibleDefinition, whatever its name.
    classes = set(edited.values())
    kept = set(edited)
    collections = {f"{cls}Collection" for cls in classes}
    for pid, script in scripts.items():
        if script in PLUMBING or script in collections or (
                script.endswith("Collection") and module.read(objects[pid]).get("m_Name") in classes):
            kept.add(pid)
    bundle_pid = next(pid for pid, obj in objects.items() if obj.type.name == "AssetBundle")
    kept |= {pid for pid, obj in objects.items() if obj.type.name == "MonoScript"} | {bundle_pid}

    for pid in edited:
        tree = module.read(objects[pid])
        own = {k: v for k, v in tree.items() if k not in ("m_Script", "m_GameObject")}
        dangling = [p for p in pointers(own) if p["m_FileID"] or p["m_PathID"] not in kept]
        if dangling or (tree.get("serializationData") or {}).get("ReferencedUnityObjects"):
            raise SystemExit(f"{tree['m_Name']} points at objects the patch drops")

    for pid in list(objects):
        if pid not in kept:
            del objects[pid]
    # Texture data lives in .resS files; with the textures gone nothing reads it.
    for name in [k for k in files if k.endswith(".resS")]:
        del files[name]

    rebuild_container(module, objects, bundle_pid, kept)
    summary = ", ".join([f"{new} copied from {source}" for source, new in clones]
                        + [f"{target} {field}={value['ref'] if isinstance(value, dict) else value}" for target, field, value in edits]
                        + [f"{event} {kind} {old}->{new}" for kind, event, old, new in blob_edits])
    prune_plumbing(module, objects, scripts, kept, f"Local compatibility patch; load it last. Changes: {summary}")
    return write_module(module, out_dir)


def rebuild_container(module, objects, bundle_pid, kept):
    """Keeps container entries for kept assets and rebuilds the preload ranges they point into."""
    bundle = objects[bundle_pid]
    tree = module.read(bundle)
    groups = OrderedDict()
    for path, info in tree["m_Container"]:
        if info["asset"]["m_PathID"] in kept:
            groups.setdefault(path, []).append(info)
    preload, container = [], []
    for path, infos in groups.items():
        start = len(preload)
        # Unity loads only this range for the path, so it lists everything the assets reach.
        listed = []
        for info in infos:
            for pid in reachable(objects, info["asset"]["m_PathID"], module.read):
                if pid not in listed:
                    listed.append(pid)
        preload += [{"m_FileID": 0, "m_PathID": pid} for pid in listed]
        for info in infos:
            container.append([path, {**info, "preloadIndex": start, "preloadSize": len(preload) - start}])
    tree["m_Container"], tree["m_PreloadTable"] = container, preload
    module.save(bundle, tree)


def prune_plumbing(module, objects, scripts, kept, description):
    """The module loads only the database plugin, and the content list names only kept assets."""
    bundle = next(obj for obj in objects.values() if obj.type.name == "AssetBundle")
    kept_paths = {path.lower() for path, _ in module.read(bundle)["m_Container"]}
    for pid in kept:
        script = scripts.get(pid)
        if script == "RuntimeModule":
            tree = module.read(objects[pid])
            tree["runtimePlugins"] = [p for p in tree["runtimePlugins"] if p["m_PathID"] in kept]
            tree["Author"], tree["Description"], tree["ReleaseNotes"] = "ZKidz (local)", description, ""
            module.save(objects[pid], tree)
        elif script == "AssetBundleContentDescriptor":
            tree = module.read(objects[pid])
            tree["entries"] = [e for e in tree["entries"] if e["filePath"].lower() in kept_paths]
            module.save(objects[pid], tree)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("mode", choices=["fork", "patch"])
    parser.add_argument("module_dir")
    parser.add_argument("out_dir")
    parser.add_argument("new_name")
    parser.add_argument("--set", action="append", default=[], type=parse_edit, dest="edits")
    parser.add_argument("--clone", action="append", default=[], type=parse_clone, dest="clones")
    parser.add_argument("--retarget-status", action="append", default=[], type=blob_edit("retarget"), dest="blob_edits")
    parser.add_argument("--add-units-variable", action="append", default=[], type=blob_edit("units"), dest="blob_edits")
    args = parser.parse_args()
    if args.mode == "patch" and not (args.edits or args.blob_edits or args.clones):
        parser.error("patch needs at least one edit")
    {"fork": fork, "patch": patch}[args.mode](args.module_dir, args.out_dir, args.new_name, args.edits, args.blob_edits,
                                              args.clones)
