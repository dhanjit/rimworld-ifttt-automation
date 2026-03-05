using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Drafts colonists with the highest Shooting skill above a configurable threshold.
    /// Optionally positions them near the home zone center.
    /// </summary>
    public class Action_DraftAllShooters : AutomationAction
    {
        public override string Label       => "Draft all shooters";
        public override string Description => "Drafts colonists with high shooting skill for combat.";

        public override bool HasConfig => true;

        public int minShootingSkill = 5;
        public int maxToDraft       = 10;

        public override bool Execute(Map map)
        {
            var candidates = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed
                         && p.skills != null
                         && p.skills.GetSkill(SkillDefOf.Shooting).Level >= minShootingSkill
                         && p.drafter != null
                         && !p.drafter.Drafted)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Shooting).Level)
                .Take(maxToDraft)
                .ToList();

            if (candidates.Count == 0)
            {
                Log.Message("[IFTTT] DraftAllShooters: No eligible colonists.");
                return false;
            }

            foreach (Pawn p in candidates)
                p.drafter.Drafted = true;

            Messages.Message(
                $"[IFTTT] Drafted {candidates.Count} shooter(s) for combat.",
                MessageTypeDefOf.CautionInput, historical: false);

            return true;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf1 = minShootingSkill.ToString();
            listing.TextFieldNumericLabeled("Min shooting skill: ", ref minShootingSkill, ref buf1, 0, 20);

            string buf2 = maxToDraft.ToString();
            listing.TextFieldNumericLabeled("Max to draft: ", ref maxToDraft, ref buf2, 1, 50);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minShootingSkill, "minShootingSkill", 5);
            Scribe_Values.Look(ref maxToDraft,       "maxToDraft",       10);
        }
    }
}
