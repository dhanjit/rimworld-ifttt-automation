using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when a colonist has a dangerous level of toxic buildup.
    /// Uses the standard ToxicBuildup hediff (HediffDefOf.ToxicBuildup).
    /// Severity: 0=none, 0.5=moderate, 1.0=lethal.
    /// </summary>
    public class Trigger_PawnToxicityHigh : AutomationTrigger
    {
        public override string Label       => "Pawn toxicity high";
        public override string Description => "Fires when a colonist has dangerous toxic buildup.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 90f; // Label + Label + Slider

        public float minSeverity = 0.5f;

        public override bool IsTriggered(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned
                .Any(p =>
                {
                    if (p.health?.hediffSet == null) return false;
                    Hediff tox = p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);
                    return tox != null && tox.Severity >= minSeverity;
                });
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label($"Min toxicity severity (0–1): {minSeverity:F2}");
            listing.Label("0.2=minor  0.5=moderate  0.8=severe  1.0=lethal");
            minSeverity = listing.Slider(minSeverity, 0.1f, 1f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minSeverity, "minSeverity", 0.5f);
        }
    }
}
