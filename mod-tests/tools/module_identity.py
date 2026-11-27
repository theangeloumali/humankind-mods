"""Gives a Humankind module a new identity everywhere the game and Unity look.

A module's identity must agree in: the RuntimeModule asset (name and guid; the game requires
name == bundle file stem), the AssetBundle object (name and container paths), Amplitude's
manifest objects, the bundle's inner CAB file names (Unity won't load two bundles holding the
same CAB name), and the `<bundle>.manifest` text file beside it (the game refuses a module
without one).
"""
import hashlib
import os
import re
import shutil
import uuid
from collections import OrderedDict
from dataclasses import dataclass, field
from typing import Callable

import UnityPy

BUNDLE = ".assetbundle"


def module_files(module_dir):
    """The module file (no extension) and its bundle, as the mod manager's scan pairs them."""
    names = os.listdir(module_dir)
    for name in names:
        bundle = f"{name}{BUNDLE}".lower()
        match = next((n for n in names if n.lower() == bundle), None)
        if match and not name.lower().endswith((BUNDLE, ".manifest")):
            return name, match
    raise SystemExit(f"no module file with its bundle in {module_dir}")


def script_name(obj):
    try:
        return obj.read(check_read=False).m_Script.read().m_ClassName
    except Exception:
        return ""


def pointers(value, found=None):
    """Every non-null object pointer inside a type tree."""
    found = [] if found is None else found
    if isinstance(value, dict):
        if set(value) == {"m_FileID", "m_PathID"} and value["m_PathID"]:
            found.append(value)
        for item in value.values():
            pointers(item, found)
    elif isinstance(value, list):
        for item in value:
            pointers(item, found)
    return found


def reachable(objects, start, read=lambda obj: obj.read_typetree()):
    """Path ids of `start` and every object in this file it references, directly or not."""
    seen, todo = set(), [start]
    while todo:
        pid = todo.pop()
        if pid in seen or pid not in objects:
            continue
        seen.add(pid)
        try:
            tree = read(objects[pid])
        except Exception:
            continue  # objects without a type tree (raw data) reference nothing we track
        todo += [p["m_PathID"] for p in pointers(tree) if not p["m_FileID"]]
    return seen


def module_guid(new_name):
    """Derived from the name, so a rebuild after a mod update keeps the module's identity."""
    return uuid.uuid5(uuid.NAMESPACE_URL, f"zkidz-humankind-module/{new_name.lower()}")


def guid_fields(guid):
    raw = guid.bytes
    return [int.from_bytes(raw[i:i + 4], "little", signed=True) for i in range(0, 16, 4)]


def rename_cabs(bundle_file, new_bundle_name):
    """Renames the inner CAB files; returns old -> new for stream paths that point at them."""
    cab = "CAB-" + hashlib.md5(new_bundle_name.encode()).hexdigest()
    renames, files = {}, OrderedDict()
    for name, inner in bundle_file.files.items():
        if name.startswith("CAB-"):
            _, dot, ext = name.partition(".")
            new = cab + (dot + ext if dot else "")
            renames[name] = new
            inner.name = new
            files[new] = inner
        else:
            files[name] = inner
    bundle_file.files = files
    return renames


@dataclass
class Module:
    source_dir: str
    old_long: str
    bundle_name: str
    new_name: str
    new_long: str
    new_bundle: str
    env: object
    swap_identity: Callable[[str], str]
    # UnityPy's read_typetree() re-reads the original bytes even after save_typetree().
    trees: dict = field(default_factory=dict)

    def read(self, obj):
        return self.trees[obj.path_id] if obj.path_id in self.trees else obj.read_typetree()

    def save(self, obj, tree):
        self.trees[obj.path_id] = tree
        obj.save_typetree(tree)


