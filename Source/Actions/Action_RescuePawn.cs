using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Assigns the nearest capable colonist to rescue the most critically downed pawn.
    /// Prioritizes colonists with highest medicine skill.
    /// </summary>
    public class Action_RescuePawn : AutomationAction
    {
        public override string Label       => "Rescue downed pawn";
        public override string Description => "Assigns a colonist to rescue the most critically downed pawn.";

        public override bool HasConfig => false;

        public override void Execute(Map map)
        {
            // Find the most critically downed pawn (lowest health)
            Pawn target = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.Downed && !p.Dead)
                .OrderBy(p => p.health.summaryHealth.SummaryHealthPercent)
                .FirstOrDefault();

            if (target == null)
            {
                Log.Message("[IFTTT] RescuePawn: No downed colonists found.");
                return;
            }

            // Find a capable rescuer
            Pawn rescuer = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && p != target && p.skills != null)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Medicine).Level)
                .FirstOrDefault();

            if (rescuer == null)
            {
                Log.Message("[IFTTT] RescuePawn: No available colonist to perform rescue.");
                return;
            }

            // Find nearest bed for rescue
            Building_Bed bed = RestUtility.FindBedFor(target, rescuer, checkSocialProperness: false);

            Job job = bed != null
                ? JobMaker.MakeJob(JobDefOf.Rescue, target, bed)
                : JobMaker.MakeJob(JobDefOf.Rescue, target);

            rescuer.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);

            Messages.Message(
                $"[IFTTT] {rescuer.LabelShort} is rescuing {target.LabelShort}.",
                MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }
}
