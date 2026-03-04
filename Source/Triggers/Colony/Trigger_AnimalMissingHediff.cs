using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when at least one owned, living animal of the selected race
    /// is on the map but does NOT yet have the SentienceCatalyst hediff.
    /// (Odyssey DLC)
    /// </summary>
    public class Trigger_AnimalMissingHediff : AutomationTrigger
    {
        private static readonly string TargetHediff = "SentienceCatalyst";

        public override string Label       => "Animal needs sentience catalyst";
        public override string Description =>
            $"Any owned {AnimalLabel} on the map that hasn't received the Sentience Catalyst yet.";

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 60f; // label + dropdown button

        public string animalDef = "Thrumbo";

        private string AnimalLabel
        {
            get
            {
                ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(animalDef);
                return d != null ? d.label.CapitalizeFirst() : animalDef;
            }
        }

        public override bool IsTriggered(Map map)
        {
            ThingDef  raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(animalDef);
            HediffDef hDef    = DefDatabase<HediffDef>.GetNamedSilentFail(TargetHediff);

            if (raceDef == null) return false;

            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p.def != raceDef)              continue;
                if (p.Faction != Faction.OfPlayer) continue;
                if (p.Dead || p.Destroyed)         continue;
                if (hDef != null && p.health.hediffSet.HasHediff(hDef)) continue;
                return true;
            }
            return false;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Animal race:");
            listing.Gap(2f);

            ThingDef current = DefDatabase<ThingDef>.GetNamedSilentFail(animalDef);
            string btnLabel  = current != null ? current.label.CapitalizeFirst() : $"(unknown: {animalDef})";

            if (Widgets.ButtonText(listing.GetRect(28f), btnLabel))
            {
                var options = new List<FloatMenuOption>();
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.race != null && d.race.Animal)
                    .OrderBy(d => d.label))
                {
                    ThingDef captured = def;
                    options.Add(new FloatMenuOption(
                        def.label.CapitalizeFirst(),
                        () => animalDef = captured.defName));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref animalDef, "animalDef", "Thrumbo");
        }
    }
}
