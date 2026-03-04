using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when one or more colonists have untended injuries.
    /// Includes blood loss, wounds, burns, etc.
    /// </summary>
    public class Trigger_PawnHasInjury : AutomationTrigger
    {
        public override string Label       => "Pawn has untended injury";
        public override string Description => "Fires when colonists have untended injuries.";

        public override bool HasConfig => true;

        public int minInjuredCount = 1;
        /// <summary>Only trigger if bleed rate is above this (0 = any injury).</summary>
        public float minBleedRate = 0f;

        public override bool IsTriggered(Map map)
        {
            int count = map.mapPawns.FreeColonistsSpawned
                .Count(p => p.health?.hediffSet != null
                         && p.health.hediffSet.hediffs
                             .Any(h => h is Hediff_Injury inj
                                    && !inj.IsTended()
                                    && inj.Bleeding));
            return count >= minInjuredCount;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = minInjuredCount.ToString();
            listing.TextFieldNumericLabeled("Min injured colonists: ", ref minInjuredCount, ref buf, 1, 100);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minInjuredCount, "minInjuredCount", 1);
            Scribe_Values.Look(ref minBleedRate,    "minBleedRate",    0f);
        }
    }
}
