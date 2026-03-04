using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Assigns idle colonists to haul items to stockpiles.
    /// Targets the most idle colonists to maximize throughput.
    /// </summary>
    public class Action_HaulToStockpile : AutomationAction
    {
        public override string Label       => "Mass haul to stockpile";
        public override string Description => "Assigns all idle colonists to haul items to stockpiles.";

        public override bool HasConfig => true;

        public int maxHaulers = 5;

        public override void Execute(Map map)
        {
            var idleColonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed
                         && p.workSettings?.WorkIsActive(WorkTypeDefOf.Hauling) == true
                         && (p.jobs?.curJob == null
                          || p.jobs.curJob.def == JobDefOf.Wait
                          || p.jobs.curJob.def == JobDefOf.Wait_MaintainPosture))
                .Take(maxHaulers)
                .ToList();

            if (idleColonists.Count == 0)
            {
                Log.Message("[IFTTT] HaulToStockpile: No idle haulers available.");
                return;
            }

            int assigned = 0;
            foreach (Pawn p in idleColonists)
            {
                Thing haulable = GenClosest.ClosestThing_Global_Reachable(
                    p.Position, map,
                    map.listerHaulables.ThingsPotentiallyNeedingHauling(),
                    PathEndMode.ClosestTouch,
                    TraverseParms.For(p));

                if (haulable == null) break;

                Job job = HaulAIUtility.HaulToStorageJob(p, haulable, false);
                if (job != null)
                {
                    p.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);
                    assigned++;
                }
            }

            if (assigned > 0)
                Messages.Message(
                    $"[IFTTT] Assigned {assigned} colonist(s) to haul items.",
                    MessageTypeDefOf.NeutralEvent, historical: false);
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = maxHaulers.ToString();
            listing.TextFieldNumericLabeled("Max haulers to assign: ", ref maxHaulers, ref buf, 1, 50);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref maxHaulers, "maxHaulers", 5);
        }
    }
}
