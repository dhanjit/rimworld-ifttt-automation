using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Generic action: dispatches a colonist to use any item that has
    /// CompUsable on a set of target pawns.
    ///
    /// Works with any mod's usable items — skill trainers, sentience catalysts,
    /// medicine injectors, psyfocus consumables, etc.
    ///
    /// Optional filters: target pawn kind, animal race, area zone,
    /// and "only target pawns missing hediff X" (avoid wasting items).
    /// </summary>
    public class Action_UseItemOnPawn : AutomationAction
    {
        public override string Label => "Use item on pawn";
        public override string Description
        {
            get
            {
                ThingDef d    = DefDatabase<ThingDef>.GetNamedSilentFail(itemDefName);
                string iLabel = d != null ? d.label.CapitalizeFirst() : itemDefName;
                return $"Use {iLabel} on {targetKind.ToString().ToLower()}";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 210f;

        public string         itemDefName          = "";
        public PawnKindFilter targetKind           = PawnKindFilter.Colonist;
        public string         raceDefName          = "";  // optional animal race filter
        public string         zoneLabel            = "";  // optional area filter
        public string         requiredMissingHediff = ""; // optional: skip targets that already have this hediff

        // ── Execute ───────────────────────────────────────────────────────────

        public override void Execute(Map map)
        {
            ThingDef itemDef = DefDatabase<ThingDef>.GetNamedSilentFail(itemDefName);
            if (itemDef == null) return;

            HediffDef hediffFilter = requiredMissingHediff.NullOrEmpty() ? null
                : DefDatabase<HediffDef>.GetNamedSilentFail(requiredMissingHediff);

            var targets = PawnFilterHelper.GetPawns(map, targetKind, zoneLabel, raceDefName)
                .Where(p => !p.Dead && p.Spawned)
                .Where(p => hediffFilter == null || !p.health.hediffSet.HasHediff(hediffFilter))
                .ToList();

            if (targets.Count == 0) return;

            var usedItems    = new HashSet<Thing>();
            var usedHandlers = new HashSet<Pawn>();

            foreach (Pawn target in targets)
            {
                if (AlreadyBeingHandled(map, target, itemDef)) continue;

                Thing item = FindItem(map, itemDef, usedItems);
                if (item == null) break; // out of items

                Pawn handler = FindHandler(map, usedHandlers);
                if (handler == null) break; // no free colonists

                bool dispatched = false;

                // Try CompUsable first (handles complex targeting like Odyssey catalysts)
                // TryStartUseJob starts the job internally and returns void in RimWorld 1.6
                CompUsable comp = item.TryGetComp<CompUsable>();
                if (comp != null)
                {
                    try
                    {
                        comp.TryStartUseJob(handler, new LocalTargetInfo(target));
                        dispatched = true;
                    }
                    catch (Exception) { /* expected — fall through to UseNeurotrainer */ }
                }

                // Fallback: UseNeurotrainer job (works for neurotrainers + similar items)
                if (!dispatched)
                {
                    try
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.UseNeurotrainer, item, target);
                        handler.jobs.StartJob(job, JobCondition.InterruptForced,
                            null, resumeCurJobAfterwards: false);
                        dispatched = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[IFTTT] UseItemOnPawn: both dispatch paths failed for" +
                            $" {item.def.label} on {handler.LabelShort}: {ex.Message}");
                    }
                }

                if (dispatched)
                {
                    usedItems.Add(item);
                    usedHandlers.Add(handler);
                }
            }
        }

        private static bool AlreadyBeingHandled(Map map, Pawn target, ThingDef itemDef)
        {
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
            {
                Job j = p.CurJob;
                if (j != null && j.targetB.Pawn == target && j.targetA.Thing?.def == itemDef)
                    return true;
            }
            return false;
        }

        private static Thing FindItem(Map map, ThingDef def, HashSet<Thing> exclude) =>
            map.listerThings.ThingsOfDef(def)
               .FirstOrDefault(t => t.Spawned && !t.Destroyed
                    && !t.IsForbidden(Faction.OfPlayer)
                    && !exclude.Contains(t));

        private static Pawn FindHandler(Map map, HashSet<Pawn> exclude) =>
            map.mapPawns.FreeColonistsSpawned
               .FirstOrDefault(p => !p.Dead && !p.Downed
                    && !p.InMentalState
                    && !exclude.Contains(p));

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Item (must have CompUsable):");
            ThingDef curItem = DefDatabase<ThingDef>.GetNamedSilentFail(itemDefName);
            string itemBtn = curItem != null ? curItem.label.CapitalizeFirst()
                : (itemDefName.NullOrEmpty() ? "(select item)" : $"(unknown: {itemDefName})");
            if (Widgets.ButtonText(listing.GetRect(28f), itemBtn))
                Find.WindowStack.Add(new FloatMenu(BuildItemMenu()));

            PawnFilterHelper.DrawKindFilter(targetKind, v => targetKind = v, listing);

            if (targetKind == PawnKindFilter.Animal || targetKind == PawnKindFilter.Any)
            {
                listing.Label("Race filter (optional):");
                PawnFilterHelper.DrawRaceDropdown(raceDefName, v => raceDefName = v, listing.GetRect(24f));
            }

            listing.Label("Zone filter (optional):");
            PawnFilterHelper.DrawZoneDropdown(zoneLabel, v => zoneLabel = v, listing.GetRect(24f));

            listing.Label("Skip targets that already have hediff (optional):");
            PawnFilterHelper.DrawHediffDropdown(requiredMissingHediff, v => requiredMissingHediff = v, listing.GetRect(24f));
        }

        private List<FloatMenuOption> BuildItemMenu()
        {
            var opts = new List<FloatMenuOption>();
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.category == ThingCategory.Item && d.HasComp(typeof(CompUsable)))
                .OrderBy(d => d.label))
            {
                ThingDef cap = d;
                opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(), () => itemDefName = cap.defName));
            }
            return opts;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref itemDefName,           "itemDefName",           "");
            Scribe_Values.Look(ref targetKind,            "targetKind",            PawnKindFilter.Colonist);
            Scribe_Values.Look(ref raceDefName,           "raceDefName",           "");
            Scribe_Values.Look(ref zoneLabel,             "zoneLabel",             "");
            Scribe_Values.Look(ref requiredMissingHediff, "requiredMissingHediff", "");
        }
    }
}
