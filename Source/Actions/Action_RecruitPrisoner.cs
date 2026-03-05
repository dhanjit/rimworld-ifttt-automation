using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Assigns the best social colonist to attempt to recruit the most persuadable prisoner.
    /// </summary>
    public class Action_RecruitPrisoner : AutomationAction
    {
        public override string Label       => "Recruit prisoner";
        public override string Description => "Assigns the best recruiter to attempt prisoner recruitment.";

        public override bool HasConfig => false;

        public override bool Execute(Map map)
        {
            Pawn prisoner = map.mapPawns.PrisonersOfColonySpawned
                .Where(p => !p.Downed && !p.Dead
                         && p.guest?.Recruitable == true)
                .OrderByDescending(p => p.guest?.resistance ?? 0f) // lowest resistance = most persuadable? reversed
                .OrderBy(p => p.guest?.resistance ?? 999f)
                .FirstOrDefault();

            if (prisoner == null)
            {
                Log.Message("[IFTTT] RecruitPrisoner: No recruitable prisoners.");
                return false;
            }

            Pawn recruiter = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && p.skills != null)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level)
                .FirstOrDefault();

            if (recruiter == null)
            {
                Log.Message("[IFTTT] RecruitPrisoner: No colonist available to recruit.");
                return false;
            }

            Job job = JobMaker.MakeJob(JobDefOf.PrisonerAttemptRecruit, prisoner);
            recruiter.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);

            Messages.Message(
                $"[IFTTT] {recruiter.LabelShort} is attempting to recruit {prisoner.LabelShort}.",
                MessageTypeDefOf.NeutralEvent, historical: false);

            return true;
        }
    }
}
