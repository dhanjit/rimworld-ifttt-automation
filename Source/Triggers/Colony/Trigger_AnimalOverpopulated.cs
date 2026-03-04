using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the colony owns more than a configured number of animals.
    /// Useful for triggering automated slaughter.
    /// </summary>
    public class Trigger_AnimalOverpopulated : AutomationTrigger
    {
        public override string Label       => "Animal overpopulation";
        public override string Description => "Fires when the colony owns more animals than the configured limit.";

        public override bool HasConfig => true;

        public int maxAnimals = 20;

        public override bool IsTriggered(Map map)
        {
            int count = map.mapPawns.AllPawnsSpawned
                .Count(p => p.RaceProps.Animal
                         && p.Faction == Faction.OfPlayer);
            return count > maxAnimals;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = maxAnimals.ToString();
            listing.TextFieldNumericLabeled("Max animals before trigger: ", ref maxAnimals, ref buf, 1, 999);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref maxAnimals, "maxAnimals", 20);
        }
    }
}
