using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Finds a powered comms console, an allied faction that can send traders,
    /// and assigns a free colonist the job of calling them.
    /// </summary>
    public class Action_CallAlliedTrader : AutomationAction
    {
        public override string Label       => "Call allied trader";
        public override string Description => "Assigns a colonist to the comms console to call " +
                                              "a trader from the best available allied faction.";

        public override void Execute(Map map)
        {
            // 1. Find a powered comms console.
            Building_CommsConsole comms = map.listerBuildings
                .AllBuildingsColonistOfClass<Building_CommsConsole>()
                .FirstOrDefault(b => b.Spawned && b.GetComp<CompPowerTrader>()?.PowerOn == true);

            if (comms == null)
            {
                Log.Message("[IFTTT] CallAlliedTrader: No powered comms console found.");
                return;
            }

            // 2. Find the best allied faction to call (highest goodwill with canRequestTraders).
            Faction targetFaction = Find.FactionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer
                         && f.PlayerRelationKind != FactionRelationKind.Hostile
                         && f.def.canRequestTraders)
                .OrderByDescending(f => f.PlayerGoodwill)
                .FirstOrDefault();

            if (targetFaction == null)
            {
                Log.Message("[IFTTT] CallAlliedTrader: No suitable faction to call.");
                return;
            }

            // 3. Find a free colonist who can reach the console.
            Pawn caller = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && !p.Dead
                         && p.CanReserveAndReach(comms, PathEndMode.InteractionCell, Danger.Deadly))
                .OrderByDescending(p => p.skills?.GetSkill(DefDatabase<SkillDef>.GetNamedSilentFail("Social"))?.Level ?? 0)
                .FirstOrDefault();

            if (caller == null)
            {
                Log.Message("[IFTTT] CallAlliedTrader: No free colonist can reach the comms console.");
                return;
            }

            // 4. Create the comms job.
            Job job = JobMaker.MakeJob(JobDefOf.UseCommsConsole, comms);
            job.commTarget = targetFaction;
            caller.jobs.TryTakeOrderedJob(job, JobTag.Misc);

            Messages.Message(
                $"[IFTTT] {caller.LabelShort} is calling {targetFaction.Name} for a trader.",
                caller,
                MessageTypeDefOf.PositiveEvent,
                historical: false);
        }
    }
}
