using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Enables or disables a specific work type for all spawned colonists.
    /// Uses a dropdown populated from DefDatabase&lt;WorkTypeDef&gt; (M-10 fix).
    /// Useful for mass-enabling firefighting during fires, or mass-enabling hauling.
    /// </summary>
    public class Action_ToggleWorkType : AutomationAction
    {
        public override string Label => "Toggle work type";
        public override string Description
        {
            get
            {
                WorkTypeDef wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeDefName);
                string wtLabel = wt != null ? wt.labelShort.CapitalizeFirst() : workTypeDefName;
                return $"{(enable ? "Enable" : "Disable")} {wtLabel} for all colonists";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 120f; // Label + Button + Checkbox + (optional) numeric

        public string workTypeDefName = "Firefighter";
        public bool   enable          = true;
        /// <summary>Priority to assign when enabling (1=highest, 4=lowest, 0=off).</summary>
        public int    priority        = 1;

        // ── Execute ───────────────────────────────────────────────────────────

        public override bool Execute(Map map)
        {
            WorkTypeDef workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeDefName);
            if (workType == null)
            {
                Log.Warning($"[IFTTT] ToggleWorkType: Unknown work type '{workTypeDefName}'.");
                return false;
            }

            int affected = 0;
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.workSettings == null || p.workSettings.WorkIsActive(workType) == enable)
                    continue;

                if (enable)
                    p.workSettings.SetPriority(workType, priority);
                else
                    p.workSettings.SetPriority(workType, 0);

                affected++;
            }

            Messages.Message(
                $"[IFTTT] {(enable ? "Enabled" : "Disabled")} '{workType.labelShort}' for {affected} colonist(s).",
                MessageTypeDefOf.NeutralEvent, historical: false);

            return true;
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Work type:");
            WorkTypeDef cur = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeDefName);
            string btn = cur != null ? cur.labelShort.CapitalizeFirst()
                : (workTypeDefName.NullOrEmpty() ? "(select work type)" : $"(unknown: {workTypeDefName})");
            if (Widgets.ButtonText(listing.GetRect(28f), btn))
            {
                var opts = new List<FloatMenuOption>();
                foreach (WorkTypeDef d in DefDatabase<WorkTypeDef>.AllDefsListForReading
                    .Where(d => !d.labelShort.NullOrEmpty())
                    .OrderBy(d => d.labelShort))
                {
                    string dn = d.defName;
                    opts.Add(new FloatMenuOption(d.labelShort.CapitalizeFirst(), () => workTypeDefName = dn));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            listing.CheckboxLabeled("Enable (unchecked = disable)", ref enable);
            if (enable)
            {
                string buf = priority.ToString();
                listing.TextFieldNumericLabeled("Priority (1=highest, 4=lowest): ", ref priority, ref buf, 1, 4);
            }
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workTypeDefName, "workTypeDefName", "Firefighter");
            Scribe_Values.Look(ref enable,          "enable",          true);
            Scribe_Values.Look(ref priority,        "priority",        1);
        }
    }
}
