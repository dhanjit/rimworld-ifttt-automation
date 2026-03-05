using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Finds every deep drill on the map that is currently mining a specific
    /// resource and either forbids or allows it.
    ///
    /// Set up two rules per resource:
    ///   • When [resource] ≥ threshold  →  Forbid drill
    ///   • When [resource] &lt; threshold  →  Allow drill
    /// </summary>
    public class Action_SetDeepDrillForbidden : AutomationAction
    {
        public override string Label => "Set deep drill forbidden";
        public override string Description =>
            $"{(forbid ? "Forbid" : "Allow")} deep drills mining {ResourceLabel}.";

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 95f; // label + button + gap + toggle button

        public string resourceDefName = "Steel";
        public bool   forbid          = true;

        // ── Helpers ───────────────────────────────────────────────────────────

        private string ResourceLabel
        {
            get
            {
                ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(resourceDefName);
                return d != null ? d.label.CapitalizeFirst() : resourceDefName;
            }
        }

        // ── Execute ───────────────────────────────────────────────────────────

        public override bool Execute(Map map)
        {
            ThingDef targetDef = DefDatabase<ThingDef>.GetNamedSilentFail(resourceDefName);
            if (targetDef == null)
            {
                Log.Warning($"[IFTTT] SetDeepDrillForbidden: resource def '{resourceDefName}' not found.");
                return false;
            }

            int changed = 0;

            // Scan every artificial building on the map for a CompDeepDrill
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
            {
                CompDeepDrill comp = t.TryGetComp<CompDeepDrill>();
                if (comp == null) continue;

                // Check what resource is underground at the drill's cells
                ThingDef drillResource = null;
                foreach (IntVec3 cell in t.OccupiedRect())
                {
                    drillResource = map.deepResourceGrid.ThingDefAt(cell);
                    if (drillResource != null) break;
                }
                if (drillResource != targetDef) continue;

                // Only change state if it differs — avoids unnecessary log spam
                if (t.IsForbidden(Faction.OfPlayer) != forbid)
                {
                    t.SetForbidden(forbid);
                    changed++;
                }
            }

            if (changed > 0)
                Messages.Message(
                    $"[IFTTT] {(forbid ? "Forbidden" : "Allowed")} {changed} deep drill(s) mining {ResourceLabel}.",
                    MessageTypeDefOf.NeutralEvent, historical: false);

            return true;
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Resource:");
            listing.Gap(2f);

            ThingDef current = DefDatabase<ThingDef>.GetNamedSilentFail(resourceDefName);
            string btnLabel  = current != null ? current.label.CapitalizeFirst()
                                               : $"(unknown: {resourceDefName})";

            if (Widgets.ButtonText(listing.GetRect(28f), btnLabel))
                Find.WindowStack.Add(new FloatMenu(BuildResourceMenu()));

            listing.Gap(4f);

            // Toggle button: "Forbid drill" ↔ "Allow drill"
            string toggleLabel = forbid ? "Action: Forbid drill" : "Action: Allow drill";
            if (Widgets.ButtonText(listing.GetRect(28f), toggleLabel))
                forbid = !forbid;
        }

        private List<FloatMenuOption> BuildResourceMenu()
        {
            var options = new List<FloatMenuOption>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.deepCommonality > 0 ||
                            d.thingCategories?.Any(c => c.defName == "StoneChunks") == true)
                .OrderBy(d => d.label))
            {
                ThingDef captured = def;
                options.Add(new FloatMenuOption(
                    def.label.CapitalizeFirst(),
                    () => resourceDefName = captured.defName));
            }
            return options;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref resourceDefName, "resourceDefName", "Steel");
            Scribe_Values.Look(ref forbid,          "forbid",          true);
        }
    }
}
