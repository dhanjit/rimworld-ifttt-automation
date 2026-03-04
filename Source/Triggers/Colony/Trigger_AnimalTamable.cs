using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when wild tamable animals are present on the map.
    /// Excludes mechanoids and already-owned animals.
    /// </summary>
    public class Trigger_AnimalTamable : AutomationTrigger
    {
        public override string Label       => "Tamable animal present";
        public override string Description => "Fires when wild animals that can be tamed are on the map.";

        public override bool HasConfig => true;

        public int minCount = 1;

        public override bool IsTriggered(Map map)
        {
            int count = map.mapPawns.AllPawnsSpawned
                .Count(p => p.RaceProps.Animal
                         && p.Faction == null
                         && !p.Downed
                         && p.RaceProps.trainability != null
                         && p.RaceProps.trainability != TrainabilityDefOf.None);
            return count >= minCount;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = minCount.ToString();
            listing.TextFieldNumericLabeled("Min tamable animals: ", ref minCount, ref buf, 1, 999);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minCount, "minCount", 1);
        }
    }
}
