using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when a colonist has dangerous blood loss (above a configured severity).
    /// Blood loss severity: 0.2=minor, 0.4=moderate, 0.6=severe, 0.8=extreme.
    /// </summary>
    public class Trigger_PawnBloodLoss : AutomationTrigger
    {
        public override string Label       => "Pawn has blood loss";
        public override string Description => "Fires when a colonist has dangerous blood loss.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 90f; // Label + Label + Slider

        /// <summary>0–1. 0.4 = moderate, 0.6 = severe.</summary>
        public float minSeverity = 0.4f;

        public override bool IsTriggered(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned
                .Any(p => p.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.BloodLoss) is Hediff hl
                       && hl.Severity >= minSeverity);
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label($"Minimum blood loss severity (0–1): {minSeverity:F2}");
            listing.Label("0.2=minor  0.4=moderate  0.6=severe  0.8=extreme");
            minSeverity = listing.Slider(minSeverity, 0.1f, 1f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minSeverity, "minSeverity", 0.4f);
        }
    }
}
