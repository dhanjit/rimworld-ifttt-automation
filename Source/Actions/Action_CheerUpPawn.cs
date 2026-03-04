using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Assigns the highest-social colonist to socially interact with the saddest colonist.
    /// Uses the SocialRelax job to encourage them to mingle and chat.
    /// </summary>
    public class Action_CheerUpPawn : AutomationAction
    {
        public override string Label       => "Cheer up sad colonist";
        public override string Description => "Sends the most social colonist to cheer up the saddest one.";

        public override bool HasConfig => false;

        public override void Execute(Map map)
        {
            var colonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && p.needs?.mood != null)
                .ToList();

            if (colonists.Count < 2)
            {
                Log.Message("[IFTTT] CheerUpPawn: Need at least 2 colonists.");
                return;
            }

            Pawn saddest = colonists.OrderBy(p => p.needs.mood.CurLevel).First();

            Pawn talker = colonists
                .Where(p => p != saddest && p.skills != null && !p.InMentalState)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level)
                .FirstOrDefault();

            if (talker == null)
            {
                Log.Message("[IFTTT] CheerUpPawn: No suitable talker found.");
                return;
            }

            // Direct the talker to go be social near the sad colonist
            Job job = JobMaker.MakeJob(JobDefOf.GotoAndBeSociallyActive, saddest);
            talker.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);

            Messages.Message(
                $"[IFTTT] {talker.LabelShort} is cheering up {saddest.LabelShort}.",
                MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }
}
