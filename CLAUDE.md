# RimWorld IFTTT Automation Mod

## Project Goal

A comprehensive **If-This-Then-That (IFTTT)** automation framework for RimWorld 1.6 (Odyssey). The mod polls game state on a configurable tick interval and evaluates user-defined rules — each rule combines one or more **triggers** (state queries) with one or more **actions** (responses). The architecture is designed to be **generic and mod-agnostic**: seven universal triggers (Thing Count, Pawn State, Pawn Property, Map State, Variable, Shuttle State, Pawn Condition) can query virtually any game property, and five universal actions (Use Item, Cast Ability, Set Forbidden, Set Variable, Designate) can interact with any mod's defs via dropdown menus populated from `DefDatabase<T>`. **41 triggers** and **35 actions** total.

This is a polling-based system, not event-driven. Every N ticks (default 250), the `AutomationGameComp` evaluates all enabled rules sorted by priority. Since we're pulling state on a frequency, any game information that's readable at tick-time is fair game for triggers.

## Build & Install

```bash
# Build
cd Source && dotnet build RimworldAutomation.csproj

# Install (copies to Steam RimWorld/Mods/RimWorldIFTTT/)
powershell -ExecutionPolicy Bypass -File install.ps1
powershell -ExecutionPolicy Bypass -File install.ps1 -NoBuild    # skip build
```

- **Output DLL**: `1.6/Assemblies/RimWorldIFTTT.dll`
- **Target**: `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\RimWorldIFTTT\`
- **DO NOT** manually copy to the Steam game folder — always use `install.ps1`
- **DLL lock**: RimWorld must be closed before install (DLL is memory-mapped while running)

## Project Structure

```
Source/
  RimworldAutomation.csproj        # SDK-style, net472, LangVersion=9.0
  AutomationMod.cs                 # Mod entry point (logs load message)
  AutomationRule.cs                # Rule model: triggers + actions + cooldown/priority/limits
  AutomationGameComp.cs            # GameComponent: ticks every N ticks, evaluates rules
  Core/
    AutomationTrigger.cs           # Abstract trigger base (Label, Description, HasConfig, IsTriggered, DrawConfig, ExposeData)
    AutomationAction.cs            # Abstract action base  (Label, Description, HasConfig, Execute, DrawConfig, ExposeData)
    TriggerRegistry.cs             # List<Type> of all 41 trigger types
    ActionRegistry.cs              # List<Type> of all 35 action types
    PawnFilter.cs                  # Shared: PawnKindFilter enum, PawnFilterHelper static class
    RuleCategory.cs                # Enum: All, Combat, ColonyManagement, Economy, Social, Medical, Research, Notifications, Custom
    TriggerMode.cs                 # Enum: All (AND), Any (OR)
  Triggers/
    Generic/                       # Universal configurable triggers
      Trigger_ThingCount.cs        #   Any ThingDef, comparator (>=/<=/=), threshold, map-wide or stockpile
      Trigger_PawnState.cs         #   Any pawn property: hediff/need/skill/trait/capacity/state flags + pawn/race/zone filters
      Trigger_PawnProperty.cs      #   Runtime-reflective: any scalar (float/bool) prop on pawn trackers + mod comps
      Trigger_MapState.cs          #   Any map property: weather/temperature/season/time/fire count/colony wealth
      Trigger_Variable.cs          #   State machine: read a named numeric variable (6 comparators)
      Trigger_ShuttleState.cs      #   Transport shuttle state: docked/loading/ready/gone/waiting/leaving-soon
      Trigger_PawnCondition.cs     #   Legacy hediff-only trigger (backward compat)
    Combat/                        # Trigger_ColonistDowned, _FireOnMap, _EnemyCount, _MechanoidPresent, _RaidIncoming
    Colony/                        # Trigger_FoodLow, _ItemCount, _PawnMoodLow, _ColonistIdle, _PowerFailure,
                                   #   _SeasonIs, _TimeOfDay, _WeatherIs, _AnimalTamable, _AnimalOverpopulated,
                                   #   _AnimalGearWornOut, _ColonistCount, _ResearchQueueEmpty, _BedCapacity,
                                   #   _MentalBreak, _SkillTrainerAvailable, _AnimalMissingHediff
    Economy/                       # Trigger_WealthAbove, _StockpileItem, _TraderArrived, _DrillResourceAbove
    Social/                        # Trigger_FactionGoodwillBelow, _PrisonerPresent, _OrbitalTraderPresent, _NeutralFactionNearby
    Medical/                       # Trigger_PawnBloodLoss, _PawnHasDisease, _PawnHasInjury, _PawnToxicityHigh
    (root)                         # Trigger_HasSkillTrainer, _UnderAttack, _CanContactFaction, _SurplusSilver, _VisitorPresent
  Actions/
    Generic/                       # Universal configurable actions
      Action_UseItemOnPawn.cs      #   Any CompUsable item on filtered pawns (replaces UseSkillTrainer, ApplySentienceCatalyst)
      Action_CastAbility.cs        #   Any AbilityDef (vanilla + VPE), self or target, hediff skip filter
      Action_SetForbidden.cs       #   Forbid/allow any ThingDef or building on map
      Action_SetVariable.cs        #   State machine: set/add/subtract/multiply/reset a named variable
      Action_Designate.cs          #   Generic: place any designation (mine/hunt/cut/haul/etc.) + ThingDef filter
    (Actions/ root)                # Action_SetAllowedArea — set pawn area restriction (unrestricted or named area)
                                   # Action_SetWorkPriority — set work type priority (0-4) for filtered pawns
                                   # Action_DraftAllShooters, _UndraftAll, _TameAnimal, _SlaughterAnimal,
                                   #   _SetResearchProject, _SetPawnSchedule, _ToggleWorkType, _HaulToStockpile,
                                   #   _ForbidAllItems, _UseSkillTrainerOnBest, _ReplaceAnimalGear,
                                   #   _ApplySentienceCatalyst, _CheerUpPawn, _RecruitPrisoner, _ReleasePrisoner,
                                   #   _ContactOrbitalTrader, _TendInjured, _RescuePawn, _SetDeepDrillForbidden,
                                   #   _PauseGame, _SendAlert, _DropSupplyDrop, _NotifyPlayer, _GiftSilver,
                                   #   _DraftShooters, _CallAlliedTrader, _UseSkillTrainer
  Jobs/
    JobDriver_ReplaceAnimalGear.cs # Custom 4-toil job: goto item -> pick up -> goto animal -> Wear()
  UI/
    MainTabWindow_Automation.cs    # Main tab: category tabs, rule list, log panel
    Dialog_AddEditRule.cs          # Edit dialog: multi-trigger, multi-action, all rule settings
    Dialog_AutomationSettings.cs   # Settings: check interval, verbose logging
