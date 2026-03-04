using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when an orbital trade ship is currently passing the colony.
    /// Requires a comms console to actually contact them (handled separately).
    /// </summary>
    public class Trigger_OrbitalTraderPresent : AutomationTrigger
    {
        public override string Label       => "Orbital trader present";
        public override string Description => "Fires when a trade ship is in orbit.";

        public override bool HasConfig => false;

        public override bool IsTriggered(Map map)
        {
            return map.passingShipManager?.passingShips?.Count > 0;
        }
    }
}
