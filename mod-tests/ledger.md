# Humankind mod combinations: test ledger

Every load order tried on Angelo's PC, with the offline check and what the game did.

How each run works:

- **Write.** The order in `orders/<run>.txt` is written to `Registry.xml` through the mod manager's registry writer. The original list is backed up at `~/humankind-registry-backups/Registry-before-order-fix-2026-11-25.xml`.
- **Launch.** The game is launched through Steam.
- **Judge.** The game's own diagnostics log is the judge.

**Fatal rule (verified in the game's code):** while loading, `RuntimeState_Bootstrapper.Diagnostics_MessageLogged` turns any logged error into an exception. `RuntimeManagerHelper.HandleCrash` then reloads the base game alone. One data error from any mod therefore silently drops every mod.

**Offline check (`check_order`):** replays an order against two of the game's data checks, both in `DataController`:

- every unlock group in a technology must hold one unit or building family;
- a family level can't mix common and emblematic units.

A third rule (`status_check`) replays every narrative event and checks that each status it applies starts on the kind of entity it targets (a unit status on an army hangs the turn).

The checks don't cover text templates (`EffectMapperConfiguration`). Those are judged in game only.

## Runs

| # | Order | Mods | Offline check | Game result | Log |
| --- | --- | --- | --- | --- | --- |
| 00 | Angelo's list as found | 41 | 15 problems | Crash: `Technology_Era1_03` unlock group mixes families. ENCReload moves the Javelin Throwers to `SpearThrower`, and Emblematic Infrastructures, loaded later, groups them with the Boomerang Throwers. Fell back to the base game. | `Diagnostics (2026'11'25 @1609'38''409''')` |
| 00b | Searched order, all 41 | 41 | 0 on the unlock rule (level rule not yet checked) | Crash: common Knarr (ENCReload) in emblematic level `NavalTransport#17` with the Waakaulua (Additional Mechanic Complexity). No order can fix this. | `Diagnostics (2026'11'25 @1618'17''162''')` |
| — | Decision (Angelo) | | | ENCReload and Triple Alliance turned off: they clash with AMC and Sigma Pack, and no patch exists. | |
| 01 | `orders/01-core.txt` | 24 | 0 | Crash: `No default Localization key for Flags 'Value, Property, Condition, Target, PluralCondition'` (flags 283). More Immersive Cultures and VIP_AMC_CSP each replace `EffectMapperConfiguration_Default`. MIC's table has 263, 279 and 283 but not 103; VIP_AMC_CSP's has 103 but not 263, 279 or 283. The last one loaded wins, so the other mod's effect texts break. | `Diagnostics (2026'11'25 @1818'26''543''')` |
| 02 | `orders/02-core.txt` (01 minus More Immersive Cultures and MIC+) | 22 | 0 | **PASS.** All 23 modules loaded with no fallback and 0 errors. "Activated Mods: 22" shown on the menu. New game (Huge world, 10 AIs, Normal pace) reached turn 5 with 0 errors. | `Diagnostics (2026'11'25 @1821'21''042''')` |
| 03 | `orders/03-districts.txt` (02 + Villages Updated, Housing District, Logical Districts Placement) | 25 | 0 | **PASS.** All 26 modules loaded with 0 errors. New game played 3 turns (3 new autosaves) with 0 errors. | `Diagnostics (2026'11'25 @1835'05''109''')` |
| 04 | `orders/04-misc.txt` (03 + Advanced Ancient Naval Warfare, Myth-Naturalwonders, NO NEOLITHIC) | 28 | 0 | **PASS.** All 29 modules loaded with 0 errors. The new game started in the Ancient era (NO NEOLITHIC working) and played 3 turns to turn 5 with 0 errors. | `Diagnostics (2026'11'25 @1844'22''015''')` |
| 05 | `orders/05-mic-after-amc.txt` (04 + More Immersive Cultures and MIC+, loaded after VIP_AMC_CSP) | 30 | 0 | **PASS.** All 31 modules loaded with 0 errors. The startup step that checks every effect's description (where run 01 crashed) ran clean, so MIC's template table covers every effect. The new game played 3 turns with 0 errors. (The runner reported FAIL only because the "Enter a new Era" dialog from NO NEOLITHIC blocked its clicks; the turns were then played by hand.) | `Diagnostics (2026'11'25 @1853'08''343''')` |

| — | Angelo: "we want everything except the redundant, try it as much as possible" | | | The search now also enforces the run-05 rule (MIC after VIP_AMC_CSP). With all 36 non-redundant mods, the only problems left are the 3 ENCReload unit-level clashes. No order can fix them, since each clashing unit has exactly one definer. | |
| 06 | `orders/06-all-but-enc.txt` (everything except the 5 redundant mods, ENCReload and Triple Alliance) | 34 listed, 33 load | 0 | **PASS.** 34 modules (33 mods plus the base game) loaded with 0 errors. Emblematic Infrastructures, Eternal Emblematics and emblematicsforever all load: in this order VIP's cavalry groups win `Technology_Era2_09`. New game played 3 turns (3 new autosaves) with 0 errors. exploitallFIMS never loads (see below). | `Diagnostics (2026'11'25 @1954'00''946''')` |
| 07 | `orders/07-final.txt` (06 without exploitallFIMS), rerun | 33 | 0 | **PASS.** 34 modules loaded with 0 errors; 3 turns played. | `Diagnostics (2026'11'25 @2018'53''830''')` before 08 |
| — | Angelo: "find a way on how we can fix the issues … ENCReload, this is one of the major mods that I really want" | | | Plan: build local fix modules with `tools/zk_modbuilder.py` instead of editing the mods. Plan of record: `../humankind-mod-compat-encreload-2026-11-25.md`. | |
| 08 | `orders/08-spike-fork.txt` (07 + "MissileBoy ZK", a renamed copy, beside the original) | 34 | 0 | Crash: `Descriptor 'Effect_Status_City_Austerity10': Specified set of Parameters 'Value, Property, Target' is invalid`. A fork re-registers its whole donor, so listing it beside the original registers everything twice. **A fork must replace its original.** | `Diagnostics (2026'11'25 @2018'53''830''')` |
| 08b | `orders/08b-spike-fork-replaces.txt` (07 with MissileBoy swapped for MissileBoy ZK) | 33 | 0 | Fell back to the base game with 0 logged errors. ModListGuard's log held the cause: `Missing manifest file for runtime module 'missileboy zk'`. The game refuses a module without the `<bundle>.manifest` text file. The builder now writes it, with `CRC: 0` so Unity skips the checksum. | `Diagnostics (2026'11'25 @2030'51''614''')` |
| 08c | 08b, with the rebuilt fork | 33 | 0 | **PASS.** 34 modules loaded, `missileboy zk` among them, 0 errors; 3 turns played. Renaming a module works. | `Diagnostics (2026'11'25 @2034'58''459''')` |
| 09 | `orders/09-exploitallfims-zk.txt` (07 + "exploitallFIMS ZK", a fork whose RuntimeModule name matches its bundle) | 34 | 0 | **PASS.** 35 modules loaded, `exploitallfims zk` among them, 0 errors; 3 turns played. The original exploitallFIMS stays installed but unlisted (the game still logs its name mismatch while scanning, a warning only). | `Diagnostics (2026'11'25 @2047'02''099''')` |
| 10 | `orders/10-all-with-enc.txt` (36 non-redundant mods with ENCReload, Triple Alliance and exploitallFIMS ZK, then the two ZK ENC Compat patches) | 38 | 0 | Fell back to the base game: `AssetBundleManifest is missing for DatabasePlugin` in both patches, so they never registered and the Knarr clash returned. Unity loads only a container asset's preload range, and the builder had listed each asset plus its script but not what it references. The builder now lists every object an asset reaches, and `verify_module.py` checks this (the real AMC bundle passes the same check). | `Diagnostics (2026'11'25 @2058'07''023''')` |
| 10b | `orders/10-all-with-enc.txt`, with the rebuilt patches | 38 | 0 | **PASS.** 39 modules loaded (ENCReload, Comp Triple Alliance, exploitallFIMS ZK and both patches among them), 0 errors. New game played 3 turns (autosaves 273–275). On exit, Registry.xml still held all 39. | `Diagnostics (2026'11'25 @2100'23''298''')` |
| soak | 10b's save, autoplay to turn 150 | 38 | 0 | **Hung at turn 10**, the first time any set played past turn 5. The player's empire chose `NarrativeEvent_Neolithic05` (Eventful only), whose first choice applies the unit statuses `Status_Unit_Demoralized01` and `Status_Unit_Scared01` to an army. Their effects index past the army's properties (`BaseSimulationEntity.AddPropertyEffects`, logged as Critical), after which every simulation refresh throws and the turn never ends. Replaying autosave 281 reproduced it exactly. Not ENCReload: Eventful has been in every passing set, which simply never reached the event. Its follow-ups `05a` and `05b` make the same mistake. New offline rule `status_check.py`: order 10 has 5 such effects. | `Diagnostics (2026'11'25 @2108'47''533''')` |
| — | Fix | | | "ZK Eventful Fix" patch: `Neolithic05` targets its own `Units` variable; `05a` and `05b` get an `Empire_Units` variable (`ArmyUnits`) beside their `Armies` one, and their statuses target it. That matches the base game, which only ever applies unit statuses to unit variables (`AllMyUnits`, `MyArmyUnits`, ...). | |
| 11 | `orders/11-with-eventful-fix.txt` (10 + "ZK Eventful Fix" last) | 39 | 0, status rule 0 (order 10: 5) | **PASS.** 40 modules, 0 errors (Critical entries now counted too), 3 turns. Autosave 281 had already rotated out, so the exact turn-10 replay couldn't be rerun; this game was then played on to turn 14 with no exception. | `Diagnostics (2026'11'25 @2153'56''350''')` |
| 11b | 11 again, fresh game for the soak | 39 | 0, status rule 0 | **PASS.** 40 modules, 0 errors, 3 turns. `run_combo` now runs both offline rules and reports a held game lock as LOCK BUSY. | |
| soak b | 11b's game, autoplay to turn 150 | 39 | | In game proof of the Eventful fix: at turn 19 all 10 empires chose `NarrativeEvent_Neolithic05` choice 0, and the diagnostics plugin logged both statuses applied to `Units`. Turn 20 followed 28 s later, with 0 simulation exceptions. Stopped at turn 84: Angelo saw cities with no buildings. `Player.log` showed `InvalidOperationException` in `Snapshot.PresentationDataChangedCollection.Invoke` every frame. `Invoke` has no try/finally, so once a handler throws, its cascade counter stays at 1 and every later call throws: the map stops receiving cities and districts, and after a reload the loading screen never clears. The first exception was lost when the relaunch overwrote `Player.log`. | |
| 12 / 11c | order 11 without / with exploitallFIMS ZK | 38 / 39 | 0 | Both PASS with 0 stuck-presentation exceptions by turn 3, and 11c by turn 30. Its player was still Neolithic at turn 30 (no cities), which led to the next finding. exploitallFIMS overrides all 73 district definitions with copies from an old game build (missing 8 fields, e.g. `PlacementPolicies`; `WondrousResourceType` 32 instead of 64); its only intended change is `ExploitationRule_Neighbours_AllFIMS`. Still a suspect, not proven. | |
| 13 | `orders/13-no-neolithic-last.txt`: NO NEOLITHIC (`humankindmoddingtest`) moved after VIP and ENCReload | 39 | 0 | **PASS**, and Ancient era at turn 20. NO NEOLITHIC redefines the Neolithic era stars, which VIP and ENCReload also define; loaded 3rd in orders 10 and 11 it was silently overridden. 0 stuck-presentation exceptions at turn 20. | |
| — | Safety net | | | `CityRedraw.Diagnostics` now patches `PresentationDataChangedCollection.Invoke`: when a handler throws it logs the exception with its stack and resets the counter, so the map keeps updating. Soak c runs order 13 with it to catch the trigger. | |

## Current state

**Final order:** `orders/07-final.txt`, which is run 06 without exploitallFIMS. That's 33 mods, all loading, and it's the largest proven set. On exit the game dropped exploitallFIMS from `Registry.xml` itself, so the file already matches.

**Not loaded (8):**

| Mod | Why | Status |
| --- | --- | --- |
| ENCReload | Its naval rework puts common Knarrs and the Great Galley on the same upgrade levels as emblematic units from AMC (Waakaulua) and Sigma Pack (Janggolan, Trireme). Each of those units has one definer, so no order fixes it. The Triple Alliance author also put that patch on hold, citing a fatal bug in ENC itself, and players report endless "turn pending" hangs (one at turn 150). A custom patch mod could move those 4 units' levels, but it would still carry ENC's own bug. | Off |
| Comp Triple Alliance | Only patches ENCReload | Off with ENCReload |
| exploitallFIMS | Broken by its author's build: its RuntimeModule is named `exploitallFIMS.<guid>.0` but its bundle is `.0.12`. The game logs `Invalid RuntimeModule's name for AssetBundle` and skips it, in every run including Angelo's original list. | Can't load until the author fixes it |
| Prussia, Asuka Japanese, Heian Japanese, Three Cultures of Korea Pack, The Lost Dynasties | Already inside Sigma Pack | Redundant, left off by Angelo's call |

Sources on ENCReload: [Comp Triple Alliance comments](https://steamcommunity.com/sharedfiles/filedetails/comments/2969186772), [Triple Alliance changelog](https://steamcommunity.com/sharedfiles/filedetails/changelog/2969186772), [Amplitude bug report: endless turn pending](https://community.amplitude-studios.com/amplitude-studios/humankind/forums/215-bug-reports/threads/51895-endless-turn-pending-likely-found-new-cause?page=1), [AMC comments](https://steamcommunity.com/sharedfiles/filedetails/comments/3307603664).
