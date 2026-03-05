using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Sets the active research project to the configured one.
    /// Uses a dropdown populated from DefDatabase&lt;ResearchProjectDef&gt; (M-09 fix).
    /// Does nothing if the project is already completed or doesn't exist.
    /// </summary>
    public class Action_SetResearchProject : AutomationAction
    {
        public override string Label => "Set research project";
        public override string Description
        {
            get
            {
                ResearchProjectDef proj = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(projectDefName);
                return proj != null
                    ? $"Set research: {proj.label.CapitalizeFirst()}"
                    : "Set research project";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 54f; // Label + Button

        public string projectDefName = "MicroelectronicsBasics";

        // ── Execute ───────────────────────────────────────────────────────────

        public override void Execute(Map map)
        {
            ResearchProjectDef proj = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(projectDefName);

            if (proj == null)
            {
                Log.Warning($"[IFTTT] SetResearch: No project found with defName '{projectDefName}'.");
                return;
            }

            if (proj.IsFinished)
            {
                Log.Message($"[IFTTT] SetResearch: '{proj.label}' is already complete.");
                return;
            }

            Find.ResearchManager.SetCurrentProject(proj);
            Messages.Message(
                $"[IFTTT] Research set to: {proj.label}.",
                MessageTypeDefOf.NeutralEvent, historical: false);
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Research project:");
            ResearchProjectDef cur = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(projectDefName);
            string btn = cur != null ? cur.label.CapitalizeFirst()
                : (projectDefName.NullOrEmpty() ? "(select project)" : $"(unknown: {projectDefName})");
            if (Widgets.ButtonText(listing.GetRect(28f), btn))
            {
                var opts = new List<FloatMenuOption>();
                foreach (ResearchProjectDef d in DefDatabase<ResearchProjectDef>.AllDefsListForReading
                    .Where(d => !d.label.NullOrEmpty() && !d.IsFinished)
                    .OrderBy(d => d.label))
                {
                    string dn = d.defName;
                    opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(), () => projectDefName = dn));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref projectDefName, "projectDefName", "MicroelectronicsBasics");
        }
    }
}
