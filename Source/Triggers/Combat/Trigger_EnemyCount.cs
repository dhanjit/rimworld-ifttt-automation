using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when the number of hostile pawns on the map reaches or exceeds
    /// a configurable minimum count.
    /// </summary>
    public class Trigger_EnemyCount : AutomationTrigger
    {
        public override string Label       => "Enemy count";
        public override string Description => "Fires when the number of hostile pawns on the map is at or above the threshold.";

        public override bool HasConfig => true;

        public int minEnemies = 3;

        public override bool IsTriggered(Map map)
        {
            int count = map.mapPawns.AllPawnsSpawned
                .Count(p => p.HostileTo(Faction.OfPlayer) && !p.Downed && !p.Dead);
            return count >= minEnemies;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = minEnemies.ToString();
            listing.TextFieldNumericLabeled("Minimum enemies: ", ref minEnemies, ref buf, 1, 999);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minEnemies, "minEnemies", 3);
        }
    }
}
