using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires based on stockpile count of a specific item (by defName).
    /// Can trigger when above OR below a threshold.
    /// </summary>
    public class Trigger_StockpileItem : AutomationTrigger
    {
        public override string Label       => "Stockpile item count";
        public override string Description
        {
            get
            {
                ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(itemDefName);
                string lbl = d?.label?.CapitalizeFirst() ?? itemDefName;
                return triggerBelow
                    ? $"Stockpile {lbl} < {threshold}"
                    : $"Stockpile {lbl} > {threshold}";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 110f;

        public string itemDefName   = "Steel";
        public int    threshold     = 100;
        public bool   triggerBelow  = true;

        [System.NonSerialized] private string _buf;

        public override bool IsTriggered(Map map)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(itemDefName);
            if (def == null) return false;

            int count = map.resourceCounter.GetCount(def);
            return triggerBelow ? count < threshold : count > threshold;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            // Row 1: Item dropdown
            listing.Label("Item:");
            ThingDef cur = DefDatabase<ThingDef>.GetNamedSilentFail(itemDefName);
            string btn = cur != null
                ? cur.label.CapitalizeFirst()
                : (itemDefName.NullOrEmpty() ? "(select)" : itemDefName);
            if (Widgets.ButtonText(listing.GetRect(28f), btn))
            {
                var opts = new List<FloatMenuOption>();
                foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.CountAsResource && !d.label.NullOrEmpty())
                    .OrderBy(d => d.label))
                {
                    ThingDef cap = d;
                    opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(),
                        () => itemDefName = cap.defName));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            listing.Gap(4f);

            // Row 2: Threshold
            _buf ??= threshold.ToString();
            listing.TextFieldNumericLabeled("Threshold: ", ref threshold, ref _buf, 0, 999999);

            // Row 3: Below/Above toggle
            listing.CheckboxLabeled("Trigger when BELOW threshold", ref triggerBelow);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref itemDefName,  "itemDefName",  "Steel");
            Scribe_Values.Look(ref threshold,    "threshold",    100);
            Scribe_Values.Look(ref triggerBelow, "triggerBelow", true);
        }
    }
}
