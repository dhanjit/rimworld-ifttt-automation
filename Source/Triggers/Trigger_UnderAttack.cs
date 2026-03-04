using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when there are hostile pawns actively threatening the colony
    /// (i.e., enemies who can see or are attacking colonists/buildings).
    /// </summary>
    public class Trigger_UnderAttack : AutomationTrigger
    {
        public override string Label       => "Colony is under attack";
        public override string Description => "Fires when hostile pawns are actively threatening the colony.";

        public override bool IsTriggered(Map map)
        {
            // DangerRating is the simplest reliable signal: None / Some / Extreme
            return map.dangerWatcher.DangerRating != StoryDanger.None;
        }
    }
}
