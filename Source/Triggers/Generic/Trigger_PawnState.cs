using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// What pawn property to query.
    /// Behavior for each value is registered in PawnQueryRegistry — adding a new
    /// property type only requires a Register() call there, not a switch case here.
    /// </summary>
    public enum PawnPropertyType
    {
        // ── Core ────────────────────────────────────────────────────────────
        Hediff,         // has / missing a HediffDef
        NeedLevel,      // NeedDef (mood, food, rest…) ≥/≤ percent
        SkillLevel,     // SkillDef ≥/≤ level (0-20)
        Trait,          // has / missing a TraitDef
        Capacity,       // PawnCapacityDef (Moving, Manipulation…) ≥/≤ percent
        IsDrafted,      // boolean flag
        IsIdle,         // boolean flag
        IsDowned,       // boolean flag
        InMentalBreak,  // boolean flag
        Gender,         // male / female
        Age,            // biological age ≥/≤ years

        // ── Bio / identity ──────────────────────────────────────────────────
        Backstory,      // BackstoryDef, childhood/adulthood slot, has/missing
        Gene,           // GeneDef, has/missing [Biotech]
        Xenotype,       // XenotypeDef, is/isn't [Biotech]
        RoyalTitle,     // RoyalTitleDef, has/missing [Royalty]
        Relationship,   // PawnRelationDef, has/missing
        WorkDisabled,   // WorkTypeDef, is/isn't incapable
        EquippedWeapon, // ThingDef (weapon), equipped/not
        PawnName,       // text search on pawn name (contains)
        SkillPassion,   // SkillDef + passion level (None/Minor/Major)

        // ── Psycasts / abilities ────────────────────────────────────────────
        HasAbility,     // AbilityDef (any vanilla/VPE psycast or ability), has/missing
        PsylinkLevel,   // psylink level (0-6) ≥/≤ threshold
        Psyfocus,       // psyfocus % ≥/≤ threshold
        PsychicEntropy, // psychic entropy % of max ≥/≤ threshold
    }

    /// <summary>
    /// Universal pawn-state trigger.
    ///
    /// Fields hold the serialised configuration; all evaluation, description, and
    /// UI logic is delegated to <see cref="PawnQueryRegistry"/> so that adding new
    /// property types requires zero changes to this class.
    /// </summary>
    public class Trigger_PawnState : AutomationTrigger
    {
        // ── Fields ────────────────────────────────────────────────────────────

        public PawnPropertyType propertyType   = PawnPropertyType.Hediff;
        public string           defName        = "";       // reused: hediffDef / needDef / geneDef / ability / name-search text / …
        public bool             boolCondition  = true;    // semantics depend on property (missing/has, is/isn't, contains/doesn't)
        public float            threshold      = 0.5f;    // numeric comparison target; also passion level (0/1/2) for SkillPassion
        public CountComparator  comparator     = CountComparator.AtLeast;
        public PawnKindFilter   pawnKind       = PawnKindFilter.Colonist;
        public string           raceDefName    = "";
        public string           zoneLabel      = "";
        public bool             checkChildhood = true;    // Backstory: true = childhood slot, false = adulthood

        // ── AutomationTrigger overrides ───────────────────────────────────────

        public override string Label => "Pawn state";

        public override string Description
        {
            get
            {
                var q = PawnQueryRegistry.Get(propertyType);
                return q?.Describe(this) ?? "Pawn state";
            }
        }

        public override bool HasConfig => true;

        public override float ConfigHeight
        {
            get
            {
                float h = 80f; // property-type dropdown + pawn-kind row + zone row
                var q = PawnQueryRegistry.Get(propertyType);
                if (q != null) h += q.ExtraHeight(this);
                if (pawnKind == PawnKindFilter.Animal || pawnKind == PawnKindFilter.Any)
                    h += 48f; // race label + dropdown
                return h;
            }
        }

        // ── Trigger logic ─────────────────────────────────────────────────────

        public override bool IsTriggered(Map map)
        {
            var q = PawnQueryRegistry.Get(propertyType);
            if (q == null) return false;
            var pawns = PawnFilterHelper.GetPawns(map, pawnKind, zoneLabel, raceDefName)
                .Where(p => !p.Dead && p.Spawned);
            return pawns.Any(p => q.Evaluate(p, this));
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            var q = PawnQueryRegistry.Get(propertyType);

            // Property-type selector
            listing.Label("Property to check:");
            if (Widgets.ButtonText(listing.GetRect(28f), q?.MenuLabel ?? propertyType.ToString()))
                Find.WindowStack.Add(new FloatMenu(
                    PawnQueryRegistry.BuildPropertyMenu(v => { propertyType = v; defName = ""; })));

            // Property-specific controls (fully delegated)
            q?.DrawUI(this, listing);

            // Pawn filter (always shown)
            PawnFilterHelper.DrawKindFilter(pawnKind, v => pawnKind = v, listing);
            if (pawnKind == PawnKindFilter.Animal || pawnKind == PawnKindFilter.Any)
            {
                listing.Label("Race filter (optional):");
                PawnFilterHelper.DrawRaceDropdown(raceDefName, v => raceDefName = v, listing.GetRect(24f));
            }
            listing.Label("Zone (optional):");
            PawnFilterHelper.DrawZoneDropdown(zoneLabel, v => zoneLabel = v, listing.GetRect(24f));
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref propertyType,   "propertyType",   PawnPropertyType.Hediff);
            Scribe_Values.Look(ref defName,        "defName",        "");
            Scribe_Values.Look(ref boolCondition,  "boolCondition",  true);
            Scribe_Values.Look(ref threshold,      "threshold",      0.5f);
            Scribe_Values.Look(ref comparator,     "comparator",     CountComparator.AtLeast);
            Scribe_Values.Look(ref pawnKind,       "pawnKind",       PawnKindFilter.Colonist);
            Scribe_Values.Look(ref raceDefName,    "raceDefName",    "");
            Scribe_Values.Look(ref zoneLabel,      "zoneLabel",      "");
            Scribe_Values.Look(ref checkChildhood, "checkChildhood", true);
        }
    }
}
