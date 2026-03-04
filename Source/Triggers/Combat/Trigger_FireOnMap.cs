using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when there are burning fires on the map that are near colony structures.
    /// Uses the game's built-in fire watcher for efficiency.
    /// </summary>
    public class Trigger_FireOnMap : AutomationTrigger
    {
        public override string Label       => "Fire on map";
        public override string Description => "Fires when burning fires are detected on the colony map.";

        public override bool HasConfig => true;

        public bool onlyNearBuildings = true;

        public override bool IsTriggered(Map map)
        {
            if (!onlyNearBuildings)
                return map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count > 0;

            // Check if any fire is inside the home zone / near player buildings
            foreach (Thing fire in map.listerThings.ThingsOfDef(ThingDefOf.Fire))
            {
                if (map.areaManager.Home[fire.Position])
                    return true;
            }
            return false;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.CheckboxLabeled("Only fires inside home zone", ref onlyNearBuildings);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref onlyNearBuildings, "onlyNearBuildings", true);
        }
    }
}
