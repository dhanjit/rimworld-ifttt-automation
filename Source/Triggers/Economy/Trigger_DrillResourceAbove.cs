using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the total quantity of a deep-drillable resource on the map
    /// reaches or exceeds a configured threshold.
    ///
    /// For stone: counts BLOCKS as primary (e.g. BlocksLimestone),
    /// optionally also counting chunks (e.g. ChunkLimestone).
    /// For metals: counts all stacks on the map regardless of storage location.
    ///
    /// Pair with Action_SetDeepDrillForbidden to pause drills when full
    /// and a second rule to resume them when depleted.
    /// </summary>
    public class Trigger_DrillResourceAbove : AutomationTrigger
    {
        public override string Label => "Drill resource above threshold";
        public override string Description =>
            $"Fires when {ResourceLabel} on map ≥ {threshold}" +
            (IsStoneChunk(CurrentDef) ? (includeChunks ? " (blocks + chunks)" : " (blocks only)") : "");

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 130f; // label + button + numeric + checkbox

        public string resourceDefName = "Steel";
        public int    threshold       = 500;
        public bool   includeChunks   = true;

        // ── Helpers ───────────────────────────────────────────────────────────

        private ThingDef CurrentDef =>
            DefDatabase<ThingDef>.GetNamedSilentFail(resourceDefName);

        private string ResourceLabel
        {
            get
            {
                ThingDef d = CurrentDef;
                return d != null ? d.label.CapitalizeFirst() : resourceDefName;
            }
        }

        private static bool IsStoneChunk(ThingDef d) =>
            d?.thingCategories?.Any(c => c.defName == "StoneChunks") == true;

        /// <summary>
        /// Chunk{Stone} → Blocks{Stone}  (e.g. ChunkLimestone → BlocksLimestone)
        /// </summary>
        private static ThingDef FindBlockDef(ThingDef chunkDef)
        {
            if (chunkDef.defName.StartsWith("Chunk"))
            {
                string stone = chunkDef.defName.Substring("Chunk".Length);
                return DefDatabase<ThingDef>.GetNamedSilentFail("Blocks" + stone);
            }
            return null;
        }

        /// <summary>Counts all spawned items of a given def on the map (including ground).</summary>
        private static int CountOnMap(Map map, ThingDef def)
        {
            int total = 0;
            foreach (Thing t in map.listerThings.ThingsOfDef(def))
            {
                if (t.Spawned && !t.Destroyed)
                    total += t.stackCount;
            }
            return total;
        }

        // ── Trigger logic ─────────────────────────────────────────────────────

        public override bool IsTriggered(Map map)
        {
            ThingDef resDef = CurrentDef;
            if (resDef == null) return false;

            int count;

            if (IsStoneChunk(resDef))
            {
                // Stone: count blocks as primary (since 1 chunk = 20 blocks)
                ThingDef blockDef = FindBlockDef(resDef);
                count = blockDef != null ? CountOnMap(map, blockDef) : 0;

                if (includeChunks)
                    count += CountOnMap(map, resDef);
            }
            else
            {
                // Metals / other: count directly
                count = CountOnMap(map, resDef);
            }

            return count >= threshold;
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Resource:");
            listing.Gap(2f);

            ThingDef current = CurrentDef;
            string btnLabel  = current != null ? current.label.CapitalizeFirst()
                                               : $"(unknown: {resourceDefName})";

            if (Widgets.ButtonText(listing.GetRect(28f), btnLabel))
                Find.WindowStack.Add(new FloatMenu(BuildResourceMenu()));

            listing.Gap(4f);

            string buf = threshold.ToString();
            listing.TextFieldNumericLabeled("Minimum count on map:", ref threshold, ref buf, 0, 9999999);

            // Only meaningful for stone (has a chunk form to also count)
            if (IsStoneChunk(current))
                listing.CheckboxLabeled("Also count stone chunks", ref includeChunks);
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
            Scribe_Values.Look(ref threshold,       "threshold",       500);
            Scribe_Values.Look(ref includeChunks,   "includeChunks",   true);
        }
    }
}
