using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when one or more colonists have an active disease or illness.
    /// Diseases are identified as bad, non-injury hediffs that can be tended.
    /// </summary>
    public class Trigger_PawnHasDisease : AutomationTrigger
    {
        public override string Label       => "Pawn has disease";
        public override string Description => "Fires when colonists are suffering from a disease or illness.";

        public override bool HasConfig => true;

        public int minSickCount = 1;

        public override bool IsTriggered(Map map)
        {
            int count = map.mapPawns.FreeColonistsSpawned
                .Count(p => p.health?.hediffSet != null
                         && p.health.hediffSet.hediffs
                             .Any(h => h.def.isBad
                                    && h.def.tendable
                                    && !(h is Hediff_Injury)
                                    && !h.IsTended()));
            return count >= minSickCount;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = minSickCount.ToString();
            listing.TextFieldNumericLabeled("Min sick colonists: ", ref minSickCount, ref buf, 1, 100);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minSickCount, "minSickCount", 1);
        }
    }
}