1.6/
  Assemblies/                      # Built DLL output
  Defs/
    MainButtonDefs.xml             # IFTTT bottom-bar tab button
    JobDefs/JobDefs_IFTTT.xml      # Custom JobDef for IFTTT_ReplaceAnimalGear
About/About.xml                    # Mod metadata (packageId, supportedVersions, description)
Tests/
  RimworldAutomation.Tests.csproj  # NUnit test project
  AutomationRuleTests.cs           # ~20 unit tests (CanFire, TriggerMode, cooldown, etc.)
  RegistryTests.cs                 # Registry integrity tests
  Stubs/                           # StubTrigger.cs, StubAction.cs
install.ps1                        # PowerShell installer (copies to Steam Mods folder)
uninstall.ps1                      # Uninstaller
```

## Architecture

### Execution Flow

1. `AutomationGameComp.GameComponentTick()` runs every `checkIntervalTicks` (250 = ~4 real seconds)
2. All enabled rules are sorted by priority (lower number = higher priority)
3. Each rule gates itself by its own `checkFrequencyTicks` and `cooldownTicks`
4. For each eligible rule: evaluate triggers according to `TriggerMode` (AND/OR) with per-trigger negate
5. If the trigger condition is met and cooldown/limits allow: execute all actions in order
6. Log the fire event to the ring buffer (100 max) for the UI log panel

### Rule Model (AutomationRule)

```
TriggerGroups: List<TriggerGroup>         (each group: List<TriggerEntry> + TriggerMode)
  TriggerGroup.mode: All (AND) | Any (OR) — triggers within the group
  TriggerEntry: AutomationTrigger + negate bool
  Groups are always AND-ed at the top level
Actions: List<AutomationAction>           (THEN branch, ordered execution)
  actionGuards: parallel List<AutomationTrigger> (per-action guard; null = always run)
