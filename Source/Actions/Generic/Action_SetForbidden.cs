using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Generic action: forbids or allows every spawned instance of a given
    /// ThingDef on the map — works for items, buildings, or any selectable thing.
    ///
    /// Replaces the specific Action_ForbidAllItems and Action_SetDeepDrillForbidden
    /// for general use. Those still exist for their specialised configs.
    /// </summary>
    public class Action_SetForbidden : AutomationAction
    {
        public override string Label => "Set forbidden";
        public override string Description
        {
            get
            {
                ThingDef d    = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
                string tLabel = d != null ? d.label.CapitalizeFirst() : thingDefName;
                return $"{(forbid ? "Forbid" : "Allow")} all {tLabel} on map";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 90f;

        public string thingDefName = "";
        public bool   forbid       = true;

        // ── Execute ───────────────────────────────────────────────────────────

        public override bool Execute(Map map)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            if (def == null) return false;

            foreach (Thing t in map.listerThings.ThingsOfDef(def))
                if (t.Spawned && !t.Destroyed)
                    t.SetForbidden(forbid);

            return true;
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Thing / building:");
            ThingDef cur = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            string btn = cur != null ? cur.label.CapitalizeFirst()
                : (thingDefName.NullOrEmpty() ? "(select)" : $"(unknown: {thingDefName})");
            if (Widgets.ButtonText(listing.GetRect(28f), btn))
                Find.WindowStack.Add(new FloatMenu(BuildThingMenu()));

            listing.Gap(4f);

            Rect row    = listing.GetRect(24f);
            float hw    = row.width / 2f;
            bool isForbid = forbid;
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x,      row.y, hw, 24f), "Forbid", isForbid,  () => forbid = true);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + hw, row.y, hw, 24f), "Allow",  !isForbid, () => forbid = false);
        }

        private List<FloatMenuOption> BuildThingMenu()
        {
            var opts = new List<FloatMenuOption>();
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.selectable &&
                            (d.category == ThingCategory.Item ||
                             d.category == ThingCategory.Building))
                .OrderBy(d => d.label))
            {
                ThingDef cap = d;
                string prefix = d.category == ThingCategory.Building ? "[B] " : "";
                opts.Add(new FloatMenuOption($"{prefix}{d.label.CapitalizeFirst()}", () => thingDefName = cap.defName));
            }
            return opts;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref thingDefName, "thingDefName", "");
            Scribe_Values.Look(ref forbid,       "forbid",       true);
        }
    }
}
