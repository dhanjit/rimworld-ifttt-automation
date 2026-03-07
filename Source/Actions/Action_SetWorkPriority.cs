using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Sets the work priority for a specific work type on a group of colonists.
    ///
    /// Priority values:
    ///   0 = disabled (pawn will not do this work)
    ///   1 = highest priority
    ///   4 = lowest priority (default for most work types)
    ///
    /// Common automation examples:
    ///   • Raid detected → set Shooting to priority 1 for all colonists
    ///   • Colonist downed → set Doctor to priority 1
    ///   • Fire on map → set Firefighting to priority 1
    ///   • Night falls → disable Mining work (priority 0)
    ///   • Peace restored → reset priorities to normal
    ///
    /// Only applies to pawns whose race/backstory allows the work type.
    /// Pawns biologically incapable of a work type are skipped silently.
    /// </summary>
    public class Action_SetWorkPriority : AutomationAction
    {
        // ── Fields ────────────────────────────────────────────────────────────

        public PawnKindFilter pawnKind        = PawnKindFilter.Colonist;
        public string         workTypeDefName = "";
        /// <summary>0 = disabled, 1 = highest, 4 = lowest.</summary>
        public int            priority        = 1;

        // ── AutomationAction overrides ────────────────────────────────────────

        public override string Label => "Set work priority";

        public override string Description
        {
            get
            {
                WorkTypeDef d = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeDefName);
                string work   = d != null
                    ? d.labelShort.CapitalizeFirst()
                    : (workTypeDefName.NullOrEmpty() ? "?" : workTypeDefName);
                string prio   = priority == 0 ? "disabled" : priority.ToString();
                string kind   = pawnKind.ToString().ToLower();
                return $"Set {kind} '{work}' priority \u2192 {prio}";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 150f;  // kind + work type + priority buttons

        // ── Execute ───────────────────────────────────────────────────────────

        public override bool Execute(Map map)
        {
            WorkTypeDef workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeDefName);
            if (workType == null) return false;

            // Only colonists have work settings — filter others out gracefully
            var pawns = PawnFilterHelper.GetPawns(map, pawnKind, "", "")
                .Where(p => !p.Dead && p.Spawned
                         && p.workSettings != null
                         && !p.WorkTypeIsDisabled(workType))
                .ToList();

            if (pawns.Count == 0) return false;

            foreach (Pawn pawn in pawns)
                pawn.workSettings.SetPriority(workType, priority);

            return true;
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            // Pawn kind (colonists are the primary target, but prisoners also have work settings)
            PawnFilterHelper.DrawKindFilter(pawnKind, v => pawnKind = v, listing);

            // Work type picker
            listing.Label("Work type:");
            WorkTypeDef cur = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeDefName);
            string btn = cur != null
                ? cur.labelShort.CapitalizeFirst()
                : (workTypeDefName.NullOrEmpty() ? "(select)" : $"(unknown: {workTypeDefName})");

            if (Widgets.ButtonText(listing.GetRect(24f), btn))
                Find.WindowStack.Add(new FloatMenu(BuildWorkTypeMenu()));

            // Priority buttons: Off (0), 1, 2, 3, 4
            listing.Label($"Priority (0 = disabled, 1 = highest, 4 = lowest)  \u2014 current: {(priority == 0 ? "disabled" : priority.ToString())}");
            Rect row = listing.GetRect(24f);
            float w  = row.width / 5f;

            string[] labels = { "Off", "1", "2", "3", "4" };
            for (int p = 0; p <= 4; p++)
            {
                int pCap = p;
                PawnFilterHelper.DrawToggleBtn(
                    new Rect(row.x + w * p, row.y, w, 24f),
                    labels[p],
                    priority == p,
                    () => priority = pCap);
            }
        }

        private List<FloatMenuOption> BuildWorkTypeMenu()
        {
            var opts = new List<FloatMenuOption>();
            foreach (WorkTypeDef d in DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(d => !d.labelShort.NullOrEmpty())
                .OrderBy(d => d.labelShort))
            {
                WorkTypeDef cap = d;
                opts.Add(new FloatMenuOption(
                    d.labelShort.CapitalizeFirst(),
                    () => workTypeDefName = cap.defName));
            }
            return opts;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pawnKind,         "pawnKind",         PawnKindFilter.Colonist);
            Scribe_Values.Look(ref workTypeDefName,  "workTypeDefName",  "");
            Scribe_Values.Look(ref priority,         "priority",         1);
        }
    }
}
