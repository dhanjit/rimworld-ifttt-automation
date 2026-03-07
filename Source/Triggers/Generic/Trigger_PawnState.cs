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
        // ── Core properties ──────────────────────────────────────────────────
        Hediff,         // has / missing a HediffDef
        NeedLevel,      // NeedDef (mood, food, rest, joy...) >=/<= percent
        SkillLevel,     // SkillDef >=/<= level (0-20)
        Trait,          // has / missing a TraitDef
        Capacity,       // PawnCapacityDef (Moving, Manipulation...) >=/<= percent
        IsDrafted,      // boolean flag
        IsIdle,         // boolean flag
        IsDowned,       // boolean flag
        InMentalBreak,  // boolean flag
        Gender,         // male / female
        Age,            // biological age >=/<= years

        // ── Bio / identity properties ────────────────────────────────────────
        Backstory,      // BackstoryDef, childhood/adulthood toggle, has/missing
        Gene,           // GeneDef, has/missing (Biotech DLC)
        Xenotype,       // XenotypeDef, is/isn't (Biotech DLC)
        RoyalTitle,     // RoyalTitleDef, has/missing (Royalty DLC)
        Relationship,   // PawnRelationDef, has/missing
        WorkDisabled,   // WorkTypeDef, is/isn't incapable
        EquippedWeapon, // ThingDef (weapon), has/missing
        PawnName,       // text search on pawn name (contains)
        SkillPassion,   // SkillDef + passion level (None/Minor/Major)
    }

    /// <summary>
    /// Universal pawn state trigger — can query any pawn property:
    /// hediffs, needs, skills, traits, capacities, boolean state flags,
    /// backstories, genes, xenotypes, royal titles, relationships,
    /// work type capabilities, equipped weapons, names, and skill passions.
    ///
    /// Replaces the narrower Trigger_PawnCondition with a single configurable
    /// trigger that covers virtually every pawn attribute in the game,
    /// including modded defs via DefDatabase dropdowns.
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
                    case PawnPropertyType.Backstory:
                    {
                        string slot = checkChildhood ? "childhood" : "adulthood";
                        string bLabel = BackstoryLabel(defName);
                        return boolCondition
                            ? $"Any {kind}{zone} missing {slot}: {bLabel}"
                            : $"Any {kind}{zone} has {slot}: {bLabel}";
                    }
                    case PawnPropertyType.Gene:
                    {
                        string gLabel = DefLabel<GeneDef>(defName);
                        return boolCondition
                            ? $"Any {kind}{zone} missing gene: {gLabel}"
                            : $"Any {kind}{zone} has gene: {gLabel}";
                    }
                    case PawnPropertyType.Xenotype:
                    {
                        string xLabel = DefLabel<XenotypeDef>(defName);
                        return boolCondition
                            ? $"Any {kind}{zone} is NOT xenotype: {xLabel}"
                            : $"Any {kind}{zone} is xenotype: {xLabel}";
                    }
                    case PawnPropertyType.RoyalTitle:
                    {
                        string rLabel = DefLabel<RoyalTitleDef>(defName);
                        return boolCondition
                            ? $"Any {kind}{zone} missing title: {rLabel}"
                            : $"Any {kind}{zone} has title: {rLabel}";
                    }
                    case PawnPropertyType.Relationship:
                    {
                        string relLabel = DefLabel<PawnRelationDef>(defName);
                        return boolCondition
                            ? $"Any {kind}{zone} has no {relLabel}"
                            : $"Any {kind}{zone} has {relLabel}";
                    }
                    case PawnPropertyType.WorkDisabled:
                    {
                        string wLabel = DefLabel<WorkTypeDef>(defName);
                        return boolCondition
                            ? $"Any {kind}{zone} incapable of: {wLabel}"
                            : $"Any {kind}{zone} capable of: {wLabel}";
                    }
                    case PawnPropertyType.EquippedWeapon:
                    {
                        string eqLabel = DefLabel<ThingDef>(defName);
                        return boolCondition
                            ? $"Any {kind}{zone} NOT equipped: {eqLabel}"
                            : $"Any {kind}{zone} equipped with: {eqLabel}";
                    }
                    case PawnPropertyType.PawnName:
                    {
                        string search = defName.NullOrEmpty() ? "???" : defName;
                        return boolCondition
                            ? $"Any {kind}{zone} name contains: \"{search}\""
                            : $"Any {kind}{zone} name does NOT contain: \"{search}\"";
                    }
                    case PawnPropertyType.SkillPassion:
                    {
                        string sLabel = DefLabel<SkillDef>(defName);
                        string pLabel = PassionLabel((int)threshold);
                        return $"Any {kind}{zone} {sLabel} passion is {pLabel}";
                    }
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
                    case PawnPropertyType.Gene:
                    case PawnPropertyType.Xenotype:
                    case PawnPropertyType.RoyalTitle:
                    case PawnPropertyType.Relationship:
                    case PawnPropertyType.WorkDisabled:
                    case PawnPropertyType.EquippedWeapon:
                        h += 70f; // def dropdown + has/missing toggle
                        break;
                    case PawnPropertyType.Backstory:
                        h += 100f; // childhood/adulthood toggle + def dropdown + has/missing toggle
                        break;
                    case PawnPropertyType.NeedLevel:
                    case PawnPropertyType.SkillLevel:
                    case PawnPropertyType.Capacity:
                        h += 90f; // def dropdown + comparator row + threshold
                        break;
                    case PawnPropertyType.SkillPassion:
                        h += 70f; // skill dropdown + passion toggle row
                        break;
                    case PawnPropertyType.PawnName:
                        h += 60f; // text field + has/missing toggle
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
        public string           defName      = "";        // reused for all def-based lookups + name search text
        public bool             boolCondition = true;     // true = missing/has/is  (meaning depends on property)
        public float            threshold    = 0.5f;      // for numeric comparisons; also passion level (0/1/2) for SkillPassion
        public CountComparator  comparator   = CountComparator.AtLeast;
        public PawnKindFilter   pawnKind     = PawnKindFilter.Colonist;
        public string           raceDefName  = "";
        public string           zoneLabel    = "";
        public bool             checkChildhood = true;    // for Backstory: true = childhood, false = adulthood

        // ── Helpers ───────────────────────────────────────────────────────────

        private string CompSym => comparator == CountComparator.AtLeast ? "\u2265"
                                : comparator == CountComparator.AtMost  ? "\u2264" : "=";

        private static string DefLabel<T>(string dn) where T : Def
        {
            if (dn.NullOrEmpty()) return "???";
            T d = DefDatabase<T>.GetNamedSilentFail(dn);
            return d != null ? (d.label?.CapitalizeFirst() ?? d.defName) : dn;
        }

        /// <summary>
        /// Backstory labels use .title rather than .label in some versions.
        /// Falls back to defName if neither is available.
        /// </summary>
        private static string BackstoryLabel(string dn)
        {
            if (dn.NullOrEmpty()) return "???";
            BackstoryDef d = DefDatabase<BackstoryDef>.GetNamedSilentFail(dn);
            if (d == null) return dn;
            if (!d.title.NullOrEmpty()) return d.title.CapitalizeFirst();
            if (!d.label.NullOrEmpty()) return d.label.CapitalizeFirst();
            return d.defName;
        }

        private static string PassionLabel(int level)
        {
            switch (level)
            {
                case 0:  return "None";
                case 1:  return "Minor";
                case 2:  return "Major";
                default: return "???";
            }
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

                // ── Bio / identity properties ────────────────────────────────────
                case PawnPropertyType.Backstory:
                {
                    BackstoryDef bDef = DefDatabase<BackstoryDef>.GetNamedSilentFail(defName);
                    if (bDef == null) return false;
                    return pawns.Any(p =>
                    {
                        BackstoryDef pawnBs = checkChildhood ? p.story?.Childhood : p.story?.Adulthood;
                        bool has = pawnBs?.defName == bDef.defName;
                        return has != boolCondition;
                    });
                }
                case PawnPropertyType.Gene:
                {
                    GeneDef gDef = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
                    if (gDef == null) return false;
                    return pawns.Any(p =>
                    {
                        bool has = p.genes?.HasActiveGene(gDef) == true;
                        return has != boolCondition;
                    });
                }
                case PawnPropertyType.Xenotype:
                {
                    XenotypeDef xDef = DefDatabase<XenotypeDef>.GetNamedSilentFail(defName);
                    if (xDef == null) return false;
                    return pawns.Any(p =>
                    {
                        bool matches = p.genes?.Xenotype?.defName == xDef.defName;
                        return matches != boolCondition;
                    });
                }
                case PawnPropertyType.RoyalTitle:
                {
                    RoyalTitleDef rtDef = DefDatabase<RoyalTitleDef>.GetNamedSilentFail(defName);
                    if (rtDef == null) return false;
                    return pawns.Any(p =>
                    {
                        bool has = p.royalty?.AllTitlesInEffectForReading?
                            .Any(t => t.def == rtDef) == true;
                        return has != boolCondition;
                    });
                }
                case PawnPropertyType.Relationship:
                {
                    PawnRelationDef relDef = DefDatabase<PawnRelationDef>.GetNamedSilentFail(defName);
                    if (relDef == null) return false;
                    return pawns.Any(p =>
                    {
                        bool has = p.relations?.DirectRelations?
                            .Any(r => r.def == relDef) == true;
                        return has != boolCondition;
                    });
                }
                case PawnPropertyType.WorkDisabled:
                {
                    WorkTypeDef wDef = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
                    if (wDef == null) return false;
                    return pawns.Any(p =>
                    {
                        bool disabled = p.WorkTypeIsDisabled(wDef);
                        return disabled == boolCondition;
                    });
                }
                case PawnPropertyType.EquippedWeapon:
                {
                    ThingDef eqDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                    if (eqDef == null) return false;
                    return pawns.Any(p =>
                    {
                        bool has = p.equipment?.Primary?.def == eqDef;
                        return has != boolCondition;
                    });
                }
                case PawnPropertyType.PawnName:
                {
                    if (defName.NullOrEmpty()) return false;
                    string search = defName.ToLower();
                    return pawns.Any(p =>
                    {
                        string fullName = p.Name?.ToStringFull?.ToLower() ?? "";
                        bool contains = fullName.Contains(search);
                        // boolCondition=true -> "name contains", false -> "name does NOT contain"
                        return contains == boolCondition;
                    });
                }
                case PawnPropertyType.SkillPassion:
                {
                    SkillDef sDef = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
                    if (sDef == null) return false;
                    Passion target = (Passion)(int)threshold;
                    return pawns.Any(p =>
                    {
                        SkillRecord sr = p.skills?.GetSkill(sDef);
                        return sr != null && sr.passion == target;
                    });
                }

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

                // ── Bio / identity controls ──────────────────────────────────────
                case PawnPropertyType.Backstory:
                    DrawBackstorySlotToggle(listing);
                    DrawBackstoryDropdown(listing);
                    DrawBoolToggle(listing, "Missing", "Has");
                    break;
                case PawnPropertyType.Gene:
                    DrawGeneDropdown(listing);
                    DrawBoolToggle(listing, "Missing", "Has");
                    break;
                case PawnPropertyType.Xenotype:
                    DrawDefDropdown<XenotypeDef>(listing, "Xenotype:");
                    DrawBoolToggle(listing, "Is NOT", "Is");
                    break;
                case PawnPropertyType.RoyalTitle:
                    DrawDefDropdown<RoyalTitleDef>(listing, "Royal title:");
                    DrawBoolToggle(listing, "Missing", "Has");
                    break;
                case PawnPropertyType.Relationship:
                    DrawRelationDropdown(listing);
                    DrawBoolToggle(listing, "Has none", "Has");
                    break;
                case PawnPropertyType.WorkDisabled:
                    DrawDefDropdown<WorkTypeDef>(listing, "Work type:");
                    DrawBoolToggle(listing, "Incapable", "Capable");
                    break;
                case PawnPropertyType.EquippedWeapon:
                    DrawWeaponDropdown(listing);
                    DrawBoolToggle(listing, "NOT equipped", "Equipped");
                    break;
                case PawnPropertyType.PawnName:
                    listing.Label("Name contains (case-insensitive):");
                    defName = listing.TextEntry(defName);
                    DrawBoolToggle(listing, "Contains", "Does NOT contain");
                    break;
                case PawnPropertyType.SkillPassion:
                    DrawDefDropdown<SkillDef>(listing, "Skill:");
                    DrawPassionToggle(listing);
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
            string btn = cur != null ? (cur.label?.CapitalizeFirst() ?? cur.defName)
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

        /// <summary>Childhood / Adulthood toggle for Backstory property.</summary>
        private void DrawBackstorySlotToggle(Listing_Standard listing)
        {
            Rect row = listing.GetRect(24f);
            float hw = row.width / 2f;
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x,      row.y, hw, 24f), "Childhood",  checkChildhood,  () => { checkChildhood = true;  defName = ""; });
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + hw, row.y, hw, 24f), "Adulthood",  !checkChildhood, () => { checkChildhood = false; defName = ""; });
        }

        /// <summary>
        /// Backstory dropdown using .title for display since BackstoryDef
        /// may not populate .label the same way as other Defs.
        /// </summary>
        private void DrawBackstoryDropdown(Listing_Standard listing)
        {
            listing.Label(checkChildhood ? "Childhood backstory:" : "Adulthood backstory:");
            string btnLabel = BackstoryLabel(defName);
            if (defName.NullOrEmpty()) btnLabel = "(select)";
            if (Widgets.ButtonText(listing.GetRect(24f), btnLabel))
            {
                var opts = new List<FloatMenuOption>();
                foreach (BackstoryDef bd in DefDatabase<BackstoryDef>.AllDefsListForReading
                    .OrderBy(bd => bd.title ?? bd.label ?? bd.defName))
                {
                    string dn  = bd.defName;
                    string lbl = !bd.title.NullOrEmpty() ? bd.title.CapitalizeFirst()
                               : !bd.label.NullOrEmpty() ? bd.label.CapitalizeFirst()
                               : bd.defName;
                    opts.Add(new FloatMenuOption(lbl, () => defName = dn));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        /// <summary>
        /// Gene dropdown — uses GeneDef.label. Handles null labels gracefully.
        /// </summary>
        private void DrawGeneDropdown(Listing_Standard listing)
        {
            listing.Label("Gene:");
            GeneDef cur = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
            string btn = cur != null ? (cur.label?.CapitalizeFirst() ?? cur.defName)
                : (defName.NullOrEmpty() ? "(select)" : $"(unknown: {defName})");
            if (Widgets.ButtonText(listing.GetRect(24f), btn))
            {
                var opts = new List<FloatMenuOption>();
                foreach (GeneDef gd in DefDatabase<GeneDef>.AllDefsListForReading
                    .Where(gd => !gd.label.NullOrEmpty())
                    .OrderBy(gd => gd.label))
                {
                    string dn = gd.defName;
                    opts.Add(new FloatMenuOption(gd.label.CapitalizeFirst(), () => defName = dn));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        /// <summary>
        /// Relationship dropdown — PawnRelationDef. Handles empty labels.
        /// </summary>
        private void DrawRelationDropdown(Listing_Standard listing)
        {
            listing.Label("Relationship:");
            PawnRelationDef cur = DefDatabase<PawnRelationDef>.GetNamedSilentFail(defName);
            string btnLabel = cur != null ? (cur.label?.CapitalizeFirst() ?? cur.defName)
                : (defName.NullOrEmpty() ? "(select)" : $"(unknown: {defName})");
            if (Widgets.ButtonText(listing.GetRect(24f), btnLabel))
            {
                var opts = new List<FloatMenuOption>();
                foreach (PawnRelationDef rd in DefDatabase<PawnRelationDef>.AllDefsListForReading
                    .OrderBy(rd => rd.label ?? rd.defName))
                {
                    string dn  = rd.defName;
                    string lbl = !rd.label.NullOrEmpty() ? rd.label.CapitalizeFirst() : rd.defName;
                    opts.Add(new FloatMenuOption(lbl, () => defName = dn));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        /// <summary>
        /// Weapon dropdown — filters ThingDefs to ranged/melee weapons only.
        /// </summary>
        private void DrawWeaponDropdown(Listing_Standard listing)
        {
            listing.Label("Weapon:");
            ThingDef cur = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            string btn = cur != null ? (cur.label?.CapitalizeFirst() ?? cur.defName)
                : (defName.NullOrEmpty() ? "(select)" : $"(unknown: {defName})");
            if (Widgets.ButtonText(listing.GetRect(24f), btn))
            {
                var opts = new List<FloatMenuOption>();
                foreach (ThingDef td in DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(td => td.IsWeapon && !td.label.NullOrEmpty())
                    .OrderBy(td => td.label))
                {
                    string dn = td.defName;
                    opts.Add(new FloatMenuOption(td.label.CapitalizeFirst(), () => defName = dn));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        /// <summary>
        /// Passion level toggle: None / Minor / Major.
        /// Uses threshold field (0/1/2) to store the passion level.
        /// </summary>
        private void DrawPassionToggle(Listing_Standard listing)
        {
            Rect row = listing.GetRect(24f);
            float w  = row.width / 3f;
            int cur  = (int)threshold;
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x,         row.y, w, 24f), "None",  cur == 0, () => threshold = 0f);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w,     row.y, w, 24f), "Minor", cur == 1, () => threshold = 1f);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w * 2, row.y, w, 24f), "Major", cur == 2, () => threshold = 2f);
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
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x,         row.y, w, 24f), "\u2265 At least", comparator == CountComparator.AtLeast, () => comparator = CountComparator.AtLeast);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w,     row.y, w, 24f), "\u2264 At most",  comparator == CountComparator.AtMost,  () => comparator = CountComparator.AtMost);
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
                case PawnPropertyType.Hediff:         return "Hediff (has / missing)";
                case PawnPropertyType.NeedLevel:      return "Need level (mood, food\u2026)";
                case PawnPropertyType.SkillLevel:     return "Skill level";
                case PawnPropertyType.Trait:          return "Trait (has / missing)";
                case PawnPropertyType.Capacity:       return "Capacity (moving, sight\u2026)";
                case PawnPropertyType.IsDrafted:      return "Is drafted";
                case PawnPropertyType.IsIdle:         return "Is idle";
                case PawnPropertyType.IsDowned:       return "Is downed";
                case PawnPropertyType.InMentalBreak:  return "In mental break";
                case PawnPropertyType.Gender:         return "Gender (male / female)";
                case PawnPropertyType.Age:            return "Age (biological years)";
                case PawnPropertyType.Backstory:      return "Backstory (childhood / adulthood)";
                case PawnPropertyType.Gene:           return "Gene (has / missing) [Biotech]";
                case PawnPropertyType.Xenotype:       return "Xenotype (is / isn't) [Biotech]";
                case PawnPropertyType.RoyalTitle:     return "Royal title (has / missing) [Royalty]";
                case PawnPropertyType.Relationship:   return "Relationship (has / missing)";
                case PawnPropertyType.WorkDisabled:   return "Work type (incapable / capable)";
                case PawnPropertyType.EquippedWeapon: return "Equipped weapon";
                case PawnPropertyType.PawnName:       return "Pawn name (text search)";
                case PawnPropertyType.SkillPassion:   return "Skill passion (none / minor / major)";
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
            Scribe_Values.Look(ref propertyType,   "propertyType",   PawnPropertyType.Hediff);
            Scribe_Values.Look(ref defName,         "defName",         "");
            Scribe_Values.Look(ref boolCondition,   "boolCondition",   true);
            Scribe_Values.Look(ref threshold,       "threshold",       0.5f);
            Scribe_Values.Look(ref comparator,      "comparator",      CountComparator.AtLeast);
            Scribe_Values.Look(ref pawnKind,         "pawnKind",         PawnKindFilter.Colonist);
            Scribe_Values.Look(ref raceDefName,      "raceDefName",      "");
            Scribe_Values.Look(ref zoneLabel,        "zoneLabel",        "");
            Scribe_Values.Look(ref checkChildhood,   "checkChildhood",   true);
        }
    }
}
