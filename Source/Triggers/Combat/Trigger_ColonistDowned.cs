using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>Fires when any free colonist on the map is downed.</summary>
    public class Trigger_ColonistDowned : AutomationTrigger
    {
        public override string Label       => "Colonist downed";
        public override string Description => "Fires when at least one colonist is downed (incapacitated).";

        public override bool IsTriggered(Map map)
            => map.mapPawns.FreeColonistsSpawned.Any(p => p.Downed);
    }
}
