"""The edits zk_modbuilder applies to a loaded module: field sets, reference sets, object clones and
narrative-event blob edits, plus the command-line parsers for each."""
import copy
import hashlib
import json

from module_identity import script_name
from odin_blob import add_units_variable, retarget_status


def apply_edits(module, edits):
    """Edits the named definitions; returns {path id: class} of those edited.
    UI mappers share their unit's name, so only objects that have the field are edited."""
    edited, done = {}, set()
    for obj in module.env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        tree = module.read(obj)
        hits = [(field, value) for target, field, value in edits
                if tree.get("m_Name") == target and field.split(".")[0] in tree]
        if not hits:
            continue
        for field, value in hits:
            set_field(tree, field, value)
        module.save(obj, tree)
        edited[obj.path_id] = script_name(obj)
        done.add(tree["m_Name"])
    missing = {target for target, _, _ in edits} - done
    if missing:
        raise SystemExit(f"no definition with that field for {sorted(missing)} in {module.bundle_name}")
    return edited


def apply_blob_edits(module, blob_edits):
    """Applies (kind, event, old, new) edits to events' Odin blobs; returns {path id: class}."""
    edited, wanted = {}, {event for _, event, _, _ in blob_edits}
    for obj in module.env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        tree = module.read(obj)
        if tree.get("m_Name") not in wanted or "serializationData" not in tree:
            continue
        blob = bytes(tree["serializationData"]["SerializedBytes"])
        # Variables first, so a retarget can point at one this run adds.
        for kind, event, old, new in sorted(blob_edits, key=lambda edit: edit[0] != "units"):
            if event != tree["m_Name"]:
                continue
            if kind == "units":
                blob = add_units_variable(blob, old, new)
            else:
                blob, count = retarget_status(blob, old, new)
                if not count:
                    raise SystemExit(f"{event}: no status effect targets {old!r}")
        tree["serializationData"]["SerializedBytes"] = list(blob)
        module.save(obj, tree)
        edited[obj.path_id] = script_name(obj)
        wanted.discard(tree["m_Name"])
    if wanted:
        raise SystemExit(f"no event named {sorted(wanted)} in {module.bundle_name}")
    return edited


def clone_objects(module, clones):
    """Copies each source object under its new name, beside it in the container; returns {path id: class}."""
    cloned = {}
    objects = module.env.file.files[next(k for k in module.env.file.files if not k.endswith(".resS"))].objects
    by_name = {}
    for obj in objects.values():
        if obj.type.name == "MonoBehaviour":
            by_name.setdefault(module.read(obj).get("m_Name"), obj)
    bundle = next(obj for obj in objects.values() if obj.type.name == "AssetBundle")
    bundle_tree = module.read(bundle)
    for source, new_name in clones:
        if source not in by_name:
            raise SystemExit(f"no object named {source} in {module.bundle_name}")
        original = by_name[source]
        # Derived from the name, so a rebuild gives the copy the same path id.
        pid = int.from_bytes(hashlib.md5(new_name.encode()).digest()[:8], "little", signed=True)
        if pid in objects:
            raise SystemExit(f"path id clash cloning {new_name}")
        # A plain copy.copy trips ObjectReader.__getattr__ on the empty instance.
        twin = object.__new__(type(original))
        twin.__dict__.update(original.__dict__)
        twin.path_id = pid
        objects[pid] = twin
        tree = copy.deepcopy(module.read(original))
        tree["m_Name"] = new_name
        module.save(twin, tree)
        path = next(p for p, info in bundle_tree["m_Container"] if info["asset"]["m_PathID"] == original.path_id)
        bundle_tree["m_Container"].append([path, {"preloadIndex": 0, "preloadSize": 0, "asset": {"m_FileID": 0, "m_PathID": pid}}])
        cloned[pid] = script_name(original)
    module.save(bundle, bundle_tree)
    return cloned


def blob_edit(kind):
    def parse(text):
        event, _, change = text.partition(":")
        old, _, new = change.partition("=")
        return kind, event, old, new
    return parse


def field_parent(tree, field):
    """The container holding a dotted field ("Effects.0.Path.PropertyToFollow") and its last key."""
    *steps, last = field.split(".")
    for step in steps:
        tree = tree[int(step)] if isinstance(tree, list) else tree[step]
    return tree, int(last) if isinstance(tree, list) else last


def set_field(tree, field, value):
    parent, key = field_parent(tree, field)
    # "@name" sets a reference's element name and keeps the reference's other keys.
    parent[key] = {**parent[key], "serializableElementName": value["ref"]} if isinstance(value, dict) and "ref" in value else value


def get_field(tree, field):
    parent, key = field_parent(tree, field)
    return parent[key]


def parse_edit(text):
    """<object>.<field>[.<field>...]=<int> | @<name> | json:<value>; object names hold no dots."""
    target_field, _, value = text.partition("=")
    target, _, field = target_field.partition(".")
    if value.startswith("@"):
        return target, field, {"ref": value[1:]}
    return target, field, json.loads(value[5:]) if value.startswith("json:") else int(value)


def parse_clone(text):
    source, _, new_name = text.partition("=")
    return source, new_name
