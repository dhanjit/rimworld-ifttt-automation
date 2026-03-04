using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the colony holds a skill neurotrainer that at least one passionate
    /// colonist (below level 20) could benefit from.
    /// </summary>
    public class Trigger_HasSkillTrainer : AutomationTrigger
    {
        public override string Label       => "Has usable skill trainer";
        public override string Description => "Fires when the colony has a skill neurotrainer " +
                                              "that a passionate colonist (below level 20) could use.";

        public override bool IsTriggered(Map map)
        {
            // Find items by looking for those carrying CompUseEffect_LearnSkill
            // (ThingsInGroup takes a ThingRequestGroup enum, not a ThingCategoryDef).
            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                CompUseEffect_LearnSkill comp = thing.TryGetComp<CompUseEffect_LearnSkill>();
                if (comp?.Props?.skill == null) continue;

                SkillDef skillDef = comp.Props.skill;
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn.skills == null) continue;
                    SkillRecord skill = pawn.skills.GetSkill(skillDef);
                    if (skill != null
                        && !skill.TotallyDisabled
                        && skill.passion != Passion.None
                        && skill.Level < 20)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
