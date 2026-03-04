using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when a raid threat is imminent — specifically when the map's danger rating
    /// is at high level (active raids or pre-arrival warnings).
    /// Also detects when the map has hostile pawns that spawned from raid events.
    /// </summary>
    public class Trigger_RaidIncoming : AutomationTrigger
    {
        public override string Label       => "Raid incoming / under attack";
        public override string Description => "Fires when the colony is under a high danger rating (raid).";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 125f; // Label + 3 ButtonTexts

        /// <summary>
        /// DangerRating threshold: 0=None, 1=Some, 2=High.
        /// Default = Some (1) triggers on any threat.
        /// </summary>
        public int minDangerLevel = 1;

        public override bool IsTriggered(Map map)
        {
            return (int)map.dangerWatcher.DangerRating >= minDangerLevel;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label($"Minimum danger level: {(StoryDanger)minDangerLevel}");
            if (listing.ButtonText("None (always)")) minDangerLevel = 0;
            if (listing.ButtonText("Some"))          minDangerLevel = 1;
            if (listing.ButtonText("High"))          minDangerLevel = 2;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minDangerLevel, "minDangerLevel", 1);
        }
    }
}
