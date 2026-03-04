using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when any allied faction's goodwill drops below the configured threshold.
    /// Useful for triggering automatic gift-giving to maintain alliances.
    /// </summary>
    public class Trigger_FactionGoodwillBelow : AutomationTrigger
    {
        public override string Label       => "Faction goodwill low";
        public override string Description => "Fires when an allied faction's goodwill is below the threshold.";

        public override bool HasConfig => true;

        public int goodwillThreshold = 40;
        /// <summary>If true, checks ALL allies. If false, checks ANY one ally.</summary>
        public bool checkAll = false;

        public override bool IsTriggered(Map map)
        {
            var allies = Find.FactionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer && f.PlayerRelationKind == FactionRelationKind.Ally)
                .ToList();

            if (allies.Count == 0) return false;

            if (checkAll)
                return allies.All(f => f.PlayerGoodwill < goodwillThreshold);
            else
                return allies.Any(f => f.PlayerGoodwill < goodwillThreshold);
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = goodwillThreshold.ToString();
            listing.TextFieldNumericLabeled("Goodwill threshold: ", ref goodwillThreshold, ref buf, -100, 100);
            listing.CheckboxLabeled("Require ALL allies below threshold (unchecked = any one)", ref checkAll);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref goodwillThreshold, "goodwillThreshold", 40);
            Scribe_Values.Look(ref checkAll,          "checkAll",          false);
        }
    }
}
