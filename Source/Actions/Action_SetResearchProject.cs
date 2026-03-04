using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Sets the active research project to the configured one (by defName).
    /// Does nothing if the project is already completed or doesn't exist.
    /// </summary>
    public class Action_SetResearchProject : AutomationAction
    {
        public override string Label       => "Set research project";
        public override string Description => "Activates a specific research project by defName.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 95f; // Label + Label + TextEntry

        public string projectDefName = "MicroelectronicsBasics";

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

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Research project defName:");
            listing.Label("(e.g., MicroelectronicsBasics, Gunsmithing, MultiAnalyzer)");
            projectDefName = listing.TextEntry(projectDefName);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref projectDefName, "projectDefName", "MicroelectronicsBasics");
        }
    }
}
