using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Generic action: finds a colonist who has a given AbilityDef and casts it
    /// on themselves (self-buff) or on filtered target pawns in a zone.
    ///
    /// Works with vanilla psycasts and Vanilla Psycasts Expanded — both register
    /// their abilities in DefDatabase&lt;AbilityDef&gt; and on pawn.abilities in 1.6.
    ///
    /// Optional hedge filter: "only cast on targets that don't already have buff X"
    /// prevents wasting psyfocus when the buff is already active.
    /// </summary>
    public class Action_CastAbility : AutomationAction
    {
        public override string Label => "Cast ability";
        public override string Description
        {
            get
            {
                AbilityDef ad  = DefDatabase<AbilityDef>.GetNamedSilentFail(abilityDefName);
                string aLabel  = ad != null ? ad.label.CapitalizeFirst() : abilityDefName;
                return selfCast
                    ? $"Self-cast: {aLabel}"
                    : $"Cast {aLabel} on {targetKind.ToString().ToLower()}" +
                      (zoneLabel.NullOrEmpty() ? "" : $" in '{zoneLabel}'");
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 215f;

        public string         abilityDefName        = "";
        public bool           selfCast              = true;
        public PawnKindFilter targetKind            = PawnKindFilter.Colonist;
        public string         zoneLabel             = "";
        public string         requiredMissingHediff = ""; // only cast on targets lacking this hediff

        // ── Execute ───────────────────────────────────────────────────────────

        public override void Execute(Map map)
        {
            AbilityDef aDef = DefDatabase<AbilityDef>.GetNamedSilentFail(abilityDefName);
            if (aDef == null) return;

            HediffDef hediffFilter = requiredMissingHediff.NullOrEmpty() ? null
                : DefDatabase<HediffDef>.GetNamedSilentFail(requiredMissingHediff);

            if (selfCast)
            {
                // Every colonist who has the ability, isn't on cooldown, and (if filtered) lacks the hediff
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Dead && !p.Downed && !p.InMentalState))
                {
                    if (hediffFilter != null && pawn.health.hediffSet.HasHediff(hediffFilter))
                        continue;

                    TryCast(pawn, pawn, aDef);
                }
            }
            else
            {
                var usedCasters = new HashSet<Pawn>();

                IEnumerable<Pawn> targets = PawnFilterHelper.GetPawns(map, targetKind, zoneLabel)
                    .Where(p => !p.Dead && p.Spawned)
                    .Where(p => hediffFilter == null || !p.health.hediffSet.HasHediff(hediffFilter));

                foreach (Pawn target in targets)
                {
                    Pawn caster = FindCaster(map, aDef, usedCasters);
                    if (caster == null) break; // no eligible casters remain

                    if (TryCast(caster, target, aDef))
                        usedCasters.Add(caster);
                }
            }
        }

        private static Pawn FindCaster(Map map, AbilityDef aDef, HashSet<Pawn> exclude)
        {
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.Dead || p.Downed || !p.Spawned || p.InMentalState) continue;
                if (exclude.Contains(p)) continue;
                if (p.abilities?.GetAbility(aDef) != null)
                    return p;
            }
            return null;
        }

        private static bool TryCast(Pawn caster, Pawn target, AbilityDef aDef)
        {
            try
            {
                Ability ab = caster.abilities?.GetAbility(aDef);
                if (ab == null) return false;
                // Activate returns false if on cooldown or otherwise unable to cast
                return ab.Activate(new LocalTargetInfo(target), LocalTargetInfo.Invalid);
            }
            catch (Exception ex)
            {
                Log.Warning($"[IFTTT] CastAbility failed {caster.LabelShort} → {target.LabelShort}: {ex.Message}");
                return false;
            }
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Ability:");
            AbilityDef curAb = DefDatabase<AbilityDef>.GetNamedSilentFail(abilityDefName);
            string abBtn = curAb != null ? curAb.label.CapitalizeFirst()
                : (abilityDefName.NullOrEmpty() ? "(select ability)" : $"(unknown: {abilityDefName})");
            if (Widgets.ButtonText(listing.GetRect(28f), abBtn))
                Find.WindowStack.Add(new FloatMenu(BuildAbilityMenu()));

            listing.Gap(2f);

            // Self-cast / Cast on other toggle
            Rect castRow  = listing.GetRect(24f);
            float hw      = castRow.width / 2f;
            bool isSelf   = selfCast;
            PawnFilterHelper.DrawToggleBtn(new Rect(castRow.x,      castRow.y, hw, 24f), "Self-cast",      isSelf,  () => selfCast = true);
            PawnFilterHelper.DrawToggleBtn(new Rect(castRow.x + hw, castRow.y, hw, 24f), "Cast on others", !isSelf, () => selfCast = false);

            if (!selfCast)
            {
                PawnFilterHelper.DrawKindFilter(targetKind, v => targetKind = v, listing);
                listing.Label("Zone filter (optional):");
                PawnFilterHelper.DrawZoneDropdown(zoneLabel, v => zoneLabel = v, listing.GetRect(24f));
            }

            listing.Label("Skip targets that already have hediff (optional):");
            PawnFilterHelper.DrawHediffDropdown(requiredMissingHediff, v => requiredMissingHediff = v, listing.GetRect(24f));
        }

        private List<FloatMenuOption> BuildAbilityMenu()
        {
            var opts = new List<FloatMenuOption>();
            foreach (AbilityDef d in DefDatabase<AbilityDef>.AllDefsListForReading
                .Where(d => !d.label.NullOrEmpty())
                .OrderBy(d => d.label))
            {
                AbilityDef cap = d;
                opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(), () => abilityDefName = cap.defName));
            }
            return opts;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref abilityDefName,        "abilityDefName",        "");
            Scribe_Values.Look(ref selfCast,              "selfCast",              true);
            Scribe_Values.Look(ref targetKind,            "targetKind",            PawnKindFilter.Colonist);
            Scribe_Values.Look(ref zoneLabel,             "zoneLabel",             "");
            Scribe_Values.Look(ref requiredMissingHediff, "requiredMissingHediff", "");
        }
    }
}
