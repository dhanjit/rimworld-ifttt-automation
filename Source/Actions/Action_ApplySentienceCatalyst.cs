using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// For every owned animal of the chosen race that hasn't yet received the
    /// Sentience Catalyst treatment, finds an available catalyst in the colony
    /// and dispatches a free colonist to apply it.  Handles multiple animals
    /// in a single Execute() call and avoids dispatching duplicate colonists
    /// to an animal that is already being treated.  (Odyssey DLC)
    /// </summary>
    public class Action_ApplySentienceCatalyst : AutomationAction
    {
        private static readonly string ItemDefName   = "SentienceCatalyst";
        private static readonly string HediffDefName = "SentienceCatalyst";

        public override string Label       => "Apply sentience catalyst to animal";
        public override string Description =>
            $"Dispatch colonists to apply a Sentience Catalyst to every untreated '{AnimalLabel}'.";

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 60f;

        public string animalDef = "Thrumbo";

        private string AnimalLabel
        {
            get
            {
                ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(animalDef);
                return d != null ? d.label.CapitalizeFirst() : animalDef;
            }
        }

        public override void Execute(Map map)
        {
            ThingDef  iDef    = DefDatabase<ThingDef>.GetNamedSilentFail(ItemDefName);
            ThingDef  raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(animalDef);
            HediffDef hDef    = DefDatabase<HediffDef>.GetNamedSilentFail(HediffDefName);

            if (iDef == null)
            {
                Log.Warning("[IFTTT] ApplySentienceCatalyst: 'SentienceCatalyst' ThingDef not found. " +
                            "Is the Odyssey DLC active?");
                return;
            }
            if (raceDef == null)
            {
                Log.Warning($"[IFTTT] ApplySentienceCatalyst: animal def '{animalDef}' not found.");
                return;
            }

            // All untreated, healthy, owned animals of the chosen race
            List<Pawn> targets = map.mapPawns.AllPawnsSpawned
                .Where(p =>
                    p.def == raceDef               &&
                    p.Faction == Faction.OfPlayer  &&
                    !p.Dead && !p.Destroyed        &&
                    !p.Downed                      &&
                    (hDef == null || !p.health.hediffSet.HasHediff(hDef)))
                .ToList();

            if (targets.Count == 0)
            {
                Log.Message($"[IFTTT] ApplySentienceCatalyst: no untreated '{animalDef}' found.");
                return;
            }

            // Track items and handlers already committed this Execute() call
            // so we don't double-assign them across the loop.
            var usedItems    = new HashSet<Thing>();
            var usedHandlers = new HashSet<Pawn>();
            int dispatched   = 0;

            foreach (Pawn target in targets)
            {
                // Skip if a colonist is already on their way to treat this specific animal
                // (prevents re-dispatching between check intervals while the job is in flight)
                bool alreadyInProgress = map.mapPawns.FreeColonistsSpawned
                    .Any(p => p.CurJob?.targetB.Pawn == target);
                if (alreadyInProgress) continue;

                // Find an available catalyst not yet claimed this call
                Thing item = map.listerThings.ThingsOfDef(iDef)
                    .FirstOrDefault(t =>
                        t.Spawned                          &&
                        !t.Destroyed                       &&
                        !t.IsForbidden(Faction.OfPlayer)   &&
                        !usedItems.Contains(t));

                if (item == null) break; // No more catalysts in colony

                // Nearest free colonist not yet committed this call
                Pawn handler = map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Downed && !p.Dead && p.Spawned && !usedHandlers.Contains(p))
                    .OrderBy(p => p.Position.DistanceTo(item.Position))
                    .FirstOrDefault();

                if (handler == null) break; // No more free colonists

                // Dispatch via the item's own CompUsable (uses DLC job + targeting logic)
                CompUsable comp = item.TryGetComp<CompUsable>();
                if (comp != null)
                {
                    comp.TryStartUseJob(handler, new LocalTargetInfo(target));
                }
                else
                {
                    // Fallback: build the UseItem job manually
                    JobDef useItemJob = DefDatabase<JobDef>.GetNamedSilentFail("UseItem");
                    if (useItemJob == null)
                    {
                        Log.Warning("[IFTTT] ApplySentienceCatalyst: no CompUsable and 'UseItem' JobDef missing.");
                        break;
                    }
                    Job job = JobMaker.MakeJob(useItemJob, item, target);
                    job.count = 1;
                    handler.jobs.StartJob(job, JobCondition.InterruptForced, null,
                                          resumeCurJobAfterwards: false);
                }

                usedItems.Add(item);
                usedHandlers.Add(handler);
                dispatched++;
            }

            if (dispatched > 0)
                Messages.Message(
                    $"[IFTTT] Dispatched {dispatched} colonist(s) to apply Sentience Catalyst to {AnimalLabel}.",
                    MessageTypeDefOf.NeutralEvent, historical: false);
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Target animal race:");
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
