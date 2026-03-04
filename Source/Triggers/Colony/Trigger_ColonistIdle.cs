using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when one or more colonists are idle (no current job).
    /// </summary>
    public class Trigger_ColonistIdle : AutomationTrigger
    {
        public override string Label       => "Colonist idle";
        public override string Description => "Fires when colonist(s) have no current job.";

        public override bool HasConfig => true;

        /// <summary>Minimum number of idle colonists to trigger.</summary>
        public int minIdleCount = 1;

        public override bool IsTriggered(Map map)
        {
            int idle = map.mapPawns.FreeColonistsSpawned
                .Count(p => p.jobs?.curJob == null
                         || p.jobs.curJob.def == JobDefOf.Wait
                         || p.jobs.curJob.def == JobDefOf.Wait_Combat);
            return idle >= minIdleCount;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = minIdleCount.ToString();
            listing.TextFieldNumericLabeled("Min idle colonists: ", ref minIdleCount, ref buf, 1, 50);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minIdleCount, "minIdleCount", 1);
        }
    }
}
