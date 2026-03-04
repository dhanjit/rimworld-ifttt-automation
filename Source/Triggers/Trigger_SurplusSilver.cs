using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the colony's silver stockpile exceeds a configurable threshold.
    /// </summary>
    public class Trigger_SurplusSilver : AutomationTrigger
    {
        public override string Label       => "Has surplus silver";
        public override string Description => "Fires when the colony holds more than the configured silver threshold.";

        public override bool HasConfig => true;

        // ── Config ────────────────────────────────────────────────────────────
        public int threshold = 1000;

        // ── Logic ─────────────────────────────────────────────────────────────
        public override bool IsTriggered(Map map)
        {
            int silver = map.resourceCounter.GetCount(ThingDefOf.Silver);
            return silver > threshold;
        }

        // ── Config UI ─────────────────────────────────────────────────────────
        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = threshold.ToString();
            listing.TextFieldNumericLabeled("Silver threshold: ", ref threshold, ref buf, 0, 999999);
        }

        // ── Save/load ─────────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref threshold, "threshold", 1000);
        }
    }
}
