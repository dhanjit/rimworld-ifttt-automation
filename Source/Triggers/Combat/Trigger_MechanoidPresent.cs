using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>Fires when hostile mechanoids are present on the map.</summary>
    public class Trigger_MechanoidPresent : AutomationTrigger
    {
        public override string Label       => "Mechanoids present";
        public override string Description => "Fires when hostile mechanoids are on the map.";

        public override bool IsTriggered(Map map)
            => map.mapPawns.AllPawnsSpawned
                .Any(p => p.RaceProps.IsMechanoid && p.HostileTo(Faction.OfPlayer));
    }
}
