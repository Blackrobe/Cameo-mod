---
name: openra-cameo
description: Senior developer agent for the OpenRA Cameo mod. Use when implementing new units/factions/traits, fixing production queue bugs, adding hotkeys, editing YAML rulesets, or modifying C# traits in OpenRA.Mods.Cameo. Understands the full engine/mod layering, production systems, spawner hierarchy, chrome/hotkey wiring, TechTree prerequisites, and harvester behaviour.
argument-hint: Describe the feature to implement or bug to fix. E.g. "Add a new Zerg unit to the larva queue" or "Fix the production timer for harvester units".
---

## Repository Layout

```
Cameo-mod/
  engine/                   # OpenRA engine submodule (do not edit unless necessary)
    OpenRA.Mods.AS/          # Adventure Stories traits (BaseSpawnerMaster lives here)
    OpenRA.Mods.Common/      # Core traits: ProductionQueue, Production, TechTree, Buildable
    mods/common/             # Engine-provided hotkeys, chrome layouts, fluent strings
  OpenRA.Mods.CA/            # Combined Arms traits (ProductionQueueFromSelectionCA, ProductionTabsCAWidget)
  OpenRA.Mods.Cameo/         # Cameo-specific C# traits (LarvaProductionQueue, etc.)
  mods/cameo/                # YAML rulesets, chrome layouts, hotkeys, sequences
    rules/starcraft.yaml     # All Zerg/Terran/Protoss actor definitions
    chrome/ingame-player.yaml# In-game sidebar, production tabs, palette wiring
    hotkeys.yaml             # Cameo-specific hotkey bindings
```

**Build command** (run from repo root):
```powershell
dotnet build --configuration Release --verbosity minimal
```
Close the game before building — the game locks `bin/*.dll`.

---

## Production System Architecture

### Three-layer model
1. **`ProductionQueue`** (engine, per-actor or per-player) — manages the ordered item queue, build timers, TechTree watchers, and calls `BuildUnit()` when an item completes.
2. **`Production`** (engine, per-actor) — spawns the finished unit via an exit cell. Checks `ExitCell` availability; returns `false` if blocked.
3. **`ProductionQueueFromSelectionCA`** (CA, World actor) — when selection changes, finds the best `ProductionQueue` on selected actors (queue-per-actor first, then queue-per-player fallback) and sets `ProductionTabsCAWidget.CurrentQueue`.

### Queue types
- **Per-player** (classic C&C): queue lives on the player actor; any matching `Production` building can fulfil it.
- **Per-actor** (C&C3 / Zerg larva style): queue lives on the unit itself via a named `@TAG`. Selecting that actor opens its own queue.

### Custom `LarvaProductionQueue`
Located at `OpenRA.Mods.Cameo/Traits/LarvaProductionQueue.cs`.
Overrides `BuildUnit()` to:
- Spawn units at the larva's `CenterPosition` (bypasses exit-cell checks entirely)
- Respect `BuildAmount` and `AdditionalActors` from `BuildableInfo` (Zergling pair)
- Kill the larva actor in the next frame via `AddFrameEndTask`

Do **not** override `GetBuildTime` — it should use normal cost-based timing.

---

## TechTree & Prerequisites

- `TechTree.Add(key, prerequisites, limit, watcher)` registers a watcher.
- `TechTree.Update()` re-evaluates all watchers.
- **Race condition**: when an actor is spawned into the world (`w.Add(slave)`), its `ProductionQueue.CacheProducibles()` and `techTree.Update()` must be called immediately after. This is done in `engine/OpenRA.Mods.AS/Traits/BaseSpawnerMaster.cs` → `SpawnIntoWorld`.

---

## Spawner Hierarchy (Zerg Hatchery)

- Hatchery YAML uses `DroneSpawnerMaster` (no suffix) — this is the **engine AS** trait.
- `DroneSpawnerMaster` → `BaseSpawnerMaster` (engine AS) → `SpawnIntoWorld` does `w.Add(slave)`.
- `DroneSpawnerMasterCA` (CA mod) is a **different class** not used by the Zerg hatchery.
- Always check the YAML to confirm which C# class a trait maps to before editing.

---

## YAML Ruleset Conventions

- Actor names are lowercase (`sczergling`, `scdrone`, `sc_zerg_larva`).
- Template actors start with `^` and cannot be instantiated directly.
- `Inherits@TAG: ^TemplateName` — multiple inheritance with unique tags.
- `-TraitName:` removes an inherited trait.
- `TraitName@TAG:` adds a second instance of a trait with a unique tag.
- `Queue: SCLarva` in a unit's `Buildable:` block registers it in the larva production queue.
- `Prerequisites: ~actorname` — tilde means "hide until available" (soft prerequisite).
- `Prerequisites: ~!upgradename` — hide when upgrade is present (negated soft).

