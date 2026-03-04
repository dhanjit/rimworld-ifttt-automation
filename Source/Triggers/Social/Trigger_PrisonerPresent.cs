using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the colony has at least one prisoner.
    /// Useful for triggering recruitment attempts or prisoner release rules.
    /// </summary>
    public class Trigger_PrisonerPresent : AutomationTrigger
    {
        public override string Label       => "Prisoner present";
        public override string Description => "Fires when the colony holds at least one prisoner.";

        public override bool HasConfig => true;

        public int minCount = 1;

        public override bool IsTriggered(Map map)
        {
            int count = map.mapPawns.PrisonersOfColonySpawned.Count();
            return count >= minCount;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = minCount.ToString();
            listing.TextFieldNumericLabeled("Min prisoner count: ", ref minCount, ref buf, 1, 100);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minCount, "minCount", 1);
        }
    }
}
