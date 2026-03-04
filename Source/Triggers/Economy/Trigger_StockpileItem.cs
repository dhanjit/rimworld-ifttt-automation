using RimWorld;
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
        public override string Description => "Fires when a specific item's stockpile count crosses a threshold.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 130f; // Label + TextEntry + TextFieldNumeric + Checkbox

        public string itemDefName   = "Steel";
        public int    threshold     = 100;
        public bool   triggerBelow  = true;

        public override bool IsTriggered(Map map)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(itemDefName);
            if (def == null) return false;

            int count = map.resourceCounter.GetCount(def);
            return triggerBelow ? count < threshold : count > threshold;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label($"Item defName: {itemDefName}");
            itemDefName = listing.TextEntry(itemDefName);

            string buf = threshold.ToString();
            listing.TextFieldNumericLabeled("Count threshold: ", ref threshold, ref buf, 0, 999999);
            listing.CheckboxLabeled("Trigger when BELOW threshold (unchecked = above)", ref triggerBelow);
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
