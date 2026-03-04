using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the colony's raw/packaged food supply drops below a
    /// configurable number of nutrition units.
    /// </summary>
    public class Trigger_FoodLow : AutomationTrigger
    {
        public override string Label       => "Food supply low";
        public override string Description => "Fires when total colony food nutrition is below the threshold.";

        public override bool HasConfig => true;

        /// <summary>Minimum nutrition units before firing. 1 meal ≈ 0.9 nutrition.</summary>
        public int nutritionThreshold = 50;

        public override bool IsTriggered(Map map)
        {
            // resourceCounter tracks nutrition for raw food. For a rough total,
            // we count all things in the edible food category.
            float total = 0f;
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
            {
                if (t.def.IsNutritionGivingIngestible && !t.IsForbidden(Faction.OfPlayer))
                    total += t.GetStatValue(StatDefOf.Nutrition) * t.stackCount;
            }
            return total < nutritionThreshold;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = nutritionThreshold.ToString();
            listing.TextFieldNumericLabeled("Min nutrition: ", ref nutritionThreshold, ref buf, 0, 99999);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nutritionThreshold, "nutritionThreshold", 50);
        }
    }
}
