using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>Which map-level property to query.</summary>
    public enum MapPropertyType
    {
        Weather,       // current weather == selected WeatherDef
        Temperature,   // outdoor temp ≥/≤ threshold
        Season,        // current season == selected
        TimeOfDay,     // hour of day ≥/≤ threshold
        FireCount,     // number of fires on map ≥/≤ threshold
        ColonyWealth,  // total colony wealth ≥/≤ threshold
    }

    /// <summary>
    /// Universal map state trigger — can query any map-level property:
    /// weather, temperature, season, time of day, fire count, colony wealth.
    ///
    /// All values are polled every tick interval from the current map.
    /// Works with vanilla and modded weather defs.
    /// </summary>
    public class Trigger_MapState : AutomationTrigger
    {
        public override string Label => "Map state";
        public override string Description
        {
            get
            {
                switch (propertyType)
                {
                    case MapPropertyType.Weather:
                        return $"Weather is {DefLabel<WeatherDef>(defName)}";
                    case MapPropertyType.Temperature:
                        return $"Temperature {CompSym} {threshold:F0}°C";
                    case MapPropertyType.Season:
                        return $"Season is {defName}";
                    case MapPropertyType.TimeOfDay:
                        return $"Hour {CompSym} {(int)threshold}";
                    case MapPropertyType.FireCount:
                        return $"Fires on map {CompSym} {(int)threshold}";
                    case MapPropertyType.ColonyWealth:
                        return $"Colony wealth {CompSym} {threshold:N0}";
                    default:
                        return "Map state";
                }
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight
        {
            get
            {
                switch (propertyType)
                {
                    case MapPropertyType.Weather:
                    case MapPropertyType.Season:
                        return 100f; // property dropdown + def dropdown
                    default:
                        return 120f; // property dropdown + comparator + threshold
                }
            }
        }

        // ── Fields ────────────────────────────────────────────────────────────

        public MapPropertyType propertyType = MapPropertyType.Weather;
        public string          defName      = "";         // weather defName / season name
        public float           threshold    = 0f;         // numeric threshold
        public CountComparator comparator   = CountComparator.AtLeast;

        // ── Helpers ───────────────────────────────────────────────────────────

        private string CompSym => comparator == CountComparator.AtLeast ? "≥"
                                : comparator == CountComparator.AtMost  ? "≤" : "=";

        private static string DefLabel<T>(string dn) where T : Def
        {
            if (dn.NullOrEmpty()) return "???";
            T d = DefDatabase<T>.GetNamedSilentFail(dn);
            return d != null ? d.label.CapitalizeFirst() : dn;
        }

        private bool CompareNum(float value)
        {
            switch (comparator)
            {
                case CountComparator.AtLeast: return value >= threshold;
                case CountComparator.AtMost:  return value <= threshold;
                default:                      return Math.Abs(value - threshold) < 0.01f;
            }
        }

        // ── Trigger logic ─────────────────────────────────────────────────────

        public override bool IsTriggered(Map map)
        {
            switch (propertyType)
            {
                case MapPropertyType.Weather:
                    return map.weatherManager.curWeather?.defName == defName;

                case MapPropertyType.Temperature:
                    return CompareNum(map.mapTemperature.OutdoorTemp);

                case MapPropertyType.Season:
                    return GenLocalDate.Season(map).ToString() == defName;

                case MapPropertyType.TimeOfDay:
                    return CompareNum(GenLocalDate.HourOfDay(map));

                case MapPropertyType.FireCount:
                    return CompareNum(map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count);

                case MapPropertyType.ColonyWealth:
                    return CompareNum(map.wealthWatcher.WealthTotal);

                default:
                    return false;
            }
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            // Property type selector
            listing.Label("Map property:");
            if (Widgets.ButtonText(listing.GetRect(28f), PropertyLabel(propertyType)))
                Find.WindowStack.Add(new FloatMenu(BuildPropertyMenu()));

            listing.Gap(2f);

            // Property-specific controls
            switch (propertyType)
            {
                case MapPropertyType.Weather:
                    DrawWeatherDropdown(listing);
                    break;
                case MapPropertyType.Season:
                    DrawSeasonDropdown(listing);
                    break;
                case MapPropertyType.Temperature:
                    DrawComparator(listing);
                    DrawThresholdSlider(listing, -80f, 80f, $"Temperature: {threshold:F0}°C");
                    break;
                case MapPropertyType.TimeOfDay:
                    DrawComparator(listing);
                    DrawThresholdSlider(listing, 0f, 23f, $"Hour: {(int)threshold}");
                    break;
                case MapPropertyType.FireCount:
                    DrawComparator(listing);
                    string fireBuf = ((int)threshold).ToString();
                    int fireVal = (int)threshold;
                    listing.TextFieldNumericLabeled("Fire count:", ref fireVal, ref fireBuf, 0, 999);
                    threshold = fireVal;
                    break;
                case MapPropertyType.ColonyWealth:
                    DrawComparator(listing);
                    string wBuf = ((int)threshold).ToString();
                    int wVal = (int)threshold;
                    listing.TextFieldNumericLabeled("Wealth:", ref wVal, ref wBuf, 0, 99_999_999);
                    threshold = wVal;
                    break;
            }
        }

        private void DrawWeatherDropdown(Listing_Standard listing)
        {
            listing.Label("Weather:");
            WeatherDef cur = DefDatabase<WeatherDef>.GetNamedSilentFail(defName);
            string btn = cur != null ? cur.label.CapitalizeFirst()
                : (defName.NullOrEmpty() ? "(select weather)" : $"(unknown: {defName})");
            if (Widgets.ButtonText(listing.GetRect(24f), btn))
            {
                var opts = new List<FloatMenuOption>();
                foreach (WeatherDef d in DefDatabase<WeatherDef>.AllDefsListForReading
                    .Where(d => !d.label.NullOrEmpty())
                    .OrderBy(d => d.label))
                {
                    string dn = d.defName;
                    opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(), () => defName = dn));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        private void DrawSeasonDropdown(Listing_Standard listing)
        {
            listing.Label("Season:");
            string btn = defName.NullOrEmpty() ? "(select season)" : defName;
            if (Widgets.ButtonText(listing.GetRect(24f), btn))
            {
                var opts = new List<FloatMenuOption>();
                foreach (Season s in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
                {
                    string name = s.ToString();
                    opts.Add(new FloatMenuOption(name, () => defName = name));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        private void DrawComparator(Listing_Standard listing)
        {
            Rect row = listing.GetRect(24f);
            float w   = row.width / 3f;
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x,         row.y, w, 24f), "≥ At least", comparator == CountComparator.AtLeast, () => comparator = CountComparator.AtLeast);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w,     row.y, w, 24f), "≤ At most",  comparator == CountComparator.AtMost,  () => comparator = CountComparator.AtMost);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w * 2, row.y, w, 24f), "= Exactly",  comparator == CountComparator.Exactly, () => comparator = CountComparator.Exactly);
        }

        private void DrawThresholdSlider(Listing_Standard listing, float min, float max, string label)
        {
            threshold = Widgets.HorizontalSlider(listing.GetRect(28f), threshold, min, max, false, label);
        }

        // ── Menus ─────────────────────────────────────────────────────────────

        private static string PropertyLabel(MapPropertyType pt)
        {
            switch (pt)
            {
                case MapPropertyType.Weather:      return "Weather";
                case MapPropertyType.Temperature:  return "Temperature";
                case MapPropertyType.Season:       return "Season";
                case MapPropertyType.TimeOfDay:    return "Time of day (hour)";
                case MapPropertyType.FireCount:    return "Fire count";
                case MapPropertyType.ColonyWealth: return "Colony wealth";
                default: return pt.ToString();
            }
        }

        private List<FloatMenuOption> BuildPropertyMenu()
        {
            var opts = new List<FloatMenuOption>();
            foreach (MapPropertyType pt in Enum.GetValues(typeof(MapPropertyType)))
            {
                MapPropertyType cap = pt;
                opts.Add(new FloatMenuOption(PropertyLabel(cap), () =>
                {
                    propertyType = cap;
                    defName = "";
                    threshold = 0f;
                }));
            }
            return opts;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref propertyType, "propertyType", MapPropertyType.Weather);
            Scribe_Values.Look(ref defName,      "defName",      "");
            Scribe_Values.Look(ref threshold,    "threshold",    0f);
            Scribe_Values.Look(ref comparator,   "comparator",   CountComparator.AtLeast);
        }
    }
}
