using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Generic trigger: fires when any pawn of the specified kind
    /// has or is missing a given HediffDef.
    ///
    /// Optional filters: animal race, map area/zone.
    /// Works with vanilla and modded hediffs — just pick from the dropdown.
    /// </summary>
    public class Trigger_PawnCondition : AutomationTrigger
    {
        public override string Label => "Pawn condition (hediff)";
        public override string Description
        {
            get
            {
                HediffDef hd  = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
                string hLabel = hd != null ? hd.label.CapitalizeFirst() : hediffDefName;
                string kStr   = pawnKind.ToString().ToLower();
                string zone   = zoneLabel.NullOrEmpty() ? "" : $" in '{zoneLabel}'";
                return missing
                    ? $"Any {kStr}{zone} missing: {hLabel}"
                    : $"Any {kStr}{zone} has: {hLabel}";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 195f;

        public string         hediffDefName = "";
        public bool           missing       = true;   // true = pawn lacks hediff; false = pawn has hediff
        public PawnKindFilter pawnKind      = PawnKindFilter.Colonist;
        public string         raceDefName   = "";     // optional race filter (animals)
        public string         zoneLabel     = "";     // optional area filter

        // ── Trigger logic ─────────────────────────────────────────────────────

        public override bool IsTriggered(Map map)
        {
            HediffDef hDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
            if (hDef == null) return false;

            return PawnFilterHelper.GetPawns(map, pawnKind, zoneLabel, raceDefName)
                .Any(p => !p.Dead && p.Spawned && HasHediff(p, hDef) != missing);
            //                                    ^ missing=true means we want !HasHediff
        }

        private static bool HasHediff(Pawn p, HediffDef hDef) =>
            p.health?.hediffSet?.HasHediff(hDef) == true;

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            // Hediff dropdown
            listing.Label("Hediff:");
            PawnFilterHelper.DrawHediffDropdown(hediffDefName, v => hediffDefName = v, listing.GetRect(28f), "(select hediff)");

            // Has / Missing toggle
            Rect condRow = listing.GetRect(24f);
            float hw     = condRow.width / 2f;
            bool isMissing = missing;
            PawnFilterHelper.DrawToggleBtn(new Rect(condRow.x,      condRow.y, hw, 24f), "Missing", isMissing,  () => missing = true);
            PawnFilterHelper.DrawToggleBtn(new Rect(condRow.x + hw, condRow.y, hw, 24f), "Has",     !isMissing, () => missing = false);

            // Pawn kind
            PawnFilterHelper.DrawKindFilter(pawnKind, v => pawnKind = v, listing);

            // Race filter (only shown for Animal / Any)
            if (pawnKind == PawnKindFilter.Animal || pawnKind == PawnKindFilter.Any)
            {
                listing.Label("Race filter (optional):");
                PawnFilterHelper.DrawRaceDropdown(raceDefName, v => raceDefName = v, listing.GetRect(24f));
            }

            // Zone filter
            listing.Label("Zone filter (optional):");
            PawnFilterHelper.DrawZoneDropdown(zoneLabel, v => zoneLabel = v, listing.GetRect(24f));
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hediffDefName, "hediffDefName", "");
            Scribe_Values.Look(ref missing,       "missing",       true);
            Scribe_Values.Look(ref pawnKind,      "pawnKind",      PawnKindFilter.Colonist);
            Scribe_Values.Look(ref raceDefName,   "raceDefName",   "");
            Scribe_Values.Look(ref zoneLabel,     "zoneLabel",     "");
        }
    }
}
