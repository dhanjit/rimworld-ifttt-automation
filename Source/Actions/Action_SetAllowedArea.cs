using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Sets the allowed area restriction for a group of pawns.
    /// Setting to "(unrestricted)" removes the restriction entirely so pawns can go anywhere.
    ///
    /// Common automation examples:
    ///   • Raid detected → restrict colonists to Home area
    ///   • Raid over → unrestrict colonists
    ///   • Toxic fallout → restrict everyone to an indoor area
    ///   • Merchant arrives → allow traders to go to trade zone
    /// </summary>
    public class Action_SetAllowedArea : AutomationAction
    {
        // ── Fields ────────────────────────────────────────────────────────────

        public PawnKindFilter pawnKind    = PawnKindFilter.Colonist;
        public string         raceDefName = "";
        /// <summary>Label of the target area. Empty string means unrestricted (no restriction).</summary>
        public string         areaLabel   = "";

        // ── AutomationAction overrides ────────────────────────────────────────

        public override string Label => "Set allowed area";

        public override string Description
        {
            get
            {
                string kind = pawnKind.ToString().ToLower();
                string area = areaLabel.NullOrEmpty() ? "unrestricted" : $"'{areaLabel}'";
                return $"Set {kind} allowed area \u2192 {area}";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight
        {
            get
            {
                float h = 100f;  // DrawKindFilter (~50) + area dropdown (~50)
                if (pawnKind == PawnKindFilter.Animal || pawnKind == PawnKindFilter.Any)
                    h += 50f;    // race filter label + dropdown
                return h;
            }
        }

        // ── Execute ───────────────────────────────────────────────────────────

        public override bool Execute(Map map)
        {
            // Resolve area — null means unrestricted
            Area target = null;
            if (!areaLabel.NullOrEmpty())
            {
                target = map.areaManager.AllAreas
                    .FirstOrDefault(a => a.Label == areaLabel);
                if (target == null)
                    return false; // the configured area was removed from the map
            }

            var pawns = PawnFilterHelper.GetPawns(map, pawnKind, "", raceDefName)
                .Where(p => !p.Dead && p.Spawned && p.playerSettings != null)
                .ToList();

            if (pawns.Count == 0) return false;

            foreach (Pawn pawn in pawns)
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = target;

            return true;
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            PawnFilterHelper.DrawKindFilter(pawnKind, v => pawnKind = v, listing);

            if (pawnKind == PawnKindFilter.Animal || pawnKind == PawnKindFilter.Any)
            {
                listing.Label("Race filter (optional):");
                PawnFilterHelper.DrawRaceDropdown(raceDefName, v => raceDefName = v,
                    listing.GetRect(24f));
            }

            listing.Label("Restrict to area (empty = unrestricted):");
            DrawAreaDropdown(listing.GetRect(24f));
        }

        private void DrawAreaDropdown(Rect rect)
        {
            string btnLabel = areaLabel.NullOrEmpty() ? "(unrestricted)" : areaLabel;
            if (!Widgets.ButtonText(rect, btnLabel)) return;

            Map map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;

            var opts = new List<FloatMenuOption>
            {
                new FloatMenuOption(
                    "(unrestricted \u2014 remove area restriction)",
                    () => areaLabel = "")
            };

            if (map != null)
            {
                foreach (Area area in map.areaManager.AllAreas)
                {
                    string label = area.Label;  // captured for closure
                    opts.Add(new FloatMenuOption(label, () => areaLabel = label));
                }
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pawnKind,    "pawnKind",    PawnKindFilter.Colonist);
            Scribe_Values.Look(ref raceDefName, "raceDefName", "");
            Scribe_Values.Look(ref areaLabel,   "areaLabel",   "");
        }
    }
}
