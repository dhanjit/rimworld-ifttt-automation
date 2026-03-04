using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when any tame, player-owned animal is wearing apparel whose
    /// hit-point percentage has fallen below a configurable threshold.
    ///
    /// Requires the Animal Apparel Framework mod at runtime; without it,
    /// animals will have no WornApparel and the trigger will never fire.
    /// </summary>
    public class Trigger_AnimalGearWornOut : AutomationTrigger
    {
        public override string Label       => "Animal gear worn out";
        public override string Description => "Fires when a tame animal's apparel HP% drops below the configured threshold.";

        public override bool HasConfig => true;

        /// <summary>HP fraction 0–1; default 0.55 = 55 %.</summary>
        public float wornThreshold = 0.55f;

        public override bool IsTriggered(Map map)
        {
            foreach (Pawn animal in map.mapPawns.AllPawnsSpawned)
            {
                if (!animal.RaceProps.Animal) continue;
                if (animal.Faction != Faction.OfPlayer) continue;
                if (animal.apparel == null) continue;           // no apparel tracker (vanilla animals)

                foreach (Apparel ap in animal.apparel.WornApparel)
                {
                    float pct = ap.HitPoints / (float)ap.MaxHitPoints;
                    if (pct < wornThreshold)
                        return true;
                }
            }
            return false;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label($"Worn-out threshold: {wornThreshold:P0}  (fires when HP is below this)");
            wornThreshold = listing.Slider(wornThreshold, 0.05f, 0.95f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref wornThreshold, "wornThreshold", 0.55f);
        }
    }
}
