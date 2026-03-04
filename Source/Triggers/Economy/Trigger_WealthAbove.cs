using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the colony's total wealth (items + buildings + pawns) exceeds a threshold.
    /// Note: wealth is cached by the game; use large check intervals to match.
    /// </summary>
    public class Trigger_WealthAbove : AutomationTrigger
    {
        public override string Label       => "Colony wealth above";
        public override string Description => "Fires when colony total wealth exceeds the configured threshold.";

        public override bool HasConfig => true;

        public float wealthThreshold = 50000f;

        public override bool IsTriggered(Map map)
        {
            return map.wealthWatcher?.WealthTotal > wealthThreshold;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = wealthThreshold.ToString("F0");
            listing.Label($"Wealth threshold: {wealthThreshold:F0} silver");
            wealthThreshold = listing.Slider(wealthThreshold, 0f, 500000f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref wealthThreshold, "wealthThreshold", 50000f);
        }
    }
}