ElseActions: List<AutomationAction>       (ELSE branch, runs when triggers don't match)
  elseActionGuards: parallel guard list
Settings: name, notes, category, priority, enabled
Timing: checkFrequencyTicks, cooldownTicks
Limits: oneShotRule, maxFires (0 = unlimited)
State: lastCheckedTick, lastFiredTick, totalFireCount, sessionFireCount
```

### Save/Load

All rules, triggers, and actions implement `IExposable`. Polymorphic serialization uses `Scribe_Deep.Look` for triggers and actions within each rule. Field-level persistence uses `Scribe_Values.Look`.

### Adding a New Trigger

1. Create a class extending `AutomationTrigger` in the appropriate `Triggers/` subfolder
2. Override: `Label`, `Description`, `IsTriggered(Map map)`
3. If configurable: override `HasConfig => true`, `ConfigHeight`, `DrawConfig(Listing_Standard)`, `ExposeData()`
4. Register in `TriggerRegistry.AllTypes`

### Adding a New Action

1. Create a class extending `AutomationAction` in the appropriate `Actions/` subfolder
2. Override: `Label`, `Description`, `Execute(Map map)`
3. If configurable: override `HasConfig => true`, `ConfigHeight`, `DrawConfig(Listing_Standard)`, `ExposeData()`
4. Register in `ActionRegistry.AllTypes`

### UI Dropdown Pattern (for generic triggers/actions)

Use `FloatMenu` populated from `DefDatabase<T>`:

```csharp
if (Widgets.ButtonText(listing.GetRect(28f), buttonLabel))
{
    var opts = new List<FloatMenuOption>();
    foreach (SomeDef d in DefDatabase<SomeDef>.AllDefsListForReading
        .Where(d => !d.label.NullOrEmpty())
        .OrderBy(d => d.label))
    {
        string dn = d.defName;
        opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(), () => fieldName = dn));
    }
    Find.WindowStack.Add(new FloatMenu(opts));
}
```

**Important**: Use `Action<T>` setter callbacks (not `ref` parameters) in reusable UI helpers — `ref` parameters cannot be captured in lambda callbacks (CS1628).

### Shared PawnFilterHelper

`PawnFilterHelper` provides reusable UI and filtering methods used by generic triggers and actions:

- `GetPawns(map, kind, zone, race)` — Enumerate pawns with filters
- `DrawKindFilter(current, setter, listing)` — 4-button colonist/animal/prisoner/any selector
- `DrawZoneDropdown(current, setter, rect)` — Area/zone picker from `map.areaManager.AllAreas`
- `DrawRaceDropdown(current, setter, rect)` — Animal race picker from `DefDatabase<ThingDef>`
- `DrawHediffDropdown(current, setter, rect)` — Hediff picker from `DefDatabase<HediffDef>`
- `DrawToggleBtn(rect, label, active, onClick)` — Green-highlighted toggle button

### Generic Trigger Design

The four universal triggers use two complementary approaches:

**Enum + cascading UI** (`Trigger_PawnState`, `Trigger_MapState`):
- `Trigger_PawnState`: `PawnPropertyType` enum (24 values incl. psycasts) delegates all behavior to `PawnQueryRegistry`. Fields are reused (`defName` holds hediffDefName OR needDefName OR skillDefName depending on property type). Handles Def-parameterized properties that need a specific Def chosen.
- `Trigger_MapState`: `MapPropertyType` enum with same pattern.
- `Trigger_ThingCount`: Simpler — single ThingDef + comparator + threshold.

**Runtime reflection** (`Trigger_PawnProperty`):
- Discovers all public scalar (float/bool/int) properties on standard pawn sub-trackers AND on ThingComp subclasses from any loaded mod assembly.
- Uses `PawnPropertyScanner` (lazy one-time scan) and `PawnPropEntry` (cached getter lambdas).
- Grouped dropdown (by tracker type) with separator labels; comparator + TextField for numeric properties.
- Complements `Trigger_PawnState` — use PawnProperty for raw scalar values, PawnState for Def-parameterized queries.

The `ConfigHeight` property is computed dynamically based on the selected property type to prevent wasted UI space.

### Generic Action Design: Avoiding Double-Dispatch

Actions that dispatch jobs to colonists (UseItemOnPawn, CastAbility, ReplaceAnimalGear) must handle **multiple targets** in a single Execute call. Pattern:

```csharp
var usedItems    = new HashSet<Thing>();  // don't claim same item twice
var usedHandlers = new HashSet<Pawn>();   // don't assign same colonist twice

