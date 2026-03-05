using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Pauses the game. Useful for emergency triggers like raids or medical crises.
    /// </summary>
    public class Action_PauseGame : AutomationAction
    {
        public override string Label       => "Pause game";
        public override string Description => "Pauses the game immediately. Good for emergency alerts.";

        public override bool HasConfig => true;

        public string alertMessage = "Automation: Game paused!";

        public override bool Execute(Map map)
        {
            Find.TickManager.Pause();

            if (!alertMessage.NullOrEmpty())
                Messages.Message(alertMessage, MessageTypeDefOf.CautionInput, historical: false);

            return true;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Alert message (shown when paused):");
            alertMessage = listing.TextEntry(alertMessage);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref alertMessage, "alertMessage", "Automation: Game paused!");
        }
    }
}
