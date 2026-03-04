using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the in-game hour is within a configured window (e.g., 6–10 = morning).
    /// </summary>
    public class Trigger_TimeOfDay : AutomationTrigger
    {
        public override string Label       => "Time of day";
        public override string Description => "Fires when the in-game hour is within the configured range.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 110f; // Label + Slider + Label + Slider

        /// <summary>Start hour (0–23, inclusive).</summary>
        public int startHour = 6;
        /// <summary>End hour (0–23, inclusive). If end < start, wraps midnight.</summary>
        public int endHour   = 10;

        public override bool IsTriggered(Map map)
        {
            int hour = GenLocalDate.HourOfDay(map);
            if (startHour <= endHour)
                return hour >= startHour && hour <= endHour;
            else
                // Wraps midnight: e.g., 22–4
                return hour >= startHour || hour <= endHour;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            float sH = startHour, eH = endHour;
            listing.Label($"Start hour: {startHour:00}:00");
            sH = listing.Slider(sH, 0f, 23f);
            startHour = (int)sH;

            listing.Label($"End hour: {endHour:00}:00");
            eH = listing.Slider(eH, 0f, 23f);
            endHour = (int)eH;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startHour, "startHour", 6);
            Scribe_Values.Look(ref endHour,   "endHour",   10);
        }
    }
}