foreach (Pawn target in targets)
{
    if (AlreadyBeingHandled(map, target, itemDef)) continue;  // check CurJob
    Thing item = FindItem(map, itemDef, usedItems);
    if (item == null) break;
    Pawn handler = FindHandler(map, usedHandlers);
    if (handler == null) break;
    // dispatch job...
    usedItems.Add(item);
    usedHandlers.Add(handler);
}
```

## RimWorld 1.6 API Reference

These are **verified correct** for RimWorld 1.6.4633. Many online examples use older API names.

### Corrected API Names

| Wrong (pre-1.6 or common mistake) | Correct (1.6) |
|---|---|
| `FoodSourceNotPlant` | `ThingRequestGroup.FoodSourceNotPlantOrTree` |
| `ResearchManager.currentProj` | `ResearchManager.GetProject()` → returns `ResearchProjectDef` |
| `ResearchManager.currentProj = proj` | `ResearchManager.SetCurrentProject(proj)` |
| `ResearchManager.IsFinished()` | `ResearchProjectDef.IsFinished` (property on the def) |
| `JobDefOf.UseItem` | `JobDefOf.UseNeurotrainer` (for neurotrainer items) |
| `JobDefOf.SocialRelax` | `JobDefOf.GotoAndBeSociallyActive` |
| `TimeAssignmentDefOf.Recreation` | `TimeAssignmentDefOf.Joy` |
| `HaulAIUtility.HaulToStorageJob(p, t)` | `HaulAIUtility.HaulToStorageJob(p, t, bool forced)` (3 params) |
| `Thing.SetForbidden(bool, silent: true)` | `Thing.SetForbidden(bool)` (no named `silent` param) |
| `RaceProperties.wildness` | Does not exist — use `p.BodySize` instead |
| `Ability.IsOnCooldown` | Does not exist as public property — let `Ability.Activate()` handle cooldown checks; it returns false if unable to cast |
| `CompUsable.TryStartUseJob()` returns `Job` | Returns `void` in 1.6 — starts the job internally |
| `pawn.playerSettings.AreaRestriction` | `pawn.playerSettings.AreaRestrictionInPawnCurrentMap` (Area, get/set) |
| `Pawn_PsychicEntropyTracker.CurrentPsyfocusLevel` | `CurrentPsyfocus` |
| `Pawn_PsychicEntropyTracker.CurrentPsychicEntropy / MaxEntropy` | `EntropyRelativeValue` (already 0-1) |

### Correct Usage Examples

```csharp
// Set pawn allowed area (null = unrestricted) — 1.6: per-map property
pawn.playerSettings.AreaRestrictionInPawnCurrentMap = map.areaManager.AllAreas.FirstOrDefault(a => a.Label == label);
pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null; // unrestrict
// Old pre-1.6: pawn.playerSettings.AreaRestriction (renamed in 1.6)

// Set work priority (0 = disabled, 1 = highest, 4 = lowest)
pawn.workSettings.SetPriority(workType, priority);  // pawn.WorkTypeIsDisabled(wt) first
pawn.workSettings.GetPriority(workType);             // read current

// Psyfocus and entropy (Trigger_PawnState + PawnPropertyScanner)
float psyfocus = pawn.psychicEntropy?.CurrentPsyfocus ?? 0f;          // 0-1
float entropy  = pawn.psychicEntropy?.EntropyRelativeValue ?? 0f;      // 0-1 (NOT CurrentEntropy/MaxEntropy)

// Psylink level — via hediff severity (DLC-safe)
HediffDef amp = DefDatabase<HediffDef>.GetNamedSilentFail("PsychicAmplifier");
float level = amp != null ? pawn.health?.hediffSet?.GetFirstHediffOfDef(amp)?.Severity ?? 0f : 0f;

// Deep resource grid (no public GetNextResource on CompDeepDrill)
ThingDef resource = map.deepResourceGrid.ThingDefAt(cell);

// Area/zone checking
bool inHome = map.areaManager.Home[someIntVec3];

// Pawn needs
float moodPct = pawn.needs.TryGetNeed(NeedDefOf.Mood)?.CurLevelPercentage ?? 0f;

// Pawn skills
int level = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;

// Pawn traits
bool hasTrait = pawn.story?.traits?.HasTrait(traitDef) == true;

// Pawn capacities
float moving = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);

// Count items on ENTIRE map (not just stockpiles)
foreach (Thing t in map.listerThings.ThingsOfDef(def))
    if (t.Spawned && !t.Destroyed) total += t.stackCount;

// Count items in stockpiles ONLY
int count = map.resourceCounter.GetCount(def);

// Casting abilities (vanilla + VPE in 1.6)
Ability ab = pawn.abilities?.GetAbility(abilityDef);
ab?.Activate(new LocalTargetInfo(target), LocalTargetInfo.Invalid);

// Map state
WeatherDef weather = map.weatherManager.curWeather;
float temp = map.mapTemperature.OutdoorTemp;
Season season = GenLocalDate.Season(map);
int hour = GenLocalDate.HourOfDay(map);
float wealth = map.wealthWatcher.WealthTotal;

