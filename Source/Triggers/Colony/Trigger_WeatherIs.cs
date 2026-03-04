using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the current weather matches a configurable type.
    /// Uses the weather's defName for flexibility (works with modded weathers).
    /// Common: "Clear", "Rain", "SnowGentle", "SnowHard", "FoggyRain", "DryThunderstorm"
    /// </summary>
    public class Trigger_WeatherIs : AutomationTrigger
    {
        public override string Label       => "Weather is";
        public override string Description => "Fires during a specific weather condition.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 95f; // Label + Label + TextEntry

        public string weatherDefName = "Rain";

        public override bool IsTriggered(Map map)
        {
            return map.weatherManager.curWeather?.defName == weatherDefName;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label($"Weather defName: {weatherDefName}");
            listing.Label("(Examples: Clear, Rain, SnowGentle, SnowHard, FoggyRain)");
            weatherDefName = listing.TextEntry(weatherDefName);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref weatherDefName, "weatherDefName", "Rain");
        }
    }
}
