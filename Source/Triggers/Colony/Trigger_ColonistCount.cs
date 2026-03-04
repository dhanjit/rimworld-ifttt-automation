using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the number of free colonists is above or below a threshold.
    /// Useful for recruitment triggers or population cap enforcement.
    /// </summary>
    public class Trigger_ColonistCount : AutomationTrigger
    {
        public override string Label       => "Colonist count";
        public override string Description => "Fires when the colony's free colonist count crosses the configured threshold.";

        public override bool HasConfig => true;

        public int threshold = 5;
        /// <summary>True = fire when count is below threshold. False = fire when above.</summary>
        public bool below = true;

        public override bool IsTriggered(Map map)
        {
            int count = map.mapPawns.FreeColonistsSpawned.Count();
            return below ? count < threshold : count > threshold;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = threshold.ToString();
            listing.TextFieldNumericLabeled("Colonist count threshold: ", ref threshold, ref buf, 1, 999);
            listing.CheckboxLabeled("Trigger when BELOW threshold (unchecked = above)", ref below);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref threshold, "threshold", 5);
            Scribe_Values.Look(ref below,     "below",     true);
        }
    }
}
