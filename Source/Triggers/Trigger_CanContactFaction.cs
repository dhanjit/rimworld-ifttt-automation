using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the colony has a powered comms console AND there is at least
    /// one allied faction that can be contacted (to request traders, etc.).
    /// </summary>
    public class Trigger_CanContactFaction : AutomationTrigger
    {
        public override string Label       => "Can contact allied faction";
        public override string Description => "Fires when a powered comms console exists and " +
                                              "at least one allied faction is contactable.";

        public override bool IsTriggered(Map map)
        {
            // Need at least one powered comms console.
            bool hasComms = map.listerBuildings
                .AllBuildingsColonistOfClass<Building_CommsConsole>()
                .Any(b => b.Spawned && b.GetComp<CompPowerTrader>()?.PowerOn == true);

            if (!hasComms) return false;

            // Need at least one non-hostile faction with canRequestTraders.
            bool hasAlly = Find.FactionManager.AllFactionsVisible
                .Any(f => !f.IsPlayer
                          && f.PlayerRelationKind != FactionRelationKind.Hostile
                          && f.def.canRequestTraders);

            return hasAlly;
        }
    }
}
