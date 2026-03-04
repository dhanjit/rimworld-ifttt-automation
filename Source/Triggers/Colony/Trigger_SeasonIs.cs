using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the current in-game season matches the configured season.
    /// </summary>
    public class Trigger_SeasonIs : AutomationTrigger
    {
        public override string Label       => "Season is";
        public override string Description => "Fires during a specific season.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 165f; // Label + 4 ButtonTexts

        public Season targetSeason = Season.Spring;

        public override bool IsTriggered(Map map)
        {
            return GenLocalDate.Season(map) == targetSeason;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label($"Target season: {targetSeason}");
            if (listing.ButtonText("Spring"))  targetSeason = Season.Spring;
            if (listing.ButtonText("Summer"))  targetSeason = Season.Summer;
            if (listing.ButtonText("Fall"))    targetSeason = Season.Fall;
            if (listing.ButtonText("Winter"))  targetSeason = Season.Winter;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref targetSeason, "targetSeason", Season.Spring);
        }
    }
}
