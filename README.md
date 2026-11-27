# Humankind late-game fixes (BepInEx)

BepInEx 5 plugins for [Humankind](https://store.steampowered.com/app/1124300/HUMANKIND/) that fix late-game problems the game never fixed, found while running a large mod list (44 mods) to turn 150+.

## What it fixes

**Buildings disappear and land looks grayed out late in the game; a reload brings them back.**
Every building, tree and prop is a particle in one GPU pool of 16,777,216 uints. A big late-game map needs more: measured 6.5M at turn 86 (830 districts) and 19.0M at turn 150 (about 7,500 districts). Whatever does not fit is silently dropped each frame, and building particles are spawned once, so a district rebuilt after the pool is full stands empty until a reload. The plugin creates the pool at 33,554,432 (about 134 MB more video memory). In a 30-turn A/B from the same save, the game's pool stayed full from turn 128 while the larger pool peaked at 54%.

**A turn never ends: `DivideByZeroException` in `ArmyGoToAction.CanBeFurthered`.** An army whose units have zero speed now stops instead of breaking the turn.

**A turn never ends: `NullReferenceException` in `GetUpgradedUnitDefinition`.** Independent peoples upgrading onto a unit no technology unlocks now keep their unit.

**Safety net for the 960-layer FX limit.** The renderer can draw at most 960 output layers and never removes one. The plugin warns on screen from 940, and Ctrl+F8 quick-saves and reloads (also automatic at a new turn from 945).

## Install

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (x64) into the Humankind folder, or run `scripts/install-bepinex.sh` from WSL.
2. Build: `dotnet build mods/HumankindMods.sln -p:HumankindDir=<your Humankind folder>` (`-p:Deploy=true` copies the plugins into `BepInEx/plugins/ZKidz`).
3. Start the game. Settings live in `BepInEx/config/com.zkidz.humankind.cityredraw.diagnostics.cfg`: `ParticlePoolSize` (0 keeps the game's size), `WarnAtLayers`, `AutoReloadAtLayers`, `QuickReloadKey`.

The per-turn GPU budget (pool fill, layer count, layers added, CPU particle drops) is logged to `BepInEx/CityRedraw/diagnostics.log`.

## Plugins

- `mods/CityRedraw.Diagnostics`: the fixes above, plus diagnostics (district census Ctrl+F10, camera tour Ctrl+F11).
- `mods/ModListGuard`: keeps the game from silently dropping the mod list after a load error.
- `mods/HumankindAutoPlay`: lets the AI play every empire to a target turn (`scripts/autoplay.sh --turns N`), for soak tests.
- `mods/HumankindMods.Core`: shared, unit-tested logic (`dotnet test mods/HumankindMods.sln`).

## Test harness

`mod-tests/` is the WSL tooling used to test mod lists: load-order files, offline checks of mod data against the game's contracts, fix-module builders and soak scripts. Paths come from `%USERPROFILE%` (override with `HK_DOCS`, `HK_LOCALLOW`, `HUMANKIND_DIR`, `STEAM_EXE`). The load-order writer it calls (`hk-order`) is not included. Tests: `mod-tests/.venv/bin/pytest mod-tests/tests` after `mod-tests/tools/setup.sh`.

## License

MIT
