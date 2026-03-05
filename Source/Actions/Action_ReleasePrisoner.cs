using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Releases the prisoner with the lowest resistance (most persuaded / least dangerous),
    /// freeing up a prison cell. Can optionally release all prisoners.
    /// </summary>
    public class Action_ReleasePrisoner : AutomationAction
    {
        public override string Label       => "Release prisoner";
        public override string Description => "Releases the least dangerous prisoner to free prison space.";

        public override bool HasConfig => true;

        public bool releaseAll = false;

        public override bool Execute(Map map)
        {
            var prisoners = map.mapPawns.PrisonersOfColonySpawned
                .Where(p => !p.Dead)
                .ToList();

            if (prisoners.Count == 0)
            {
                Log.Message("[IFTTT] ReleasePrisoner: No prisoners found.");
                return false;
            }

            var toRelease = releaseAll
                ? prisoners
                : prisoners
                    .OrderBy(p => p.guest?.resistance ?? 0f)
                    .Take(1)
                    .ToList();

            foreach (Pawn p in toRelease)
            {
                // Remove prisoner status — pawn will begin to leave on their own
                p.guest.SetGuestStatus(null);
            }

            Messages.Message(
                $"[IFTTT] Released {toRelease.Count} prisoner(s).",
                MessageTypeDefOf.NeutralEvent, historical: false);

            return true;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.CheckboxLabeled("Release ALL prisoners (unchecked = only least dangerous)", ref releaseAll);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref releaseAll, "releaseAll", false);
        }
    }
}