---

## Chrome & Hotkeys

### Hotkey wiring path
1. Define the hotkey name + key binding in `mods/cameo/hotkeys.yaml` (or `engine/mods/common/hotkeys/*.yaml`).
2. Reference the hotkey name in the chrome YAML widget field (e.g. `NextProductionTabKey: NextProductionTab`).
3. The widget's `HotkeyReference` field is populated by name at load time.

### Production tab cycling
`ProductionTabsCAWidget.HandleKeyPress` handles `NextProductionTabKey` / `PreviousProductionTabKey`.
`SelectNextTab(reverse)` cycles through all queues in the current `queueGroup`, prioritising completed items.
The T-key group buttons call `SelectNextTab` when the same group is already active (pressing T twice cycles barracks).

### WORLD_KEYHANDLER
`CycleProductionActorsHotkeyLogic` lives in `engine/mods/common/chrome/ingame.yaml` → `WORLD_KEYHANDLER`.
It cycles selection across all `Production` actors the player owns, ordered by production type.
Cameo includes `common|chrome/ingame.yaml` in `mod.yaml` so this is active.

---

## Common Pitfalls

| Symptom | Likely Cause |
|---|---|
| Clicking unit icon does nothing | `RejectsOrders` on the actor blocking `StartProduction`; add `Except: StartProduction, PauseProduction, CancelProduction` |
| New spawned actor's production UI is empty | TechTree watchers not re-evaluated after spawn; fix in `BaseSpawnerMaster.SpawnIntoWorld` |
| Unit produces instantly | `GetBuildTime` override returning `0`; remove the override |
| Only larvae on empty south tiles can produce | `base.BuildUnit` uses `Production.Produce` which checks the exit cell; use direct `CreateActor` + `IPositionable.SetPosition` instead |
| Drone/unit missing from larva palette | `Buildable:` block missing `Queue: SCLarva` |
| Zergling spawns only 1 instead of 2 | Custom `BuildUnit` not reading `AdditionalActors` from `BuildableInfo`; mirror `Production.ProduceActors` logic |
| Edit to `DroneSpawnerMasterCA` has no effect on larvae | Hatchery uses `DroneSpawnerMaster` (engine AS), not `DroneSpawnerMasterCA` (CA mod) |
| Harvester forgets player-ordered field after returning to refinery | Expected — engine's `Harvester.OnDockCompleted` always creates `FindAndDeliverResources(null)`; handled by `HarvesterReturnToField` trait in `OpenRA.Mods.Cameo` |

---

## Harvester System

### Resource collection loop
1. `Harvester.Created` queues `FindAndDeliverResources(null)` on spawn.
2. `FindAndDeliverResources` (engine, `OpenRA.Mods.Common/Activities/`) manages the full harvest-return cycle. It holds `lastHarvestedCell` locally and `orderLocation` for explicit orders.
3. When full, it queues `MoveToDock` → docking runs via `GenericDockSequence`.
4. `Harvester.OnDockCompleted` queues a **new** `FindAndDeliverResources(null)` — discarding the old activity's memory.
5. `GenericDockSequence` then fires `INotifyDockClient.Undocked` on all traits.

### `HarvesterReturnToField` trait
Located at `OpenRA.Mods.Cameo/Traits/HarvesterReturnToField.cs`. Added to `^Harvester` template in `mods/cameo/rules/defaults.yaml`.

Implements:
- `INotifyHarvestAction.MovingToResources` — records `lastHarvestedCell` each time a harvest run begins.
- `INotifyDockClient.Undocked` — fires after `OnDockCompleted`. Walks to the tail of the activity queue, cancels the `FindAndDeliverResources(null)` the engine just appended, and replaces it with `FindAndDeliverResources(lastHarvestedCell)`.
- `IResolveOrder` — watches for `"Dock"` / `"ForceDock"` orders (player right-clicking a refinery). Sets `skipNextReturn = true` so the next `Undocked` call is ignored, letting the harvester search for nearest resources from the refinery instead.

### Key interfaces (engine)
- `INotifyHarvestAction` (`MovingToResources`, `Harvested`, `MovementCancelled`) — fired by `HarvestResource` activity.
- `INotifyDockClient` (`Docked`, `Undocked`) — fired by `GenericDockSequence` after dock animations complete.
- `INotifyDockClientMoving` (`MovingToDock`, `MovementCancelled`) — fired by `MoveToDock` activity.

### "Dock" order flow
`DockClientManager.ResolveOrder` handles `"Dock"` / `"ForceDock"` → queues `MoveToDock(target, forceEnter)`. This is what fires when a player right-clicks a refinery with a harvester selected.
