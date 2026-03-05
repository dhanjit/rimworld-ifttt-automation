using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// UndrAfts all currently drafted colonists.
    /// Useful as a post-raid cleanup action.
    /// </summary>
    public class Action_UndraftAll : AutomationAction
    {
        public override string Label       => "Undraft all colonists";
        public override string Description => "Releases all drafted colonists back to civilian mode.";

        public override bool HasConfig => false;

        public override bool Execute(Map map)
        {
            var drafted = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.drafter?.Drafted == true)
                .ToList();

            foreach (Pawn p in drafted)
                p.drafter.Drafted = false;

            if (drafted.Count > 0)
                Messages.Message(
                    $"[IFTTT] Undrafted {drafted.Count} colonist(s).",
                    MessageTypeDefOf.NeutralEvent,
                    historical: false);

            return drafted.Count > 0;
        }
    }
}
