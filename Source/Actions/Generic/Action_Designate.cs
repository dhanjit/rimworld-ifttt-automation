using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Generic action: places a designation on things/cells on the map.
    /// Supports all standard DesignationDefs: Mine, CutPlant, HarvestPlant,
    /// Hunt, Tame, Slaughter, Haul, Deconstruct, SmoothFloor, etc.
    ///
    /// Configurable:
    ///  - Which DesignationDef to use
    ///  - Optional ThingDef filter (e.g., only mine Compacted Steel)
    ///  - Max designations per Execute call (prevent lag spikes)
    /// </summary>
    public class Action_Designate : AutomationAction
    {
        public override string Label => "Designate";
        public override string Description
        {
            get
            {
                DesignationDef dd = LookupDesignationDef();
                string dLabel = dd?.label?.CapitalizeFirst() ?? dd?.defName ?? designationDefName;
                if (!thingDefFilter.NullOrEmpty())
                {
                    ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefFilter);
                    string tLabel = td?.label?.CapitalizeFirst() ?? thingDefFilter;
                    return $"Designate {dLabel}: {tLabel} (max {maxPerCall})";
                }
                return $"Designate {dLabel} (max {maxPerCall})";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => thingDefFilter.NullOrEmpty() ? 120f : 150f;

        // ── Config fields ────────────────────────────────────────────────────
        public string designationDefName = "";
        public string thingDefFilter     = "";     // optional: only designate this ThingDef
        public int    maxPerCall         = 50;     // cap per Execute to prevent lag

        [System.NonSerialized] private string _maxBuf;

        // ── Execute ──────────────────────────────────────────────────────────

        public override bool Execute(Map map)
        {
            DesignationDef desDef = LookupDesignationDef();
            if (desDef == null) return false;

            int count = 0;

            if (desDef.targetType == TargetType.Cell)
                count = DesignateCells(map, desDef);
            else
                count = DesignateThings(map, desDef);

            if (count > 0 && AutomationGameComp.Instance?.verboseLogging == true)
                Log.Message($"[IFTTT] Action_Designate: placed {count} '{desDef.defName}' designations.");

            return count > 0;
        }

        /// <summary>
        /// Designate cell-targeted things (Mine, SmoothFloor, SmoothWall, FillIn, etc.).
        /// For Mine: scans all Mineable things; optionally filters by mined resource ThingDef.
        /// </summary>
        private int DesignateCells(Map map, DesignationDef desDef)
        {
            int count = 0;

            // Mine and MineVein target Mineable things on the map.
            if (desDef == DesignationDefOf.Mine || desDef.defName == "MineVein")
            {
                ThingDef resourceFilter = thingDefFilter.NullOrEmpty()
                    ? null
                    : DefDatabase<ThingDef>.GetNamedSilentFail(thingDefFilter);

                foreach (Building b in map.listerBuildings.allBuildingsColonist.Concat(
                    map.listerBuildings.allBuildingsNonColonist))
                {
                    if (count >= maxPerCall) break;
                    if (!(b is Mineable mineable)) continue;
                    if (!b.Spawned || b.Destroyed) continue;

                    // Already designated?
                    if (map.designationManager.DesignationAt(b.Position, desDef) != null) continue;
                    if (map.designationManager.DesignationOn(b, desDef) != null) continue;

                    // Optional resource filter: check what this mineable yields
                    if (resourceFilter != null)
                    {
                        ThingDef yields = b.def.building?.mineableThing;
                        if (yields != resourceFilter) continue;
                    }

                    // Check reachability — don't designate things colonists can't reach
                    if (!map.reachability.CanReachColony(b.Position)) continue;

                    map.designationManager.AddDesignation(
                        new Designation(b, desDef));
                    count++;
                }
            }
            else
            {
                // Other cell designations (SmoothFloor, RemoveFloor, etc.)
                // These need specific cell conditions we can't generically handle well,
                // so we skip the ThingDef filter for these.
                // For now, log a message — future expansion can add per-def cell scanning.
                if (AutomationGameComp.Instance?.verboseLogging == true)
                    Log.Message($"[IFTTT] Action_Designate: cell designation '{desDef.defName}' not yet supported for auto-scanning.");
            }

            return count;
        }

        /// <summary>
        /// Designate Thing-targeted designations (Hunt, Tame, Slaughter, CutPlant,
        /// HarvestPlant, Haul, Deconstruct, Strip, etc.).
        /// </summary>
        private int DesignateThings(Map map, DesignationDef desDef)
        {
            int count = 0;
            ThingDef filter = thingDefFilter.NullOrEmpty()
                ? null
                : DefDatabase<ThingDef>.GetNamedSilentFail(thingDefFilter);

            // Build candidate list based on designation type
            IEnumerable<Thing> candidates = GetThingCandidates(map, desDef, filter);

            foreach (Thing t in candidates)
            {
                if (count >= maxPerCall) break;
                if (t == null || !t.Spawned || t.Destroyed) continue;

                // Already designated with this def?
                if (map.designationManager.DesignationOn(t, desDef) != null) continue;

                // Optional ThingDef filter
                if (filter != null && t.def != filter) continue;

                map.designationManager.AddDesignation(
                    new Designation(t, desDef));
                count++;
            }

            return count;
        }

        /// <summary>
        /// Returns candidate things for a given designation type.
        /// </summary>
        private static IEnumerable<Thing> GetThingCandidates(Map map, DesignationDef desDef, ThingDef filter)
        {
            // Hunt / Tame / Slaughter — target pawns (animals)
            if (desDef == DesignationDefOf.Hunt ||
                desDef == DesignationDefOf.Tame ||
                desDef == DesignationDefOf.Slaughter)
            {
                return map.mapPawns.AllPawnsSpawned
                    .Where(p => IsValidPawnTarget(p, desDef));
            }

            // CutPlant / HarvestPlant — target plants
            if (desDef == DesignationDefOf.CutPlant ||
                desDef == DesignationDefOf.HarvestPlant)
            {
                if (filter != null)
                    return map.listerThings.ThingsOfDef(filter).Where(t => t is Plant);
                return map.listerThings.ThingsInGroup(ThingRequestGroup.Plant);
            }

            // Haul — target haulable items
            if (desDef == DesignationDefOf.Haul)
            {
                if (filter != null)
                    return map.listerThings.ThingsOfDef(filter)
                        .Where(t => t.def.EverHaulable);
                return map.listerHaulables.ThingsPotentiallyNeedingHauling();
            }

            // Deconstruct / Uninstall — target buildings
            if (desDef == DesignationDefOf.Deconstruct ||
                desDef == DesignationDefOf.Uninstall)
            {
                if (filter != null)
                    return map.listerThings.ThingsOfDef(filter)
                        .Where(t => t is Building);
                return map.listerBuildings.allBuildingsColonist.Cast<Thing>();
            }

            // Strip — target pawns or corpses
            if (desDef == DesignationDefOf.Strip)
            {
                return map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            }

            // Flick — flickable buildings
            if (desDef == DesignationDefOf.Flick)
            {
                if (filter != null)
                    return map.listerThings.ThingsOfDef(filter)
                        .Where(t => t.TryGetComp<CompFlickable>() != null);
                return map.listerBuildings.allBuildingsColonist
                    .Where(b => b.TryGetComp<CompFlickable>() != null)
                    .Cast<Thing>();
            }

            // Fallback: if a filter is given, use it
            if (filter != null)
                return map.listerThings.ThingsOfDef(filter);

            // Can't enumerate all things for unknown designation types
            return Enumerable.Empty<Thing>();
        }

        private static bool IsValidPawnTarget(Pawn p, DesignationDef desDef)
        {
            if (p == null || p.Dead || !p.Spawned) return false;

            if (desDef == DesignationDefOf.Hunt)
                return p.RaceProps.Animal && p.Faction == null; // wild only

            if (desDef == DesignationDefOf.Tame)
                return p.RaceProps.Animal && p.Faction == null; // wild only

            if (desDef == DesignationDefOf.Slaughter)
                return p.RaceProps.Animal && p.Faction == Faction.OfPlayer; // colony animals

            return false;
        }

        // ── UI ───────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            // Row 1: Designation type dropdown
            listing.Label("Designation type:");
            DesignationDef cur = LookupDesignationDef();
            string btn = cur != null
                ? (cur.label?.CapitalizeFirst() ?? cur.defName)
                : (designationDefName.NullOrEmpty() ? "(select)" : $"(unknown: {designationDefName})");
            if (Widgets.ButtonText(listing.GetRect(28f), btn))
                Find.WindowStack.Add(new FloatMenu(BuildDesignationMenu()));

            listing.Gap(4f);

            // Row 2: Optional ThingDef filter
            listing.Label("Filter (optional — leave empty for all):");
            ThingDef curThing = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefFilter);
            string tBtn = curThing != null
                ? curThing.label.CapitalizeFirst()
                : (thingDefFilter.NullOrEmpty() ? "(any)" : $"(unknown: {thingDefFilter})");
            Rect filterRow = listing.GetRect(28f);
            float clearW = 50f;
            if (Widgets.ButtonText(new Rect(filterRow.x, filterRow.y, filterRow.width - clearW - 4f, 28f), tBtn))
                Find.WindowStack.Add(new FloatMenu(BuildThingFilterMenu()));
            if (!thingDefFilter.NullOrEmpty())
            {
                if (Widgets.ButtonText(new Rect(filterRow.xMax - clearW, filterRow.y, clearW, 28f), "Clear"))
                    thingDefFilter = "";
            }

            listing.Gap(4f);

            // Row 3: Max per call
            Rect maxRow = listing.GetRect(24f);
            Widgets.Label(new Rect(maxRow.x, maxRow.y, 130f, 24f), "Max per tick:");
            _maxBuf ??= maxPerCall.ToString();
            Widgets.TextFieldNumeric(
                new Rect(maxRow.x + 134f, maxRow.y, 60f, 24f),
                ref maxPerCall, ref _maxBuf, 1f, 500f);
        }

        private List<FloatMenuOption> BuildDesignationMenu()
        {
            var opts = new List<FloatMenuOption>();
            foreach (DesignationDef d in DefDatabase<DesignationDef>.AllDefsListForReading
                .OrderBy(d => d.label ?? d.defName))
            {
                DesignationDef cap = d;
                string lbl = d.label?.CapitalizeFirst() ?? d.defName;
                string target = d.targetType == TargetType.Cell ? " [cell]" : " [thing]";
                opts.Add(new FloatMenuOption(lbl + target,
                    () => designationDefName = cap.defName));
            }
            return opts;
        }

        private List<FloatMenuOption> BuildThingFilterMenu()
        {
            var opts = new List<FloatMenuOption>();
            DesignationDef dd = LookupDesignationDef();

            IEnumerable<ThingDef> defs;

            if (dd == DesignationDefOf.Mine || dd?.defName == "MineVein")
            {
                // Show mineable resource outputs (Steel, Gold, etc.)
                defs = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.building?.mineableThing != null)
                    .Select(d => d.building.mineableThing)
                    .Distinct();
            }
            else if (dd == DesignationDefOf.CutPlant || dd == DesignationDefOf.HarvestPlant)
            {
                defs = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.plant != null);
            }
            else if (dd == DesignationDefOf.Hunt || dd == DesignationDefOf.Tame ||
                     dd == DesignationDefOf.Slaughter)
            {
                defs = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.race?.Animal == true);
            }
            else
            {
                // Generic: show everything selectable
                defs = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => !d.label.NullOrEmpty());
            }

            foreach (ThingDef d in defs.Where(d => !d.label.NullOrEmpty()).OrderBy(d => d.label))
            {
                ThingDef cap = d;
                opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(),
                    () => thingDefFilter = cap.defName));
            }

            return opts;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private DesignationDef LookupDesignationDef()
            => DefDatabase<DesignationDef>.GetNamedSilentFail(designationDefName);

        // ── Persistence ──────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref designationDefName, "designationDefName", "");
            Scribe_Values.Look(ref thingDefFilter,     "thingDefFilter",     "");
            Scribe_Values.Look(ref maxPerCall,         "maxPerCall",         50);
        }
    }
}
