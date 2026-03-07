using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// For every owned animal of the chosen race that hasn't yet received the Sentience Catalyst
    /// treatment, finds an available catalyst and dispatches its bonded master pawn to apply it.
    ///
    /// Master-first logic (Odyssey DLC):
    ///  - The handler MUST be the animal's bonded master (playerSettings.Master).
    ///  - If the animal has no master assigned: skip with a player warning.
    ///  - If the master exists but is not currently on this map: skip with a player warning.
    ///  - Jobs are QUEUED (not interrupt-forced) so the master finishes their current task first.
    /// </summary>
    public class Action_ApplySentienceCatalyst : AutomationAction
    {
        private static readonly string ItemDefName   = "SentienceCatalyst";
        private static readonly string HediffDefName = "SentienceCatalyst";

        public override string Label       => "Apply sentience catalyst to animal";
        public override string Description =>
            $"Queue master pawn to apply a Sentience Catalyst to untreated '{AnimalLabel}'.";

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

        public override bool Execute(Map map)
        {
            ThingDef  iDef    = DefDatabase<ThingDef>.GetNamedSilentFail(ItemDefName);
            ThingDef  raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(animalDef);
            HediffDef hDef    = DefDatabase<HediffDef>.GetNamedSilentFail(HediffDefName);

            if (iDef == null)
            {
                Log.Warning("[IFTTT] ApplySentienceCatalyst: 'SentienceCatalyst' ThingDef not found. " +
                            "Is the Odyssey DLC active?");
                return false;
            }
            if (raceDef == null)
            {
                Log.Warning($"[IFTTT] ApplySentienceCatalyst: animal def '{animalDef}' not found.");
                return false;
            }

            // All untreated, healthy, owned animals of the chosen race on this map.
            List<Pawn> targets = map.mapPawns.AllPawnsSpawned
                .Where(p =>
                    p.def == raceDef              &&
                    p.Faction == Faction.OfPlayer &&
                    !p.Dead && !p.Destroyed       &&
                    !p.Downed                     &&
                    (hDef == null || !p.health.hediffSet.HasHediff(hDef)))
                .ToList();

            if (targets.Count == 0) return false;

            // Pre-collect the catalyst job def once (from the item's CompUsable.Props).
            JobDef useJobDef = ResolveUseJobDef(map, iDef);
            if (useJobDef == null)
            {
                Log.Warning("[IFTTT] ApplySentienceCatalyst: cannot resolve a use-job for the catalyst item.");
                return false;
            }

            var usedItems = new HashSet<Thing>();
            int dispatched = 0;

            foreach (Pawn target in targets)
            {
                // ── 1. Resolve the handler: must be the bonded master ──────────────
                Pawn master = target.playerSettings?.Master;

                if (master == null)
                {
                    // No master assigned — cannot apply without a master; warn and skip.
                    Messages.Message(
                        $"[IFTTT] Sentience Catalyst: {target.LabelShort} has no assigned master. " +
                        "Assign a master pawn first.",
                        MessageTypeDefOf.CautionInput, historical: false);
                    continue;
                }

                if (master.Map != map || !master.Spawned || master.Dead || master.Downed)
                {
                    // Master exists but is not available on this map — warn and skip.
                    Messages.Message(
                        $"[IFTTT] Sentience Catalyst: {master.LabelShort} (master of {target.LabelShort}) " +
                        "is not present on this map.",
                        MessageTypeDefOf.CautionInput, historical: false);
                    continue;
                }

                // ── 2. Skip if master already has a catalyst job queued or in progress ──
                bool masterAlreadyHandling =
                    master.CurJob?.targetB.Pawn == target ||
                    master.CurJob?.targetA.Thing?.def == iDef;
                if (masterAlreadyHandling) continue;

                // ── 3. Find a catalyst on the map not already claimed this Execute() ──
                Thing item = map.listerThings.ThingsOfDef(iDef)
                    .FirstOrDefault(t =>
                        t.Spawned                        &&
                        !t.Destroyed                     &&
                        !t.IsForbidden(Faction.OfPlayer) &&
                        !usedItems.Contains(t));

                if (item == null)
                {
                    // Out of catalysts — stop dispatching further targets.
                    Messages.Message(
                        $"[IFTTT] Sentience Catalyst: no more catalysts available in the colony.",
                        MessageTypeDefOf.CautionInput, historical: false);
                    break;
                }

                // ── 4. Queue the job on the master pawn (do not interrupt current task) ──
                Job job = JobMaker.MakeJob(useJobDef, item, target);
                job.count = 1;
                master.jobs.jobQueue.EnqueueFirst(job, null);

                usedItems.Add(item);
                dispatched++;
            }

            if (dispatched > 0)
                Messages.Message(
                    $"[IFTTT] Queued {dispatched} Sentience Catalyst job(s) for master pawn(s).",
                    MessageTypeDefOf.NeutralEvent, historical: false);

            return dispatched > 0;
        }

        /// <summary>
        /// Resolves the JobDef to use for applying a catalyst.
        /// Prefers the job from the item's own CompUsable.Props (DLC-correct).
        /// Falls back to known job def names if CompUsable is unavailable.
        /// </summary>
        private static JobDef ResolveUseJobDef(Map map, ThingDef iDef)
        {
            // Try to get it from any spawned catalyst (most reliable)
            Thing sample = map.listerThings.ThingsOfDef(iDef).FirstOrDefault(t => t.Spawned);
            if (sample != null)
            {
                CompUsable comp = sample.TryGetComp<CompUsable>();
                JobDef fromComp = comp?.Props?.useJob;
                if (fromComp != null) return fromComp;
            }

            // Fallbacks by known names (Odyssey DLC)
            return DefDatabase<JobDef>.GetNamedSilentFail("ApplySentienceCatalyst")
                ?? DefDatabase<JobDef>.GetNamedSilentFail("UseItem");
        }

        // ── DrawConfig ───────────────────────────────────────────────────────

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

        // ── ExposeData ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref animalDef, "animalDef", "Thrumbo");
        }
    }
}