def load_renamed(module_dir, new_name):
    """Loads a module and gives it a new identity everywhere the game and Unity look."""
    old_long, bundle_name = module_files(module_dir)
    match = re.match(r"^(.*)\.([0-9a-fA-F]{32})\.(\d+)\.(\d+)$", old_long)
    major, minor = (match.group(3), match.group(4)) if match else ("1", "0")
    guid = module_guid(new_name)
    new_long = f"{new_name}.{guid.hex}.{major}.{minor}"
    new_bundle = f"{new_long.lower()}{BUNDLE}"
    old_stem = bundle_name[: -len(BUNDLE)].lower()
    old_display = match.group(1) if match else old_long
    # One pass, full stem first: replacing in two passes would rewrite the new name again.
    identity = re.compile(f"{re.escape(old_stem)}|{re.escape(old_display)}", re.I)

    def swap_identity(text):
        return identity.sub(lambda m: new_long.lower() if m.group(0).lower() == old_stem else new_name.lower(), text)

    env = UnityPy.load(os.path.join(module_dir, bundle_name))
    module = Module(module_dir, old_long, bundle_name, new_name, new_long, new_bundle, env, swap_identity)
    cab_renames = rename_cabs(env.file, new_bundle)
    for obj in env.objects:
        kind = obj.type.name
        if kind == "AssetBundle":
            tree = obj.read_typetree()
            tree["m_Name"] = new_bundle
            tree["m_AssetBundleName"] = new_bundle
            tree["m_Container"] = [[swap_identity(path), info] for path, info in tree["m_Container"]]
            module.save(obj, tree)
        elif kind == "Texture2D":
            tree = obj.read_typetree()
            stream = tree.get("m_StreamData") or {}
            for old, new in cab_renames.items():
                if stream.get("path") and old in stream["path"]:
                    stream["path"] = stream["path"].replace(old, new)
                    module.save(obj, tree)
        elif kind == "MonoBehaviour":
            script = script_name(obj)
            if script not in ("RuntimeModule", "AssetBundleManifest", "AssetBundleLoadingConfiguration"):
                continue
            tree = obj.read_typetree()
            if script == "RuntimeModule":
                tree["m_Name"] = new_long
                tree["guid"] = dict(zip("abcd", guid_fields(guid)))
            elif script == "AssetBundleManifest":
                tree["m_Name"] = f"{new_name}.Manifest"
                tree["AssetBundleName"] = new_long
            else:
                for entry in tree.get("assetBundleManifestEntries", []):
                    entry["name"] = f"{new_name}.Manifest"
            module.save(obj, tree)
    return module


def write_module(module, out_dir):
    dest = os.path.join(out_dir, module.new_long)
    os.makedirs(dest, exist_ok=False)
    with open(os.path.join(dest, module.new_bundle), "wb") as handle:
        handle.write(module.env.file.save())
    shutil.copy(os.path.join(module.source_dir, module.old_long), os.path.join(dest, module.new_long))
    write_manifests(module, dest)
    print(f"built {dest}\nmodule id: {module.new_name.lower()}")
    return dest


def write_manifests(module, dest):
    """The game refuses a module whose `<bundle>.manifest` is missing ("Missing manifest file").
    `CRC: 0` tells Unity's LoadFromFile not to check a checksum the rebuilt bundle no longer has."""
    pairs = [(f"{module.bundle_name}.manifest", f"{module.new_bundle}.manifest"),
             (f"{module.old_long}.manifest", f"{module.new_long}.manifest")]
    for old, new in pairs:
        source = os.path.join(module.source_dir, old)
        if not os.path.isfile(source):
            if old.startswith(module.bundle_name):
                raise SystemExit(f"{source} is missing; the game needs it")
            continue
        with open(source, encoding="utf-8") as handle:
            lines = handle.read().splitlines()
        lines = ["CRC: 0" if line.startswith("CRC:") else module.swap_identity(line) for line in lines]
        with open(os.path.join(dest, new), "w", encoding="utf-8", newline="\n") as handle:
            handle.write("\n".join(lines) + "\n")
