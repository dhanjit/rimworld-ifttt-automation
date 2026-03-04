using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Drops a configurable resource from existing stockpiles near the colony center.
    /// Simulates "redistribute resources" by spawning a pile near the center for haulers.
    /// NOTE: This doesn't create items from nothing — it's a notification/marker action.
    /// The real version orders a supply-drop incident if the player has it available.
    /// </summary>
    public class Action_DropSupplyDrop : AutomationAction
    {
        public override string Label       => "Trigger supply drop (orbital)";
        public override string Description =>
            "If a supply drop orbital bombardment is available, triggers it at the colony center.";

        public override bool HasConfig => false;

        public override void Execute(Map map)
        {
            // This fires the "supply drop" incident if available
            IncidentDef supplyDrop = DefDatabase<IncidentDef>.GetNamedSilentFail("OrbitalSupplyDrop");
            if (supplyDrop == null)
            {
                Messages.Message(
                    "[IFTTT] DropSupplyDrop: OrbitalSupplyDrop incident not found (may need DLC).",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(supplyDrop.category, map);
            if (supplyDrop.Worker.CanFireNow(parms))
            {
                supplyDrop.Worker.TryExecute(parms);
                Messages.Message(
                    "[IFTTT] Supply drop ordered.",
                    MessageTypeDefOf.PositiveEvent, historical: false);
            }
            else
            {
                Messages.Message(
                    "[IFTTT] DropSupplyDrop: Cannot fire supply drop right now.",
                    MessageTypeDefOf.RejectInput, historical: false);
            }
        }
    }
}
