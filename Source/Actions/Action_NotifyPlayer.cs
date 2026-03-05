using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Simply shows a configurable message in the game's message log.
    /// Useful for testing triggers or as an alerting mechanism.
    /// </summary>
    public class Action_NotifyPlayer : AutomationAction
    {
        public override string Label       => "Notify player";
        public override string Description => "Shows a configurable message in the message log. " +
                                              "Good for testing triggers.";

        public override bool HasConfig => true;

        // ── Config ────────────────────────────────────────────────────────────
        public string message = "Automation rule fired!";

        // ── Logic ─────────────────────────────────────────────────────────────
        public override bool Execute(Map map)
        {
            Messages.Message(
                $"[IFTTT] {message}",
                MessageTypeDefOf.NeutralEvent,
                historical: false);

            return true;
        }

        // ── Config UI ─────────────────────────────────────────────────────────
        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Message text:");
            message = listing.TextEntry(message);
        }

        // ── Save/load ─────────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref message, "message", "Automation rule fired!");
        }
    }
}
