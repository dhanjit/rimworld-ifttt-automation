using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Finds the best skill neurotrainer in the colony and assigns it to the
    /// most suitable colonist (highest passion, lowest skill, not incapacitated).
    /// </summary>
    public class Action_UseSkillTrainer : AutomationAction
    {
        public override string Label       => "Use skill trainer";
        public override string Description => "Assigns the best available skill trainer to the most " +
                                              "suitable passionate colonist.";

        public override bool Execute(Map map)
        {
            JobDef useNeurotrainerDef = DefDatabase<JobDef>.GetNamedSilentFail("UseNeurotrainer");
            if (useNeurotrainerDef == null)
            {
                Log.Warning("[IFTTT] UseSkillTrainer: UseNeurotrainer job def not found.");
                return false;
            }

            // Score each (pawn, trainer) pair and pick the best one.
            Pawn  bestPawn    = null;
            Thing bestTrainer = null;
            float bestScore   = -1f;

            // Iterate haulable items and filter to skill neurotrainers by comp.
            foreach (Thing trainer in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                CompUseEffect_LearnSkill comp = trainer.TryGetComp<CompUseEffect_LearnSkill>();
                if (comp?.Props?.skill == null) continue;
                SkillDef skillDef = comp.Props.skill;

                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn.skills == null) continue;
                    if (pawn.Downed || pawn.Dead) continue;
                    if (!pawn.CanReserveAndReach(trainer, PathEndMode.Touch, Danger.Deadly)) continue;

                    SkillRecord skill = pawn.skills.GetSkill(skillDef);
                    if (skill == null || skill.TotallyDisabled || skill.Level >= 20) continue;

                    // Score: passion matters most, lower existing skill is a bigger gap to fill.
                    float passionBonus = skill.passion == Passion.Major ? 2f : (skill.passion == Passion.Minor ? 1f : 0f);
                    if (passionBonus <= 0f) continue;

                    float score = passionBonus * (20 - skill.Level);
                    if (score > bestScore)
                    {
                        bestScore   = score;
                        bestPawn    = pawn;
                        bestTrainer = trainer;
                    }
                }
            }

            if (bestPawn == null)
            {
                Log.Message("[IFTTT] UseSkillTrainer: No suitable pawn/trainer pair found.");
                return false;
            }

            Job job = JobMaker.MakeJob(useNeurotrainerDef, bestTrainer);
            bestPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);

            Messages.Message(
                $"[IFTTT] {bestPawn.LabelShort} will use a skill trainer.",
                bestPawn,
                MessageTypeDefOf.PositiveEvent,
                historical: false);

            return true;
        }
    }
}
