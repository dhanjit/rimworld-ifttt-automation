using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when any powered building on the map is not receiving power.
    /// Detects blackouts / unpowered structures.
    /// </summary>
    public class Trigger_PowerFailure : AutomationTrigger
    {
        public override string Label       => "Power failure";
        public override string Description => "Fires when any powered building loses power.";

        public override bool HasConfig => true;

        /// <summary>Minimum number of unpowered buildings required to trigger.</summary>
        public int minUnpoweredCount = 1;

        public override bool IsTriggered(Map map)
        {
            int count = 0;
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.PowerTrader))
            {
                if (t is ThingWithComps twc)
                {
                    CompPowerTrader comp = twc.TryGetComp<CompPowerTrader>();
                    if (comp != null && comp.Props.PowerConsumption > 0 && !comp.PowerOn)
                    {
                        count++;
                        if (count >= minUnpoweredCount) return true;
                    }
                }
            }
            return false;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = minUnpoweredCount.ToString();
            listing.TextFieldNumericLabeled("Min unpowered buildings: ", ref minUnpoweredCount, ref buf, 1, 999);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minUnpoweredCount, "minUnpoweredCount", 1);
        }
    }
}
