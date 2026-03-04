using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when a neutral faction visit group is on the map.
    /// Neutral = not ally, not enemy, not player.
    /// Good for triggering diplomacy actions.
    /// </summary>
    public class Trigger_NeutralFactionNearby : AutomationTrigger
    {
        public override string Label       => "Neutral faction visiting";
        public override string Description => "Fires when visitors from a neutral faction are on the map.";

        public override bool HasConfig => false;

        public override bool IsTriggered(Map map)
        {
            return map.mapPawns.AllPawnsSpawned
                .Any(p => p.Faction != null
                       && !p.Faction.IsPlayer
                       && !p.Faction.HostileTo(Faction.OfPlayer)
                       && p.Faction.PlayerRelationKind == FactionRelationKind.Neutral
                       && p.RaceProps.Humanlike
                       && !p.IsPrisoner);
        }
    }
}
