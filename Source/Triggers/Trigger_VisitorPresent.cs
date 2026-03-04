using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when at least one non-hostile humanlike pawn from another faction
    /// is currently on the player's map (i.e., a visitor group).
    /// </summary>
    public class Trigger_VisitorPresent : AutomationTrigger
    {
        public override string Label       => "Visitors are present";
        public override string Description => "Fires when non-hostile visitors from another faction are on the map.";

        public override bool IsTriggered(Map map)
        {
            return map.mapPawns.AllPawnsSpawned.Any(p =>
                p.Faction != null
                && !p.Faction.IsPlayer
                && !p.HostileTo(Faction.OfPlayer)
                && p.RaceProps.Humanlike
                && !p.IsPrisoner
                && p.HostFaction == null);   // not a prisoner/slave
        }
    }
}
