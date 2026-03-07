using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when one or more colonists have untended injuries.
    ///
    /// Configurable:
    ///  - Minimum number of injured colonists required.
    ///  - Minimum bleed rate: 0 = any untended injury (including non-bleeding),
    ///                         > 0 = only injuries with BleedRate ≥ that value.
    ///  - Optional filter by hediff type (specific injury type).
    /// </summary>
    public class Trigger_PawnHasInjury : AutomationTrigger
    {
        public override string Label => "Pawn has untended injury";

        public override string Description
        {
            get
            {
                string rate = minBleedRate > 0f ? $", bleed rate ≥ {minBleedRate:F2}" : "";
                string inj  = injuryDefName.NullOrEmpty() ? "any injury" : $"'{injuryDefName}' injury";
                return $"≥{minInjuredCount} colonist(s) with untended {inj}{rate}";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 85f;

        // ── Config ────────────────────────────────────────────────────────────
        public int    minInjuredCount = 1;
        /// <summary>
        /// Minimum bleed rate (HP/day) required. 0 = trigger on any untended injury,
        /// including non-bleeding ones (scratches, burns, etc.).
        /// </summary>
        public float  minBleedRate    = 0f;
        /// <summary>
        /// Optional hediff def name to filter by specific injury type.
        /// Empty string = all injury types.
        /// </summary>
        public string injuryDefName   = "";

        // ── Evaluation ───────────────────────────────────────────────────────

        public override bool IsTriggered(Map map)
        {
            int count = map.mapPawns.FreeColonistsSpawned
                .Count(p => p.health?.hediffSet != null && HasQualifyingInjury(p));
            return count >= minInjuredCount;
        }

        private bool HasQualifyingInjury(Pawn p)
        {
            foreach (var h in p.health.hediffSet.hediffs)
            {
                if (!(h is Hediff_Injury inj)) continue;
                if (inj.IsTended()) continue;

                // Bleed rate filter: if minBleedRate == 0, accept any injury (even non-bleeding);
                // if > 0, require at least that bleed rate.
                if (minBleedRate > 0f && inj.BleedRate < minBleedRate) continue;

                // Optional hediff type filter
                if (!injuryDefName.NullOrEmpty() && h.def.defName != injuryDefName) continue;

                return true;
            }
            return false;
        }

        // ── DrawConfig ───────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            string countBuf = minInjuredCount.ToString();
            listing.TextFieldNumericLabeled(
                "Min injured colonists: ", ref minInjuredCount, ref countBuf, 1, 100);

            string bleedBuf = minBleedRate.ToString("F2");
            listing.TextFieldNumericLabeled(
                "Min bleed rate (0 = any injury): ", ref minBleedRate, ref bleedBuf, 0f, 10f);

            listing.Label("Injury type filter (blank = any):");
            injuryDefName = listing.TextEntry(injuryDefName ?? "");
        }

        // ── ExposeData ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minInjuredCount, "minInjuredCount", 1);
            Scribe_Values.Look(ref minBleedRate,    "minBleedRate",    0f);
            Scribe_Values.Look(ref injuryDefName,   "injuryDefName",   "");
        }
    }
}
