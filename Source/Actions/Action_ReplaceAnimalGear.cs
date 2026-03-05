using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// For every owned animal whose apparel has fallen below the HP threshold,
    /// dispatches the nearest free colonist to physically pick up a replacement
    /// and walk it to the animal — exactly how AAF's own "force equip" works.
    ///
    /// Uses the IFTTT_ReplaceAnimalGear JobDef / JobDriver_ReplaceAnimalGear.
    /// Requires the Animal Apparel Framework mod at runtime.
    /// </summary>
    public class Action_ReplaceAnimalGear : AutomationAction
    {
        public override string Label       => "Replace animal gear";
        public override string Description =>
            $"Dispatch a colonist to replace worn-out animal apparel (below {wornThreshold:P0} HP).";

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 55f; // Label + Slider

        /// HP fraction below which apparel is considered worn-out. Default 55 %.
        public float wornThreshold = 0.55f;

        public override bool Execute(Map map)
        {
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("IFTTT_ReplaceAnimalGear");
            if (jobDef == null)
            {
                Log.Error("[IFTTT] ReplaceAnimalGear: 'IFTTT_ReplaceAnimalGear' JobDef not found. " +
                          "Is JobDefs_IFTTT.xml deployed to the Defs folder?");
                return false;
            }

            // Items and handlers already committed this Execute() call
            var usedItems    = new HashSet<Thing>();
            var usedHandlers = new HashSet<Pawn>();
            int dispatched   = 0;
            bool noHandlers  = false;

            foreach (Pawn animal in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (noHandlers) break;

                if (!animal.RaceProps.Animal)            continue;
                if (animal.Faction != Faction.OfPlayer) continue;
                if (animal.Downed)                      continue;
                if (animal.apparel == null)             continue; // AAF not active

                foreach (Apparel worn in animal.apparel.WornApparel.ToList())
                {
                    float hpPct = worn.HitPoints / (float)worn.MaxHitPoints;
                    if (hpPct >= wornThreshold) continue;

                    // Skip if a handler is already en route to this animal for gear replacement
                    bool alreadyInProgress = map.mapPawns.AllPawnsSpawned.Any(p =>
                        p.Faction == Faction.OfPlayer             &&
                        p.CurJob?.def?.defName == "IFTTT_ReplaceAnimalGear" &&
                        p.CurJob.targetB.Pawn == animal);
                    if (alreadyInProgress) continue;

                    // Find a healthy replacement of the same def not already claimed
                    Apparel replacement = FindReplacement(map, worn.def, wornThreshold, usedItems);
                    if (replacement == null)
                    {
                        Log.Message($"[IFTTT] ReplaceAnimalGear: No healthy '{worn.def.label}' " +
                                    $"in storage for {animal.LabelShort}.");
                        continue;
                    }

                    // Nearest free colonist not already committed this call
                    Pawn handler = map.mapPawns.FreeColonistsSpawned
                        .Where(p => !p.Downed && !p.Dead && p.Spawned && !usedHandlers.Contains(p))
                        .OrderBy(p => p.Position.DistanceTo(replacement.Position))
                        .FirstOrDefault();

                    if (handler == null) { noHandlers = true; break; }

                    Job job = JobMaker.MakeJob(jobDef, replacement, animal);
                    handler.jobs.StartJob(job, JobCondition.InterruptForced, null,
                                          resumeCurJobAfterwards: false);

                    usedItems.Add(replacement);
                    usedHandlers.Add(handler);
                    dispatched++;
                }
            }

            if (dispatched > 0)
                Messages.Message(
                    $"[IFTTT] Dispatched {dispatched} colonist(s) to replace animal gear.",
                    MessageTypeDefOf.NeutralEvent, historical: false);
            else
                Log.Message("[IFTTT] ReplaceAnimalGear: Nothing dispatched " +
                            "(no worn-out gear, no replacements in storage, or no free colonists).");

            return dispatched > 0;
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label($"Replace when HP falls below: {wornThreshold:P0}");
            wornThreshold = listing.Slider(wornThreshold, 0.05f, 0.95f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref wornThreshold, "wornThreshold", 0.55f);
        }

        private static Apparel FindReplacement(Map map, ThingDef def,
                                               float minHPFraction, HashSet<Thing> excluded)
        {
            foreach (Thing t in map.listerThings.ThingsOfDef(def))
            {
                if (!(t is Apparel ap))              continue;
                if (!ap.Spawned || ap.Destroyed)     continue;
                if (ap.IsForbidden(Faction.OfPlayer)) continue;
                if (ap.Wearer != null)               continue; // already worn
                if (excluded.Contains(ap))           continue; // claimed this call
                if (ap.HitPoints / (float)ap.MaxHitPoints < minHPFraction) continue;
                return ap;
            }
            return null;
        }
    }
}
