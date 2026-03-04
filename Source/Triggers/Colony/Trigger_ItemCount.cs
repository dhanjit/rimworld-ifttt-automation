using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the colony's count of a specific item crosses a threshold
    /// (supports both "below" and "above" modes).
    /// </summary>
    public class Trigger_ItemCount : AutomationTrigger
    {
        public override string Label       => "Item count threshold";
        public override string Description => "Fires when a specific item's count is below or above a threshold.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 130f; // Label + TextEntry + TextFieldNumeric + Checkbox

        public string  thingDefName = "Silver";
        public int     threshold    = 500;
        public bool    triggerBelow = true;   // true=below, false=above

        public override bool IsTriggered(Map map)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            if (def == null) return false;

            int count = map.resourceCounter.GetCount(def);
            return triggerBelow ? (count < threshold) : (count > threshold);
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Item DefName (e.g. Silver, Steel, WoodLog):");
            thingDefName = listing.TextEntry(thingDefName);

            string buf = threshold.ToString();
            listing.TextFieldNumericLabeled("Threshold: ", ref threshold, ref buf, 0, 999999);
            listing.CheckboxLabeled("Fire when BELOW threshold (uncheck = fire when ABOVE)", ref triggerBelow);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref thingDefName, "thingDefName", "Silver");
            Scribe_Values.Look(ref threshold,    "threshold",    500);
            Scribe_Values.Look(ref triggerBelow, "triggerBelow", true);
        }
    }
}
