"""Reads and edits narrative events inside Odin-serialized definitions (SerializedBytes).

Odin writes a named string as 0x27, the name, then the value; each string is 0x01 (UTF-16), an
int32 length and the text. A reference node is 0x01 (named) or 0x02 (array element), a type entry
(0x2F id + name, or 0x30 id for a type already named), an int32 reference id, its fields, then
0x05. An array is 0x06 with an int64 length, its elements, then 0x07. Odin keeps no byte offsets,
so values and elements can change size in place.
"""
import re

NAMED_STRING = b"\x27"
START_OF_NODE = b"\x03"
VARIABLE_TYPE = "Amplitude.Mercury.Data.Simulation.NarrativeEventVariable_{}, Amplitude.Mercury.Data"
# Ids for added nodes, far above the few dozen an event uses.
ADDED_TYPE_ID, ADDED_REFERENCE_ID = 900, 9000


def odin_string(text):
    return b"\x01" + len(text).to_bytes(4, "little") + text.encode("utf-16-le")


def read_string(blob, pos):
    """(text, end) for the string at pos, or (None, pos) if none starts there."""
    if blob[pos:pos + 1] != b"\x01":
        return None, pos
    length = int.from_bytes(blob[pos + 1:pos + 5], "little")
    end = pos + 5 + length * 2
    return blob[pos + 5:end].decode("utf-16-le", errors="replace"), end


TARGET = NAMED_STRING + odin_string("TargetID")
STATUS_NODE = START_OF_NODE + odin_string("StatusDefinition")
ELEMENT_NAME = NAMED_STRING + odin_string("serializableElementName")


def apply_status_effects(blob):
    """[(value offset, target, status)] for each ApplyStatus effect: a TargetID followed by a
    StatusDefinition node."""
    found, pos = [], blob.find(TARGET)
    while pos >= 0:
        value_at = pos + len(TARGET)
        target, end = read_string(blob, value_at)
        if target is not None and blob.startswith(STATUS_NODE, end):
            name_at = blob.find(ELEMENT_NAME, end)
            status, _ = read_string(blob, name_at + len(ELEMENT_NAME)) if name_at >= 0 else (None, 0)
            found.append((value_at, target, status))
        pos = blob.find(TARGET, pos + 1)
    return found


def retarget_status(blob, old, new):
    """Points every ApplyStatus effect aimed at `old` at `new`; returns (blob, count)."""
    hits = [at for at, target, _ in apply_status_effects(blob) if target == old]
    for at in reversed(hits):  # back to front, so earlier offsets stay valid
        blob = blob[:at] + odin_string(new) + blob[at + len(odin_string(old)):]
    return blob, len(hits)


def int32(value):
    return value.to_bytes(4, "little", signed=True)


def declared_variables(blob):
    """{variable name: kind} for each narrative variable, kind being the type's last word
    ("Units", "Armies", ...)."""
    names, found = {}, {}
    for match in re.finditer(rb"\x2f(....)\x01(....)", blob, re.S):
        text, _ = read_string(blob, match.start() + 5)
        if text:
            names[int.from_bytes(match.group(1), "little")] = text
    for match in re.finditer(rb"[\x01\x02]\x30(....)(....)\x27", blob, re.S):
        type_name = names.get(int.from_bytes(match.group(1), "little"), "")
        record_variable(blob, type_name, match.end() - 1, found)
    for match in re.finditer(rb"[\x01\x02]\x2f(....)", blob, re.S):
        type_name, end = read_string(blob, match.end())
        if type_name:
            record_variable(blob, type_name, end + 4, found)
    return found


def record_variable(blob, type_name, fields_at, found):
    kind = re.match(r"Amplitude\.Mercury\.Data\.Simulation\.NarrativeEventVariable_\w+?_(\w+),", type_name or "")
    name_entry = NAMED_STRING + odin_string("Name")
    if kind and blob.startswith(name_entry, fields_at):
        name, _ = read_string(blob, fields_at + len(name_entry))
        found[name] = kind.group(1)


def add_units_variable(blob, armies_name, units_name):
    """Appends a NarrativeEventVariable_Empire_Units named `units_name` over the same empire as the
    Empire_Armies variable `armies_name`, so unit statuses can target that empire's units."""
    armies_type = odin_string(VARIABLE_TYPE.format("Empire_Armies"))
    name_entry = NAMED_STRING + odin_string("Name") + odin_string(armies_name)
    type_at = blob.find(armies_type)
    if type_at < 0 or blob[type_at - 6:type_at - 4] != b"\x02\x2f":
        raise ValueError(f"no Empire_Armies variable {armies_name!r} named by type here")
    node_at, fields_at = type_at - 6, type_at + len(armies_type) + 4
    if not blob.startswith(name_entry, fields_at):
        raise ValueError(f"the Empire_Armies variable isn't named {armies_name!r}")
    if blob[node_at - 9:node_at - 8] != b"\x06" or int.from_bytes(blob[node_at - 8:node_at], "little") < 1:
        raise ValueError("the variable isn't an element of an array")
    empire_entry = NAMED_STRING + odin_string("EmpireID")
    empire_at = blob.find(empire_entry, fields_at)
    _, fields_end = read_string(blob, empire_at + len(empire_entry))
    if empire_at < 0 or blob[fields_end:fields_end + 2] != b"\x05\x07":
        raise ValueError("expected the variable to be the array's last element, ending in EmpireID")

    fields = odin_string(units_name).join(blob[fields_at:fields_end].split(odin_string(armies_name), 1))
    prerequisites = b"\x01" + odin_string("ElementPrerequisites") + b"\x30"
    inner = fields.find(prerequisites)
    if inner >= 0:  # its nested reference node needs its own reference id
        id_at = inner + len(prerequisites) + 4
        fields = fields[:id_at] + int32(ADDED_REFERENCE_ID + 1) + fields[id_at + 4:]
    node = (b"\x02\x2f" + int32(ADDED_TYPE_ID) + odin_string(VARIABLE_TYPE.format("Empire_Units"))
            + int32(ADDED_REFERENCE_ID) + fields + b"\x05")
    count = int.from_bytes(blob[node_at - 8:node_at], "little")
    blob = blob[:node_at - 8] + (count + 1).to_bytes(8, "little") + blob[node_at:]
    insert_at = fields_end + 1  # after the variable's own 0x05, before the array's 0x07
    return blob[:insert_at] + node + blob[insert_at:]
