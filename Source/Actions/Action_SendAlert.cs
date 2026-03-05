using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Sends a configurable message to the player. Supports different message types
    /// and optional camera jump to the colony center.
    /// </summary>
    public class Action_SendAlert : AutomationAction
    {
        public override string Label       => "Send alert message";
        public override string Description => "Displays a configurable alert message to the player.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 255f; // Label + TextEntry + Label + 4 ButtonTexts + Checkbox

        public string  message     = "Automation alert!";
        public int     messageType = 0; // 0=neutral, 1=positive, 2=caution, 3=negative
        public bool    jumpCamera  = false;

        private static readonly string[] TypeLabels =
            { "Neutral", "Positive (green)", "Caution (yellow)", "Negative (red)" };

        public override bool Execute(Map map)
        {
            MessageTypeDef typeDef = messageType switch
            {
                1 => MessageTypeDefOf.PositiveEvent,
                2 => MessageTypeDefOf.CautionInput,
                3 => MessageTypeDefOf.NegativeEvent,
                _ => MessageTypeDefOf.NeutralEvent,
            };

            Messages.Message($"[IFTTT] {message}", typeDef, historical: false);

            if (jumpCamera)
            {
                IntVec3 center = map.Center;
                CameraJumper.TryJump(center, map);
            }

            return true;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Message text:");
            message = listing.TextEntry(message);

            listing.Label($"Message type: {TypeLabels[messageType]}");
            if (listing.ButtonText("Neutral"))  messageType = 0;
            if (listing.ButtonText("Positive")) messageType = 1;
            if (listing.ButtonText("Caution"))  messageType = 2;
            if (listing.ButtonText("Negative")) messageType = 3;

            listing.CheckboxLabeled("Jump camera to colony center", ref jumpCamera);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref message,     "message",     "Automation alert!");
            Scribe_Values.Look(ref messageType, "messageType", 0);
            Scribe_Values.Look(ref jumpCamera,  "jumpCamera",  false);
        }
    }
}
