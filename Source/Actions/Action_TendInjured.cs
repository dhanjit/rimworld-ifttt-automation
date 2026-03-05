using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Assigns the best doctor to tend the most critically injured/sick colonist.
    /// </summary>
    public class Action_TendInjured : AutomationAction
    {
        public override string Label       => "Tend injured/sick pawn";
        public override string Description => "Assigns the best doctor to tend the most critically injured colonist.";

        public override bool HasConfig => false;

        public override void Execute(Map map)
        {
            // Find the worst-off colonist that needs tending
            Pawn patient = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.health?.hediffSet?.hediffs != null
                         && p.health.hediffSet.hediffs
                             .Any(h => h.TendableNow()))
                .OrderBy(p => p.health.summaryHealth.SummaryHealthPercent)
                .FirstOrDefault();

            if (patient == null)
            {
                Log.Message("[IFTTT] TendInjured: No colonist needs tending.");
                return;
            }

            // Find best doctor — must have Doctor work type enabled (L-18)
            Pawn doctor = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && p != patient && p.skills != null
                         && p.workSettings?.WorkIsActive(WorkTypeDefOf.Doctor) == true)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Medicine).Level)
                .FirstOrDefault();

            if (doctor == null)
            {
                Log.Message("[IFTTT] TendInjured: No available doctor.");
                return;
            }

            Job job = JobMaker.MakeJob(JobDefOf.TendPatient, patient);
            doctor.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);

            Messages.Message(
                $"[IFTTT] {doctor.LabelShort} assigned to tend {patient.LabelShort}.",
                MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }
}
