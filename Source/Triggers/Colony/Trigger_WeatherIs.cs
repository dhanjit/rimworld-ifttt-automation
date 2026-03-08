using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the current weather matches a configurable type.
    /// Uses dropdown populated from DefDatabase&lt;WeatherDef&gt; (works with modded weathers).
    /// </summary>
    public class Trigger_WeatherIs : AutomationTrigger
    {
        public override string Label       => "Weather is";
        public override string Description
        {
            get
            {
                WeatherDef d = DefDatabase<WeatherDef>.GetNamedSilentFail(weatherDefName);
                string lbl = d?.label?.CapitalizeFirst() ?? weatherDefName;
                return $"Weather is: {lbl}";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 62f;

        public string weatherDefName = "Rain";

        public override bool IsTriggered(Map map)
        {
            return map.weatherManager.curWeather?.defName == weatherDefName;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Weather type:");
            WeatherDef cur = DefDatabase<WeatherDef>.GetNamedSilentFail(weatherDefName);
            string btn = cur != null
                ? cur.label.CapitalizeFirst()
                : (weatherDefName.NullOrEmpty() ? "(select)" : weatherDefName);
            if (Widgets.ButtonText(listing.GetRect(28f), btn))
            {
                var opts = new List<FloatMenuOption>();
                foreach (WeatherDef d in DefDatabase<WeatherDef>.AllDefsListForReading
                    .Where(d => !d.label.NullOrEmpty())
                    .OrderBy(d => d.label))
                {
                    WeatherDef cap = d;
                    opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(),
                        () => weatherDefName = cap.defName));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref weatherDefName, "weatherDefName", "Rain");
        }
    }
}
