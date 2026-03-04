using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Enables or disables a specific work type for all colonists.
    /// Useful for mass-enabling firefighting during fires, or mass-enabling hauling.
    /// </summary>
    public class Action_ToggleWorkType : AutomationAction
    {
        public override string Label       => "Toggle work type";
        public override string Description => "Enables or disables a work type for all colonists.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 130f; // Label + TextEntry + Checkbox + (optional) TextFieldNumeric

        public string workTypeDefName = "Firefighter";
        public bool   enable          = true;
        /// <summary>Priority to assign when enabling (1=highest, 4=lowest, 0=off).</summary>
        public int    priority        = 1;

        public override void Execute(Map map)
        {
            WorkTypeDef workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeDefName);
            if (workType == null)
            {
                Log.Warning($"[IFTTT] ToggleWorkType: Unknown work type '{workTypeDefName}'.");
                return;
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
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Work type defName (e.g., Firefighter, Hauling, Cleaning, Mining):");
            workTypeDefName = listing.TextEntry(workTypeDefName);
            listing.CheckboxLabeled("Enable (unchecked = disable)", ref enable);
            if (enable)
            {
                string buf = priority.ToString();
                listing.TextFieldNumericLabeled("Priority (1=highest, 4=lowest): ", ref priority, ref buf, 1, 4);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workTypeDefName, "workTypeDefName", "Firefighter");
            Scribe_Values.Look(ref enable,          "enable",          true);
            Scribe_Values.Look(ref priority,        "priority",        1);
        }
    }
}
