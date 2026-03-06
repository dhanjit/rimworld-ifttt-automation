using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// What pawn property to query. Each value changes what UI controls
    /// and what evaluation logic is used.
    /// </summary>
    public enum PawnPropertyType
    {
        Hediff,         // has / missing a HediffDef
        NeedLevel,      // NeedDef (mood, food, rest, joy…) ≥/≤ percent
        SkillLevel,     // SkillDef ≥/≤ level (0-20)
        Trait,          // has / missing a TraitDef
        Capacity,       // PawnCapacityDef (Moving, Manipulation…) ≥/≤ percent
        IsDrafted,      // boolean flag
        IsIdle,         // boolean flag
        IsDowned,       // boolean flag
        InMentalBreak,  // boolean flag
        Gender,         // male / female
        Age,            // biological age ≥/≤ years
    }

    /// <summary>
    /// Universal pawn state trigger — can query any pawn property:
    /// hediffs, needs, skills, traits, capacities, and boolean state flags.
    ///
    /// Replaces the narrower Trigger_PawnCondition with a single configurable
    /// trigger that covers virtually every pawn attribute in the game,
    /// including modded hediffs, needs, skills, traits, and capacities.
    /// </summary>
    public class Trigger_PawnState : AutomationTrigger
    {
        public override string Label => "Pawn state";
        public override string Description
        {
            get
            {
                string kind = pawnKind.ToString().ToLower();
                string zone = zoneLabel.NullOrEmpty() ? "" : $" in '{zoneLabel}'";
                switch (propertyType)
                {
                    case PawnPropertyType.Hediff:
                        string hLabel = DefLabel<HediffDef>(defName);
                        return boolCondition
                            ? $"Any {kind}{zone} missing: {hLabel}"
                            : $"Any {kind}{zone} has: {hLabel}";
                    case PawnPropertyType.NeedLevel:
                        return $"Any {kind}{zone} {DefLabel<NeedDef>(defName)} {CompSym} {threshold:P0}";
                    case PawnPropertyType.SkillLevel:
                        return $"Any {kind}{zone} {DefLabel<SkillDef>(defName)} {CompSym} {(int)threshold}";
                    case PawnPropertyType.Trait:
                        string tLabel = GetTraitLabel(DefDatabase<TraitDef>.GetNamedSilentFail(defName));
                        return boolCondition
                            ? $"Any {kind}{zone} missing trait: {tLabel}"
                            : $"Any {kind}{zone} has trait: {tLabel}";
                    case PawnPropertyType.Capacity:
                        return $"Any {kind}{zone} {DefLabel<PawnCapacityDef>(defName)} {CompSym} {threshold:P0}";
                    case PawnPropertyType.IsDrafted:
                        return $"Any {kind}{zone} {(boolCondition ? "is drafted" : "is not drafted")}";
                    case PawnPropertyType.IsIdle:
                        return $"Any {kind}{zone} {(boolCondition ? "is idle" : "is not idle")}";
                    case PawnPropertyType.IsDowned:
                        return $"Any {kind}{zone} {(boolCondition ? "is downed" : "is not downed")}";
                    case PawnPropertyType.InMentalBreak:
                        return $"Any {kind}{zone} {(boolCondition ? "in mental break" : "not in mental break")}";
                    case PawnPropertyType.Gender:
                        return $"Any {kind}{zone} is {(boolCondition ? "female" : "male")}";
                    case PawnPropertyType.Age:
                        return $"Any {kind}{zone} age {CompSym} {(int)threshold} years";
                    default:
                        return "Pawn state";
                }
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight
        {
            get
            {
                float h = 80f; // property-type dropdown + pawn kind row + zone row
                switch (propertyType)
                {
                    case PawnPropertyType.Hediff:
                    case PawnPropertyType.Trait:
                        h += 70f; // def dropdown + has/missing toggle
                        break;
                    case PawnPropertyType.NeedLevel:
                    case PawnPropertyType.SkillLevel:
                    case PawnPropertyType.Capacity:
                        h += 90f; // def dropdown + comparator row + threshold
                        break;
                    case PawnPropertyType.IsDrafted:
                    case PawnPropertyType.IsIdle:
                    case PawnPropertyType.IsDowned:
                    case PawnPropertyType.InMentalBreak:
                    case PawnPropertyType.Gender:
                        h += 30f; // true/false toggle
                        break;
                    case PawnPropertyType.Age:
                        h += 60f; // comparator row + slider
                        break;
                }
                if (pawnKind == PawnKindFilter.Animal || pawnKind == PawnKindFilter.Any)
                    h += 48f; // race label + dropdown
                return h;
            }
        }

        // ── Fields ────────────────────────────────────────────────────────────

        public PawnPropertyType propertyType = PawnPropertyType.Hediff;
        public string           defName      = "";        // reused for hediff/need/skill/trait/capacity defName
        public bool             boolCondition = true;     // true = missing/has/is  (meaning depends on property)
        public float            threshold    = 0.5f;      // for numeric comparisons (0-1 for percent, 0-20 for skill)
        public CountComparator  comparator   = CountComparator.AtLeast;
        public PawnKindFilter   pawnKind     = PawnKindFilter.Colonist;
        public string           raceDefName  = "";
        public string           zoneLabel    = "";

        // ── Helpers ───────────────────────────────────────────────────────────

        private string CompSym => comparator == CountComparator.AtLeast ? "≥"
                                : comparator == CountComparator.AtMost  ? "≤" : "=";

        private static string DefLabel<T>(string dn) where T : Def
        {
            if (dn.NullOrEmpty()) return "???";
            T d = DefDatabase<T>.GetNamedSilentFail(dn);
            return d != null ? d.label.CapitalizeFirst() : dn;
        }

        private bool CompareNum(float value)
        {
            switch (comparator)
            {
                case CountComparator.AtLeast: return value >= threshold;
                case CountComparator.AtMost:  return value <= threshold;
                default:                      return Math.Abs(value - threshold) < 0.01f;
            }
        }

        // ── Trigger logic ─────────────────────────────────────────────────────

        public override bool IsTriggered(Map map)
        {
            var pawns = PawnFilterHelper.GetPawns(map, pawnKind, zoneLabel, raceDefName)
                .Where(p => !p.Dead && p.Spawned);

            switch (propertyType)
            {
                case PawnPropertyType.Hediff:
                {
                    HediffDef hDef = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
                    if (hDef == null) return false;
                    // boolCondition=true → "missing" (fire when pawn lacks hediff)
                    return pawns.Any(p => (p.health?.hediffSet?.HasHediff(hDef) == true) != boolCondition);
                }
                case PawnPropertyType.NeedLevel:
                {
                    NeedDef nDef = DefDatabase<NeedDef>.GetNamedSilentFail(defName);
                    if (nDef == null) return false;
                    return pawns.Any(p =>
                    {
                        Need n = p.needs?.TryGetNeed(nDef);
                        return n != null && CompareNum(n.CurLevelPercentage);
                    });
                }
                case PawnPropertyType.SkillLevel:
                {
                    SkillDef sDef = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
                    if (sDef == null) return false;
                    return pawns.Any(p =>
                    {
                        SkillRecord sr = p.skills?.GetSkill(sDef);
                        return sr != null && CompareNum(sr.Level);
                    });
                }
                case PawnPropertyType.Trait:
                {
                    TraitDef tDef = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
                    if (tDef == null) return false;
                    // boolCondition=true → "missing trait"
                    return pawns.Any(p => (p.story?.traits?.HasTrait(tDef) == true) != boolCondition);
                }
                case PawnPropertyType.Capacity:
                {
                    PawnCapacityDef cDef = DefDatabase<PawnCapacityDef>.GetNamedSilentFail(defName);
                    if (cDef == null) return false;
                    return pawns.Any(p =>
                    {
                        if (p.health?.capacities == null) return false;
                        return CompareNum(p.health.capacities.GetLevel(cDef));
                    });
                }
                case PawnPropertyType.IsDrafted:
                    return pawns.Any(p => (p.Drafted) == boolCondition);
                case PawnPropertyType.IsIdle:
                    return pawns.Any(p => (p.CurJob == null) == boolCondition);
                case PawnPropertyType.IsDowned:
                    return pawns.Any(p => (p.Downed) == boolCondition);
                case PawnPropertyType.InMentalBreak:
                    return pawns.Any(p => (p.InMentalState) == boolCondition);
                case PawnPropertyType.Gender:
                    return pawns.Any(p => (p.gender == Gender.Female) == boolCondition);
                case PawnPropertyType.Age:
                    return pawns.Any(p => CompareNum(p.ageTracker?.AgeBiologicalYearsFloat ?? 0f));
                default:
                    return false;
            }
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            // Property type selector
            listing.Label("Property to check:");
            if (Widgets.ButtonText(listing.GetRect(28f), PropertyLabel(propertyType)))
                Find.WindowStack.Add(new FloatMenu(BuildPropertyMenu()));

            // Property-specific controls
            switch (propertyType)
            {
                case PawnPropertyType.Hediff:
                    DrawDefDropdown<HediffDef>(listing, "Hediff:");
                    DrawBoolToggle(listing, "Missing", "Has");
                    break;
                case PawnPropertyType.NeedLevel:
                    DrawDefDropdown<NeedDef>(listing, "Need:");
                    DrawComparator(listing);
                    DrawThresholdSlider(listing, 0f, 1f, $"Threshold: {threshold:P0}");
                    break;
                case PawnPropertyType.SkillLevel:
                    DrawDefDropdown<SkillDef>(listing, "Skill:");
                    DrawComparator(listing);
                    DrawThresholdSlider(listing, 0f, 20f, $"Level: {(int)threshold}");
                    break;
                case PawnPropertyType.Trait:
                    DrawTraitDropdown(listing);
                    DrawBoolToggle(listing, "Missing", "Has");
                    break;
                case PawnPropertyType.Capacity:
                    DrawDefDropdown<PawnCapacityDef>(listing, "Capacity:");
                    DrawComparator(listing);
                    DrawThresholdSlider(listing, 0f, 1.5f, $"Threshold: {threshold:P0}");
                    break;
                case PawnPropertyType.Gender:
                    DrawBoolToggle(listing, "Female", "Male");
                    break;
                case PawnPropertyType.Age:
                    DrawComparator(listing);
                    DrawThresholdSlider(listing, 0f, 120f, $"Age: {(int)threshold} years");
                    break;
                default: // boolean flags (IsDrafted, IsIdle, IsDowned, InMentalBreak)
                    DrawBoolToggle(listing, "Is true", "Is false");
                    break;
            }

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

        // ── UI building blocks ────────────────────────────────────────────────

        private void DrawDefDropdown<T>(Listing_Standard listing, string label) where T : Def
        {
            listing.Label(label);
            T cur    = DefDatabase<T>.GetNamedSilentFail(defName);
            string btn = cur != null ? cur.label.CapitalizeFirst()
                : (defName.NullOrEmpty() ? "(select)" : $"(unknown: {defName})");
            if (Widgets.ButtonText(listing.GetRect(24f), btn))
            {
                var opts = new List<FloatMenuOption>();
                foreach (T d in DefDatabase<T>.AllDefsListForReading
                    .Where(d => !d.label.NullOrEmpty())
                    .OrderBy(d => d.label))
                {
                    string dn = d.defName;
                    opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(), () => defName = dn));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        /// <summary>
        /// TraitDef labels live on degreeDatas, not on Def.label.
        /// This dropdown handles that correctly.
        /// </summary>
        private void DrawTraitDropdown(Listing_Standard listing)
        {
            listing.Label("Trait:");
            TraitDef cur = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            string btnLabel = cur != null ? GetTraitLabel(cur)
                : (defName.NullOrEmpty() ? "(select)" : $"(unknown: {defName})");
            if (Widgets.ButtonText(listing.GetRect(24f), btnLabel))
            {
                var opts = new List<FloatMenuOption>();
                foreach (TraitDef td in DefDatabase<TraitDef>.AllDefsListForReading
                    .OrderBy(td => GetTraitLabel(td)))
                {
                    string dn  = td.defName;
                    string lbl = GetTraitLabel(td);
                    opts.Add(new FloatMenuOption(lbl, () => defName = dn));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        private static string GetTraitLabel(TraitDef td)
        {
            if (td == null) return "(unknown)";
            if (td.degreeDatas?.Count > 0)
            {
                var labels = td.degreeDatas
                    .Where(d => !d.label.NullOrEmpty())
                    .Select(d => d.label.CapitalizeFirst());
                string joined = string.Join(" / ", labels);
                if (!joined.NullOrEmpty()) return joined;
            }
            return td.label?.CapitalizeFirst() ?? td.defName;
        }

        private void DrawBoolToggle(Listing_Standard listing, string trueLabel, string falseLabel)
        {
            Rect row  = listing.GetRect(24f);
            float hw  = row.width / 2f;
            bool cur  = boolCondition;
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x,      row.y, hw, 24f), trueLabel,  cur,  () => boolCondition = true);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + hw, row.y, hw, 24f), falseLabel, !cur, () => boolCondition = false);
        }

        private void DrawComparator(Listing_Standard listing)
        {
            Rect row = listing.GetRect(24f);
            float w   = row.width / 3f;
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x,         row.y, w, 24f), "≥ At least", comparator == CountComparator.AtLeast, () => comparator = CountComparator.AtLeast);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w,     row.y, w, 24f), "≤ At most",  comparator == CountComparator.AtMost,  () => comparator = CountComparator.AtMost);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w * 2, row.y, w, 24f), "= Exactly",  comparator == CountComparator.Exactly, () => comparator = CountComparator.Exactly);
        }

        private void DrawThresholdSlider(Listing_Standard listing, float min, float max, string label)
        {
            threshold = Widgets.HorizontalSlider(listing.GetRect(28f), threshold, min, max, false, label);
        }

        // ── Menus ─────────────────────────────────────────────────────────────

        private static string PropertyLabel(PawnPropertyType pt)
        {
            switch (pt)
            {
                case PawnPropertyType.Hediff:        return "Hediff (has / missing)";
                case PawnPropertyType.NeedLevel:     return "Need level (mood, food…)";
                case PawnPropertyType.SkillLevel:    return "Skill level";
                case PawnPropertyType.Trait:         return "Trait (has / missing)";
                case PawnPropertyType.Capacity:      return "Capacity (moving, sight…)";
                case PawnPropertyType.IsDrafted:     return "Is drafted";
                case PawnPropertyType.IsIdle:        return "Is idle";
                case PawnPropertyType.IsDowned:      return "Is downed";
                case PawnPropertyType.InMentalBreak: return "In mental break";
                case PawnPropertyType.Gender:       return "Gender (male / female)";
                case PawnPropertyType.Age:          return "Age (biological years)";
                default: return pt.ToString();
            }
        }

        private List<FloatMenuOption> BuildPropertyMenu()
        {
            var opts = new List<FloatMenuOption>();
            foreach (PawnPropertyType pt in Enum.GetValues(typeof(PawnPropertyType)))
            {
                PawnPropertyType cap = pt;
                opts.Add(new FloatMenuOption(PropertyLabel(cap), () =>
                {
                    propertyType = cap;
                    defName = "";          // reset def when switching property
                }));
            }
            return opts;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref propertyType,  "propertyType",  PawnPropertyType.Hediff);
            Scribe_Values.Look(ref defName,        "defName",        "");
            Scribe_Values.Look(ref boolCondition,  "boolCondition",  true);
            Scribe_Values.Look(ref threshold,      "threshold",      0.5f);
            Scribe_Values.Look(ref comparator,     "comparator",     CountComparator.AtLeast);
            Scribe_Values.Look(ref pawnKind,       "pawnKind",       PawnKindFilter.Colonist);
            Scribe_Values.Look(ref raceDefName,    "raceDefName",    "");
            Scribe_Values.Look(ref zoneLabel,      "zoneLabel",      "");
        }
    }
}
