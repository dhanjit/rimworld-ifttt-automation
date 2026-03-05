using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Designates the most expendable owned animal for slaughter.
    /// Priority: lowest bonding/usefulness first. Excludes bonded animals.
    /// </summary>
    public class Action_SlaughterAnimal : AutomationAction
    {
        public override string Label       => "Slaughter expendable animal";
        public override string Description => "Designates a non-bonded owned animal for slaughter.";

        public override bool HasConfig => true;

        public int slaughterCount = 1;
        public bool excludeBonded = true;

        public override bool Execute(Map map)
        {
            var candidates = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Animal
                         && p.Faction == Faction.OfPlayer
                         && !p.Downed)
                .ToList();

            if (excludeBonded)
                candidates = candidates
                    .Where(p => p.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond) == null)
                    .ToList();

            // Prioritize smaller animals (more expendable) and take the first N
            candidates = candidates
                .OrderBy(p => p.BodySize)
                .Take(slaughterCount)
                .ToList();

            if (candidates.Count == 0)
            {
                Log.Message("[IFTTT] SlaughterAnimal: No suitable animals found.");
                return false;
            }

            int designated = 0;
            foreach (Pawn animal in candidates)
            {
                // C-05: Skip animals already designated to avoid duplicate designations
                if (map.designationManager.DesignationOn(animal, DesignationDefOf.Slaughter) != null)
                    continue;
                map.designationManager.AddDesignation(
                    new Designation(animal, DesignationDefOf.Slaughter));
                designated++;
            }

            if (designated > 0)
                Messages.Message(
                    $"[IFTTT] Designated {designated} animal(s) for slaughter.",
                    MessageTypeDefOf.NeutralEvent, historical: false);

            return designated > 0;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = slaughterCount.ToString();
            listing.TextFieldNumericLabeled("Animals to slaughter: ", ref slaughterCount, ref buf, 1, 50);
            listing.CheckboxLabeled("Exclude bonded animals", ref excludeBonded);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref slaughterCount, "slaughterCount", 1);
            Scribe_Values.Look(ref excludeBonded,  "excludeBonded",  true);
        }
    }
}
