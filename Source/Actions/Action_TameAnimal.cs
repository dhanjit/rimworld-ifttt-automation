using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Assigns the best animal-handler colonist to tame the nearest wild tamable animal.
    /// Picks the animal closest to the colony center.
    /// </summary>
    public class Action_TameAnimal : AutomationAction
    {
        public override string Label       => "Tame nearest animal";
        public override string Description => "Assigns the best animal handler to tame the nearest wild tamable animal.";

        public override bool HasConfig => false;

        public override void Execute(Map map)
        {
            // Find a tamable wild animal
            Pawn target = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Animal
                         && p.Faction == null
                         && !p.Downed
                         && p.RaceProps.trainability != null
                         && p.RaceProps.trainability != TrainabilityDefOf.None)
                .OrderBy(p => p.Position.DistanceTo(map.Center))
                .FirstOrDefault();

            if (target == null)
            {
                Log.Message("[IFTTT] TameAnimal: No tamable wild animals found.");
                return;
            }

            // Find the best handler — must have Handling work type enabled (L-17)
            Pawn handler = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && p.skills != null
                         && p.workSettings?.WorkIsActive(WorkTypeDefOf.Handling) == true)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Animals).Level)
                .FirstOrDefault();

            if (handler == null)
            {
                Log.Message("[IFTTT] TameAnimal: No available colonist handler.");
                return;
            }

            Job job = JobMaker.MakeJob(JobDefOf.Tame, target);
            handler.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);

            Messages.Message(
                $"[IFTTT] {handler.LabelShort} assigned to tame {target.LabelShort}.",
                MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }
}
