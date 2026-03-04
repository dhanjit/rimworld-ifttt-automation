using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    public enum CountComparator { AtLeast, AtMost, Exactly }

    /// <summary>
    /// Generic trigger: fires when the count of any ThingDef on the map
    /// satisfies a configurable comparator against a threshold.
    ///
    /// Works with any item from any mod — just pick from the dropdown.
    /// Counts the entire map by default; optionally stockpile-only.
    /// </summary>
    public class Trigger_ThingCount : AutomationTrigger
    {
        public override string Label       => "Thing count";
        public override string Description => $"{ThingLabel} {ComparatorSymbol} {threshold}" +
                                              $" ({(stockpileOnly ? "stockpile" : "map-wide")})";

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 145f;

        public string         thingDefName  = "Steel";
        public int            threshold     = 100;
        public CountComparator comparator   = CountComparator.AtLeast;
        public bool           stockpileOnly = false;

        // ── Helpers ───────────────────────────────────────────────────────────

        private ThingDef CurrentDef => DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);

        private string ThingLabel
        {
            get { var d = CurrentDef; return d != null ? d.label.CapitalizeFirst() : thingDefName; }
        }

        private string ComparatorSymbol => comparator == CountComparator.AtLeast ? "≥"
                                         : comparator == CountComparator.AtMost  ? "≤" : "=";

        private int CountItems(Map map)
        {
            ThingDef def = CurrentDef;
            if (def == null) return 0;
            if (stockpileOnly)
                return map.resourceCounter.GetCount(def);
            int total = 0;
            foreach (Thing t in map.listerThings.ThingsOfDef(def))
                if (t.Spawned && !t.Destroyed) total += t.stackCount;
            return total;
        }

        // ── Trigger logic ─────────────────────────────────────────────────────

        public override bool IsTriggered(Map map)
        {
            int count = CountItems(map);
            switch (comparator)
            {
                case CountComparator.AtLeast: return count >= threshold;
                case CountComparator.AtMost:  return count <= threshold;
                default:                      return count == threshold;
            }
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Item / resource:");
            ThingDef cur = CurrentDef;
            string btn = cur != null ? cur.label.CapitalizeFirst()
                : (thingDefName.NullOrEmpty() ? "(select item)" : $"(unknown: {thingDefName})");
            if (Widgets.ButtonText(listing.GetRect(28f), btn))
                Find.WindowStack.Add(new FloatMenu(BuildThingMenu()));

            listing.Gap(2f);

            // Comparator row
            Rect row = listing.GetRect(24f);
            float w   = row.width / 3f;
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x,         row.y, w, 24f), "≥ At least", comparator == CountComparator.AtLeast, () => comparator = CountComparator.AtLeast);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w,     row.y, w, 24f), "≤ At most",  comparator == CountComparator.AtMost,  () => comparator = CountComparator.AtMost);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w * 2, row.y, w, 24f), "= Exactly",  comparator == CountComparator.Exactly, () => comparator = CountComparator.Exactly);

            listing.Gap(2f);
            string buf = threshold.ToString();
            listing.TextFieldNumericLabeled("Count threshold:", ref threshold, ref buf, 0, 9_999_999);
            listing.CheckboxLabeled("Stockpile only (not whole map)", ref stockpileOnly);
        }

        private List<FloatMenuOption> BuildThingMenu()
        {
            var opts = new List<FloatMenuOption>();
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.category == ThingCategory.Item && !d.label.NullOrEmpty())
                .OrderBy(d => d.label))
            {
                ThingDef cap = d;
                opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(), () => thingDefName = cap.defName));
            }
            return opts;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref thingDefName,  "thingDefName",  "Steel");
            Scribe_Values.Look(ref threshold,     "threshold",     100);
            Scribe_Values.Look(ref comparator,    "comparator",    CountComparator.AtLeast);
            Scribe_Values.Look(ref stockpileOnly, "stockpileOnly", false);
        }
    }
}
