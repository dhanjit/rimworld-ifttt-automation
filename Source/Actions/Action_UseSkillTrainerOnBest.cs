using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Finds the best-matched skill trainer in the stockpile and assigns
    /// the most passionate colonist to immediately use it.
    /// Improves on the original Action_UseSkillTrainer by matching trainer to best candidate.
    /// </summary>
    public class Action_UseSkillTrainerOnBest : AutomationAction
    {
        public override string Label       => "Use skill trainer (best match)";
        public override string Description =>
            "Finds a skill trainer and assigns the most passionate colonist to use it.";

        public override bool HasConfig => false;

        public override void Execute(Map map)
        {
            var trainers = map.listerThings
                .ThingsInGroup(ThingRequestGroup.HaulableEver)
                .Where(t => t.TryGetComp<CompUseEffect_LearnSkill>() != null
                         && !t.IsForbidden(Faction.OfPlayer))
                .ToList();

            if (trainers.Count == 0)
            {
                Log.Message("[IFTTT] UseSkillTrainer: No skill trainers available.");
                return;
            }

            var colonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && p.skills != null)
                .ToList();

            Pawn bestColonist = null;
            Thing bestTrainer = null;
            int bestScore = -1;

            foreach (Thing trainer in trainers)
            {
                CompUseEffect_LearnSkill comp = trainer.TryGetComp<CompUseEffect_LearnSkill>();
                if (comp?.Props?.skill == null) continue;

                SkillDef skill = comp.Props.skill;

                foreach (Pawn p in colonists)
                {
                    SkillRecord rec = p.skills.GetSkill(skill);
                    if (rec == null || rec.levelInt >= SkillRecord.MaxLevel) continue;

                    int score = (int)rec.passion * 100 + rec.levelInt;
                    if (score > bestScore)
                    {
                        bestScore    = score;
                        bestColonist = p;
                        bestTrainer  = trainer;
                    }
                }
            }

            if (bestColonist == null || bestTrainer == null)
            {
                Log.Message("[IFTTT] UseSkillTrainer: No matching colonist/trainer pair.");
                return;
            }

            Job job = JobMaker.MakeJob(JobDefOf.UseNeurotrainer, bestTrainer);
            bestColonist.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);

            Messages.Message(
                $"[IFTTT] {bestColonist.LabelShort} is using a skill trainer.",
                MessageTypeDefOf.PositiveEvent, historical: false);
        }
    }
}
