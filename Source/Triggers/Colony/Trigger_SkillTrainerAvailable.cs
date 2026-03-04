using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when a neurotrainer (skill trainer item) is in the stockpile
    /// AND there exists a colonist who can benefit from it (has passion for the skill and is not capped).
    /// </summary>
    public class Trigger_SkillTrainerAvailable : AutomationTrigger
    {
        public override string Label       => "Skill trainer available for use";
        public override string Description =>
            "Fires when a skill trainer exists AND a colonist with passion for that skill can benefit.";

        public override bool HasConfig => false;

        public override bool IsTriggered(Map map)
        {
            var trainers = map.listerThings
                .ThingsInGroup(ThingRequestGroup.HaulableEver)
                .Where(t => t.TryGetComp<CompUseEffect_LearnSkill>() != null)
                .ToList();

            if (trainers.Count == 0) return false;

            var colonists = map.mapPawns.FreeColonistsSpawned.ToList();

            foreach (Thing trainer in trainers)
            {
                CompUseEffect_LearnSkill comp = trainer.TryGetComp<CompUseEffect_LearnSkill>();
                if (comp == null) continue;

                SkillDef skillDef = comp.Props.skill;
                if (skillDef == null) continue;

                bool hasCandidate = colonists.Any(p =>
                {
                    Pawn_SkillTracker skills = p.skills;
                    if (skills == null) return false;
                    SkillRecord rec = skills.GetSkill(skillDef);
                    return rec != null
                        && rec.passion >= Passion.Minor
                        && rec.levelInt < SkillRecord.MaxLevel;
                });

                if (hasCandidate) return true;
            }

            return false;
        }
    }
}
