using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when any (or all) colonist(s) have a mood below a configurable threshold.
    /// </summary>
    public class Trigger_PawnMoodLow : AutomationTrigger
    {
        public override string Label       => "Pawn mood low";
        public override string Description => "Fires when colonist(s) mood drops below the threshold.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 90f; // Label + Slider + Checkbox

        /// <summary>0–1 range (0.2 = very sad, 0.5 = neutral).</summary>
        public float moodThreshold = 0.25f;

        /// <summary>If true, ALL colonists must be sad. If false, ANY one suffices.</summary>
        public bool requireAll = false;

        public override bool IsTriggered(Map map)
        {
            var colonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.needs?.mood != null)
                .ToList();
            if (colonists.Count == 0) return false;

            if (requireAll)
                return colonists.All(p => p.needs.mood.CurLevel < moodThreshold);
            else
                return colonists.Any(p => p.needs.mood.CurLevel < moodThreshold);
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = moodThreshold.ToString("F2");
            listing.Label($"Mood threshold (0–1): {moodThreshold:F2}");
            moodThreshold = listing.Slider(moodThreshold, 0f, 1f);
            listing.CheckboxLabeled("Require ALL colonists to be sad (not just one)", ref requireAll);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref moodThreshold, "moodThreshold", 0.25f);
            Scribe_Values.Look(ref requireAll,    "requireAll",    false);
        }
    }
}
