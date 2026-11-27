"""The game's data checks that drop every mod when they fail (DataController, Amplitude.Mercury.Data):
- every unlock group in a technology must hold one constructible family;
- a family level whose first member is common (no faction) can't hold an emblematic one, and the
  reverse (InitializeConstructibleFamilies)."""
from collections import defaultdict


def merged(order, defs, vanilla):
    tech, family = dict(vanilla["techs"]), dict(vanilla["families"])
    tech_owner = {t: "base game" for t in tech}
    family_owner = {c: "base game" for c in family}
    for mod in order:
        if mod not in defs:
            continue
        for name, groups in defs[mod]["techs"].items():
            tech[name], tech_owner[name] = groups, mod
        for name, entry in defs[mod]["families"].items():
            family[name], family_owner[name] = entry, mod
    return tech, tech_owner, family, family_owner


def conflicts(order, defs, vanilla):
    tech, tech_owner, family, family_owner = merged(order, defs, vanilla)
    found = []
    for name, groups in sorted(tech.items()):
        for group in groups:
            families = {c: family[c][0] for c in group if c in family}
            if len(set(families.values())) > 1:
                detail = ", ".join(f"{c}={f} ({family_owner[c]})" for c, f in families.items())
                found.append(f"unlock {name} (last: {tech_owner[name]}): {detail}")
    levels = defaultdict(list)
    for name, (fam, level, emblematic) in family.items():
        if fam and level is not None:  # no family: never grouped
            levels[(fam, level)].append((name, emblematic))
    for (fam, level), members in sorted(levels.items()):
        if len({emblematic for _, emblematic in members}) > 1:
            detail = ", ".join(f"{n}{'*' if e else ''} ({family_owner[n]})" for n, e in members)
            found.append(f"level {fam}#{level} mixes common and emblematic*: {detail}")
    return found
