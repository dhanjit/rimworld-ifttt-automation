using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when one or more colonists are currently having a mental break.
    /// </summary>
    public class Trigger_MentalBreak : AutomationTrigger
    {
        public override string Label       => "Colonist mental break";
        public override string Description => "Fires when a colonist is in a mental break state.";

        public override bool HasConfig => false;

        public override bool IsTriggered(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned
                .Any(p => p.InMentalState);
        }
    }
}
