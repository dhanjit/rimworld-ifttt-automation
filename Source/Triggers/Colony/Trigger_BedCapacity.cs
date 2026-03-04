using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when there are more colonists than owned non-medical beds in the colony.
    /// </summary>
    public class Trigger_BedCapacity : AutomationTrigger
    {
        public override string Label       => "Bed capacity exceeded";
        public override string Description => "Fires when there are more colonists than owned beds.";

        public override bool HasConfig => false;

        public override bool IsTriggered(Map map)
        {
            int colonists = map.mapPawns.FreeColonistsSpawned.Count();
            int beds = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                .OfType<Building_Bed>()
                .Count(b => b.Faction == Faction.OfPlayer && !b.Medical);
            // Also count non-haulable beds (most beds are buildings, not haulables)
            beds += map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>()
                .Count(b => !b.Medical);
            // Divide by 2 since we may double-count, but safer is to use listerBuildings directly
            beds = map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>()
                .Count(b => b.Faction == Faction.OfPlayer && !b.Medical);
            return colonists > beds;
        }
    }
}
