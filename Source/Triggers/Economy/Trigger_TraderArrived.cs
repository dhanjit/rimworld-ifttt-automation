using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when a caravan trader from any faction arrives at the colony map edge.
    /// These are ground traders, not orbital. They appear as visiting caravans.
    /// </summary>
    public class Trigger_TraderArrived : AutomationTrigger
    {
        public override string Label       => "Caravan trader arrived";
        public override string Description => "Fires when a friendly caravan trader arrives at the colony.";

        public override bool HasConfig => false;

        public override bool IsTriggered(Map map)
        {
            // A trader caravan is a non-hostile, non-player pawn group that has a trader
            return map.mapPawns.AllPawnsSpawned
                .Any(p => p.Faction != null
                       && !p.Faction.IsPlayer
                       && !p.Faction.HostileTo(Faction.OfPlayer)
                       && p.TraderKind != null);
        }
    }
}
