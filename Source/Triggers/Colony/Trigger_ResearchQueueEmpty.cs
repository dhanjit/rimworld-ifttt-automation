using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when no research project is currently being worked on.
    /// Useful to prompt the player to queue up a new project.
    /// </summary>
    public class Trigger_ResearchQueueEmpty : AutomationTrigger
    {
        public override string Label       => "Research queue empty";
        public override string Description => "Fires when no research project is active.";

        public override bool HasConfig => false;

        public override bool IsTriggered(Map map)
        {
            return Find.ResearchManager.GetProject() == null;
        }
    }
}