// Transport ships (shuttles) — Royalty/Biotech DLC
List<TransportShip> ships = Find.TransportShipManager.AllTransportShips;
bool docked = ship.ShipExistsAndIsSpawned;             // on map?
bool waiting = ship.Waiting;                            // idle/docked?
bool loading = ship.TransporterComp?.LoadingInProgressOrReadyToLaunch == true;
bool ready = ship.ShuttleComp?.AllRequiredThingsLoaded == true;
bool leaving = ship.LeavingSoonAutomatically;           // auto-depart pending?
ship.ForceJob(ShipJobDefOf.FlyAway);                    // programmatic launch
ship.AddJob(ShipJobMaker.MakeShipJob(ShipJobDefOf.WaitForever)); // queue job

// Designations (1.6 — rewritten DesignationManager)
map.designationManager.AddDesignation(new Designation(mineable, DesignationDefOf.Mine));
map.designationManager.DesignationOn(thing, DesignationDefOf.Mine);  // duplicate check
map.designationManager.DesignationAt(cell, DesignationDefOf.Mine);   // cell check
// New in 1.6: MineVein, FillIn, ExtractTree, EjectFuel, ReleaseAnimalToWild
```

### Stone Chunk/Block Naming Convention

- Chunks: `Chunk{Stone}` (e.g., `ChunkLimestone`, `ChunkGranite`)
- Blocks: `Blocks{Stone}` (e.g., `BlocksLimestone`, `BlocksGranite`)
- Deep-drillable: any `ThingDef` with `deepCommonality > 0` (Silver, Gold, Steel, Plasteel, Uranium, Jade)
- Stone chunks in category: `thingCategories.Any(c => c.defName == "StoneChunks")`

### Odyssey DLC (Sentience Catalyst)

- ThingDef: `SentienceCatalyst`, HediffDef: `SentienceCatalyst`
- Uses `CompProperties_Usable` with `useJob` and `CompProperties_Targetable_SingleAnimal`
- Application: `CompUsable.TryStartUseJob(handler, new LocalTargetInfo(animal))` (void return)

### Animal Gear (AAF - Animal Armor Framework)

- `Pawn_ApparelTracker.Wear(apparel, dropReplacedApparel: true)` — internal API for equipping
- For job-based replacement, use custom `JobDriver` with 4 toils: goto item -> carry -> goto animal -> Wear()

## Enums

```csharp
PawnKindFilter      { Colonist, Animal, Prisoner, Any }
CountComparator     { AtLeast, AtMost, Exactly }
PawnPropertyType    { Hediff, NeedLevel, SkillLevel, Trait, Capacity, IsDrafted, IsIdle, IsDowned, InMentalBreak,
                      Gender, Age, Backstory, Gene, Xenotype, RoyalTitle, Relationship, WorkDisabled,
                      EquippedWeapon, PawnName, SkillPassion, HasAbility, PsylinkLevel, Psyfocus, PsychicEntropy }
MapPropertyType     { Weather, Temperature, Season, TimeOfDay, FireCount, ColonyWealth }
RuleCategory        { All, Combat, ColonyManagement, Economy, Social, Medical, Research, Notifications, Custom }
TriggerMode         { All, Any }
RuleMapScope        { AnyHomeMap, AllHomeMaps, SpecificMap }
ShuttleQueryState   { AnyShuttleDocked, AnyShuttleLoading, AnyShuttleReady, NoShuttleDocked, ShuttleWaiting, ShuttleLeavingSoon }
```

## Testing

```bash
cd Tests && dotnet test RimworldAutomation.Tests.csproj
```

Tests use `StubTrigger` and `StubAction` for mocking. Tests cover: `CanFire` logic, `TriggerMode` AND/OR, cooldown timing, one-shot rules, max-fires limits, priority ordering, and registry integrity (all registered types instantiate without error).

## User Preferences

- **Never** deploy/copy files directly to the Steam game folder — always use `install.ps1`
- Work continuously; spawn subagents where beneficial
- Be exhaustive — this is the "magnum opus" IFTTT mod
- Prefer dropdown menus over text entry for def selection (use `FloatMenu` from `DefDatabase<T>`)
- For items that apply to multiple targets (e.g., multiple thrumbos needing catalyst), handle ALL eligible targets per Execute call with duplicate-dispatch prevention
- For job-based actions, colonists should physically walk to items and targets (not instant application)
- Count entire map by default (`listerThings`), not just stockpiles (`resourceCounter`), unless user specifies otherwise
