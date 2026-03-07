using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using RimWorldIFTTT.Triggers;

namespace RimWorldIFTTT
{
    /// <summary>
    /// Bundles all behavior for one pawn property query type.
    /// Data (defName, threshold, boolCondition, etc.) lives on Trigger_PawnState;
    /// this class holds the behavior functions that operate on that data.
    /// </summary>
    public class PawnQueryDef
    {
        public PawnPropertyType                              Type;
        public string                                        MenuLabel;
        /// Per-pawn evaluator — returns true if this pawn matches the trigger condition.
        public Func<Pawn, Trigger_PawnState, bool>           Evaluate;
        /// Builds the human-readable description shown in the trigger's Description property.
        public Func<Trigger_PawnState, string>               Describe;
        /// Draws property-specific UI controls (dropdowns, toggles, sliders).
        public Action<Trigger_PawnState, Listing_Standard>   DrawUI;
        /// Extra UI height beyond the base 80f shared by all instances.
        public Func<Trigger_PawnState, float>                ExtraHeight;
    }

    /// <summary>
    /// Data-driven registry replacing the 5 parallel switch blocks in Trigger_PawnState.
    /// Adding a new pawn property type requires exactly one Register() call here —
    /// no changes to Trigger_PawnState itself.
    /// </summary>
    public static class PawnQueryRegistry
    {
        private static readonly Dictionary<PawnPropertyType, PawnQueryDef> _defs =
            new Dictionary<PawnPropertyType, PawnQueryDef>();

        public static PawnQueryDef              Get(PawnPropertyType type) =>
            _defs.TryGetValue(type, out var d) ? d : null;

        public static IEnumerable<PawnQueryDef> All => _defs.Values;

        public static List<FloatMenuOption> BuildPropertyMenu(Action<PawnPropertyType> setter)
        {
            var opts = new List<FloatMenuOption>();
            foreach (PawnPropertyType pt in Enum.GetValues(typeof(PawnPropertyType)))
            {
                PawnPropertyType cap = pt;
                opts.Add(new FloatMenuOption(Get(cap)?.MenuLabel ?? cap.ToString(), () => setter(cap)));
            }
            return opts;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Shared helpers (description + evaluation utilities)
        // ════════════════════════════════════════════════════════════════════

        /// "Any colonist in 'Home'" preamble used by every description.
        public static string PawnPrefix(Trigger_PawnState t)
        {
            string kind = t.pawnKind.ToString().ToLower();
            string zone = t.zoneLabel.NullOrEmpty() ? "" : $" in '{t.zoneLabel}'";
            return $"Any {kind}{zone}";
        }

        public static string DefLabel<T>(string defName) where T : Def
        {
            if (defName.NullOrEmpty()) return "???";
            T d = DefDatabase<T>.GetNamedSilentFail(defName);
            return d != null ? (d.label?.CapitalizeFirst() ?? d.defName) : defName;
        }

        public static string BackstoryLabel(string defName)
        {
            if (defName.NullOrEmpty()) return "???";
            BackstoryDef d = DefDatabase<BackstoryDef>.GetNamedSilentFail(defName);
            if (d == null) return defName;
            if (!d.title.NullOrEmpty())  return d.title.CapitalizeFirst();
            if (!d.label.NullOrEmpty())  return d.label.CapitalizeFirst();
            return d.defName;
        }

        public static string GetTraitLabel(TraitDef td)
        {
            if (td == null) return "(unknown)";
            if (td.degreeDatas?.Count > 0)
            {
                string joined = string.Join(" / ", td.degreeDatas
                    .Where(d => !d.label.NullOrEmpty())
                    .Select(d => d.label.CapitalizeFirst()));
                if (!joined.NullOrEmpty()) return joined;
            }
            return td.label?.CapitalizeFirst() ?? td.defName;
        }

        public static string PassionLabel(int level) =>
            level == 0 ? "None" : level == 1 ? "Minor" : level == 2 ? "Major" : "???";

        public static string CompSym(CountComparator c) =>
            c == CountComparator.AtLeast ? "\u2265" : c == CountComparator.AtMost ? "\u2264" : "=";

        public static bool CompareNum(float value, float threshold, CountComparator comparator)
        {
            switch (comparator)
            {
                case CountComparator.AtLeast: return value >= threshold;
                case CountComparator.AtMost:  return value <= threshold;
                default:                      return Math.Abs(value - threshold) < 0.01f;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Shared UI building blocks
        // ════════════════════════════════════════════════════════════════════

        /// Generic def dropdown. Optional filter narrows the shown defs (e.g. IsWeapon).
        public static void DrawDefDropdown<T>(
            Listing_Standard listing, Trigger_PawnState t, string label,
            Func<IEnumerable<T>, IEnumerable<T>> filter = null) where T : Def
        {
            listing.Label(label);
            T cur = DefDatabase<T>.GetNamedSilentFail(t.defName);
            string btn = cur != null
                ? (cur.label?.CapitalizeFirst() ?? cur.defName)
                : (t.defName.NullOrEmpty() ? "(select)" : $"(unknown: {t.defName})");

            if (Widgets.ButtonText(listing.GetRect(24f), btn))
            {
                IEnumerable<T> src = DefDatabase<T>.AllDefsListForReading.Where(d => !d.label.NullOrEmpty());
                if (filter != null) src = filter(src);
                var opts = src.OrderBy(d => d.label)
                    .Select(d => { string dn = d.defName; return new FloatMenuOption(d.label.CapitalizeFirst(), () => t.defName = dn); })
                    .ToList();
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        public static void DrawBoolToggle(
            Listing_Standard listing, Trigger_PawnState t, string trueLabel, string falseLabel)
        {
            Rect row = listing.GetRect(24f);
            float hw = row.width / 2f;
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x,      row.y, hw, 24f), trueLabel,  t.boolCondition,  () => t.boolCondition = true);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + hw, row.y, hw, 24f), falseLabel, !t.boolCondition, () => t.boolCondition = false);
        }

        public static void DrawComparator(Listing_Standard listing, Trigger_PawnState t)
        {
            Rect row = listing.GetRect(24f);
            float w = row.width / 3f;
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x,         row.y, w, 24f), "\u2265 At least", t.comparator == CountComparator.AtLeast, () => t.comparator = CountComparator.AtLeast);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w,     row.y, w, 24f), "\u2264 At most",  t.comparator == CountComparator.AtMost,  () => t.comparator = CountComparator.AtMost);
            PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w * 2, row.y, w, 24f), "= Exactly",       t.comparator == CountComparator.Exactly, () => t.comparator = CountComparator.Exactly);
        }

        public static void DrawSlider(
            Listing_Standard listing, Trigger_PawnState t, float min, float max, string label)
        {
            t.threshold = Widgets.HorizontalSlider(listing.GetRect(28f), t.threshold, min, max, false, label);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Factory methods (eliminate parallel switch blocks)
        // ════════════════════════════════════════════════════════════════════

        /// Def + bool: pick a def, check has/missing. Used by Hediff, Gene, Xenotype, etc.
        private static void RegisterDefBool<T>(
            PawnPropertyType type, string menuLabel, string dropdownLabel,
            string trueVerb, string falseVerb,
            Func<Pawn, T, bool> hasDef,
            Func<IEnumerable<T>, IEnumerable<T>> filter = null) where T : Def
        {
            Register(new PawnQueryDef
            {
                Type = type, MenuLabel = menuLabel, ExtraHeight = _ => 70f,
                Evaluate = (p, t) =>
                {
                    T d = DefDatabase<T>.GetNamedSilentFail(t.defName);
                    return d != null && hasDef(p, d) != t.boolCondition;
                },
                Describe = t =>
                {
                    string lbl = DefLabel<T>(t.defName);
                    return t.boolCondition ? $"{PawnPrefix(t)} {trueVerb}: {lbl}"
                                          : $"{PawnPrefix(t)} {falseVerb}: {lbl}";
                },
                DrawUI = (t, listing) =>
                {
                    DrawDefDropdown<T>(listing, t, dropdownLabel, filter);
                    DrawBoolToggle(listing, t, trueVerb, falseVerb);
                },
            });
        }

        /// Def + numeric: pick a def, compare a numeric property.
        /// Used by NeedLevel, SkillLevel, Capacity.
        private static void RegisterDefNumeric<T>(
            PawnPropertyType type, string menuLabel, string dropdownLabel,
            float sliderMin, float sliderMax, Func<float, string> sliderLabel,
            Func<Pawn, T, float?> getValue) where T : Def
        {
            Register(new PawnQueryDef
            {
                Type = type, MenuLabel = menuLabel, ExtraHeight = _ => 90f,
                Evaluate = (p, t) =>
                {
                    T d = DefDatabase<T>.GetNamedSilentFail(t.defName);
                    if (d == null) return false;
                    float? val = getValue(p, d);
                    return val.HasValue && CompareNum(val.Value, t.threshold, t.comparator);
                },
                Describe = t => $"{PawnPrefix(t)} {DefLabel<T>(t.defName)} {CompSym(t.comparator)} {sliderLabel(t.threshold)}",
                DrawUI = (t, listing) =>
                {
                    DrawDefDropdown<T>(listing, t, dropdownLabel);
                    DrawComparator(listing, t);
                    DrawSlider(listing, t, sliderMin, sliderMax, sliderLabel(t.threshold));
                },
            });
        }

        /// Boolean flag: simple true/false check on a pawn property.
        /// Used by IsDrafted, IsIdle, IsDowned, InMentalBreak, Gender.
        private static void RegisterBoolFlag(
            PawnPropertyType type, string menuLabel,
            string trueLabel, string falseLabel,
            Func<Pawn, bool> getFlag)
        {
            Register(new PawnQueryDef
            {
                Type = type, MenuLabel = menuLabel, ExtraHeight = _ => 30f,
                Evaluate = (p, t) => getFlag(p) == t.boolCondition,
                Describe = t => $"{PawnPrefix(t)} {(t.boolCondition ? trueLabel.ToLower() : falseLabel.ToLower())}",
                DrawUI = (t, listing) => DrawBoolToggle(listing, t, trueLabel, falseLabel),
            });
        }

        /// Numeric only: compare a derived pawn value with no def picker.
        /// Used by Age, PsylinkLevel, Psyfocus, PsychicEntropy.
        private static void RegisterNumericOnly(
            PawnPropertyType type, string menuLabel,
            float sliderMin, float sliderMax, Func<float, string> sliderLabel,
            Func<Pawn, float?> getValue,
            Func<Trigger_PawnState, string> describe)
        {
            Register(new PawnQueryDef
            {
                Type = type, MenuLabel = menuLabel, ExtraHeight = _ => 60f,
                Evaluate = (p, t) =>
                {
                    float? val = getValue(p);
                    return val.HasValue && CompareNum(val.Value, t.threshold, t.comparator);
                },
                Describe = describe,
                DrawUI = (t, listing) =>
                {
                    DrawComparator(listing, t);
                    DrawSlider(listing, t, sliderMin, sliderMax, sliderLabel(t.threshold));
                },
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  All registrations
        // ════════════════════════════════════════════════════════════════════

        static PawnQueryRegistry() => RegisterAll();

        private static void RegisterAll()
        {
            // ── Group 1: Def + Has/Missing ────────────────────────────────────

            RegisterDefBool<HediffDef>(PawnPropertyType.Hediff,
                "Hediff (has / missing)", "Hediff:", "Missing", "Has",
                (p, d) => p.health?.hediffSet?.HasHediff(d) == true);

            RegisterDefBool<GeneDef>(PawnPropertyType.Gene,
                "Gene (has / missing) [Biotech]", "Gene:", "Missing", "Has",
                (p, d) => p.genes?.HasActiveGene(d) == true);

            RegisterDefBool<XenotypeDef>(PawnPropertyType.Xenotype,
                "Xenotype (is / isn't) [Biotech]", "Xenotype:", "Is NOT", "Is",
                (p, d) => p.genes?.Xenotype?.defName == d.defName);

            RegisterDefBool<RoyalTitleDef>(PawnPropertyType.RoyalTitle,
                "Royal title (has / missing) [Royalty]", "Royal title:", "Missing", "Has",
                (p, d) => p.royalty?.AllTitlesInEffectForReading?.Any(rt => rt.def == d) == true);

            RegisterDefBool<WorkTypeDef>(PawnPropertyType.WorkDisabled,
                "Work type (incapable / capable)", "Work type:", "Incapable", "Capable",
                (p, d) => p.WorkTypeIsDisabled(d));

            // HasAbility covers vanilla psycasts, VPE psycasts, and any other AbilityDef.
            RegisterDefBool<AbilityDef>(PawnPropertyType.HasAbility,
                "Ability / Psycast (has / missing)", "Ability / Psycast:", "Missing", "Has",
                (p, d) => p.abilities?.GetAbility(d) != null);

            // ── Group 2: Def + Numeric ────────────────────────────────────────

            RegisterDefNumeric<NeedDef>(PawnPropertyType.NeedLevel,
                "Need level (mood, food\u2026)", "Need:", 0f, 1f, v => $"Threshold: {v:P0}",
                (p, d) => p.needs?.TryGetNeed(d)?.CurLevelPercentage);

            RegisterDefNumeric<SkillDef>(PawnPropertyType.SkillLevel,
                "Skill level", "Skill:", 0f, 20f, v => $"Level: {(int)v}",
                (p, d) => { SkillRecord sr = p.skills?.GetSkill(d); return sr != null ? (float?)sr.Level : null; });

            RegisterDefNumeric<PawnCapacityDef>(PawnPropertyType.Capacity,
                "Capacity (moving, sight\u2026)", "Capacity:", 0f, 1.5f, v => $"Threshold: {v:P0}",
                (p, d) => p.health?.capacities != null ? (float?)p.health.capacities.GetLevel(d) : null);

            // ── Group 3: Boolean flags ────────────────────────────────────────

            RegisterBoolFlag(PawnPropertyType.IsDrafted,
                "Is drafted",     "Is drafted",      "Is not drafted",      p => p.Drafted);

            RegisterBoolFlag(PawnPropertyType.IsIdle,
                "Is idle",        "Is idle",         "Is not idle",         p => p.CurJob == null);

            RegisterBoolFlag(PawnPropertyType.IsDowned,
                "Is downed",      "Is downed",       "Is not downed",       p => p.Downed);

            RegisterBoolFlag(PawnPropertyType.InMentalBreak,
                "In mental break","In mental break", "Not in mental break", p => p.InMentalState);

            RegisterBoolFlag(PawnPropertyType.Gender,
                "Gender (male / female)", "Female", "Male",                 p => p.gender == Gender.Female);

            // ── Group 4: Numeric only (no def picker) ────────────────────────

            RegisterNumericOnly(PawnPropertyType.Age,
                "Age (biological years)", 0f, 120f, v => $"Age: {(int)v} years",
                p => p.ageTracker?.AgeBiologicalYearsFloat,
                t => $"{PawnPrefix(t)} age {CompSym(t.comparator)} {(int)t.threshold} years");

            // Psylink level 0-6 — reads the PsychicAmplifier hediff severity.
            RegisterNumericOnly(PawnPropertyType.PsylinkLevel,
                "Psylink level (0\u20136)", 0f, 6f, v => $"Level: {(int)v}",
                p => (float?)GetPsylinkLevel(p),
                t => $"{PawnPrefix(t)} psylink level {CompSym(t.comparator)} {(int)t.threshold}");

            // Psyfocus 0-100% — direct read from psychic entropy tracker.
            RegisterNumericOnly(PawnPropertyType.Psyfocus,
                "Psyfocus (%)", 0f, 1f, v => $"Psyfocus: {v:P0}",
                p => p.psychicEntropy?.CurrentPsyfocus,
                t => $"{PawnPrefix(t)} psyfocus {CompSym(t.comparator)} {t.threshold:P0}");

            // Psychic entropy as fraction of max (EntropyRelativeValue is already 0-1).
            RegisterNumericOnly(PawnPropertyType.PsychicEntropy,
                "Psychic entropy (%)", 0f, 1f, v => $"Entropy: {v:P0}",
                p => p.psychicEntropy?.EntropyRelativeValue,
                t => $"{PawnPrefix(t)} psychic entropy {CompSym(t.comparator)} {t.threshold:P0}");

            // ── Group 5: Custom (unique dropdown or UI logic) ─────────────────

            // Trait — uses degreeDatas for labels, not Def.label.
            Register(new PawnQueryDef
            {
                Type = PawnPropertyType.Trait, MenuLabel = "Trait (has / missing)", ExtraHeight = _ => 70f,
                Evaluate = (p, t) =>
                {
                    TraitDef d = DefDatabase<TraitDef>.GetNamedSilentFail(t.defName);
                    return d != null && (p.story?.traits?.HasTrait(d) == true) != t.boolCondition;
                },
                Describe = t =>
                {
                    string lbl = GetTraitLabel(DefDatabase<TraitDef>.GetNamedSilentFail(t.defName));
                    return t.boolCondition ? $"{PawnPrefix(t)} missing trait: {lbl}"
                                          : $"{PawnPrefix(t)} has trait: {lbl}";
                },
                DrawUI = (t, listing) =>
                {
                    listing.Label("Trait:");
                    TraitDef cur = DefDatabase<TraitDef>.GetNamedSilentFail(t.defName);
                    string btn = cur != null ? GetTraitLabel(cur)
                        : (t.defName.NullOrEmpty() ? "(select)" : $"(unknown: {t.defName})");
                    if (Widgets.ButtonText(listing.GetRect(24f), btn))
                    {
                        var opts = DefDatabase<TraitDef>.AllDefsListForReading
                            .OrderBy(td => GetTraitLabel(td))
                            .Select(td => { string dn = td.defName; string lbl = GetTraitLabel(td); return new FloatMenuOption(lbl, () => t.defName = dn); })
                            .ToList();
                        Find.WindowStack.Add(new FloatMenu(opts));
                    }
                    DrawBoolToggle(listing, t, "Missing", "Has");
                },
            });

            // Backstory — childhood/adulthood slot toggle + custom dropdown (.title label).
            Register(new PawnQueryDef
            {
                Type = PawnPropertyType.Backstory, MenuLabel = "Backstory (childhood / adulthood)", ExtraHeight = _ => 100f,
                Evaluate = (p, t) =>
                {
                    BackstoryDef d = DefDatabase<BackstoryDef>.GetNamedSilentFail(t.defName);
                    if (d == null) return false;
                    BackstoryDef pawnBs = t.checkChildhood ? p.story?.Childhood : p.story?.Adulthood;
                    return (pawnBs?.defName == d.defName) != t.boolCondition;
                },
                Describe = t =>
                {
                    string slot = t.checkChildhood ? "childhood" : "adulthood";
                    return t.boolCondition ? $"{PawnPrefix(t)} missing {slot}: {BackstoryLabel(t.defName)}"
                                          : $"{PawnPrefix(t)} has {slot}: {BackstoryLabel(t.defName)}";
                },
                DrawUI = (t, listing) =>
                {
                    Rect slotRow = listing.GetRect(24f);
                    float hw = slotRow.width / 2f;
                    PawnFilterHelper.DrawToggleBtn(new Rect(slotRow.x,      slotRow.y, hw, 24f), "Childhood",  t.checkChildhood,  () => { t.checkChildhood = true;  t.defName = ""; });
                    PawnFilterHelper.DrawToggleBtn(new Rect(slotRow.x + hw, slotRow.y, hw, 24f), "Adulthood", !t.checkChildhood, () => { t.checkChildhood = false; t.defName = ""; });
                    listing.Label(t.checkChildhood ? "Childhood backstory:" : "Adulthood backstory:");
                    string btnLbl = t.defName.NullOrEmpty() ? "(select)" : BackstoryLabel(t.defName);
                    if (Widgets.ButtonText(listing.GetRect(24f), btnLbl))
                    {
                        var opts = DefDatabase<BackstoryDef>.AllDefsListForReading
                            .OrderBy(bd => bd.title ?? bd.label ?? bd.defName)
                            .Select(bd =>
                            {
                                string dn  = bd.defName;
                                string lbl = !bd.title.NullOrEmpty() ? bd.title.CapitalizeFirst()
                                           : !bd.label.NullOrEmpty() ? bd.label.CapitalizeFirst()
                                           : bd.defName;
                                return new FloatMenuOption(lbl, () => t.defName = dn);
                            }).ToList();
                        Find.WindowStack.Add(new FloatMenu(opts));
                    }
                    DrawBoolToggle(listing, t, "Missing", "Has");
                },
            });

            // Relationship — label fallback to defName for relations without labels.
            Register(new PawnQueryDef
            {
                Type = PawnPropertyType.Relationship, MenuLabel = "Relationship (has / missing)", ExtraHeight = _ => 70f,
                Evaluate = (p, t) =>
                {
                    PawnRelationDef d = DefDatabase<PawnRelationDef>.GetNamedSilentFail(t.defName);
                    return d != null && (p.relations?.DirectRelations?.Any(r => r.def == d) == true) != t.boolCondition;
                },
                Describe = t =>
                {
                    string lbl = DefLabel<PawnRelationDef>(t.defName);
                    return t.boolCondition ? $"{PawnPrefix(t)} has no {lbl}" : $"{PawnPrefix(t)} has {lbl}";
                },
                DrawUI = (t, listing) =>
                {
                    listing.Label("Relationship:");
                    PawnRelationDef cur = DefDatabase<PawnRelationDef>.GetNamedSilentFail(t.defName);
                    string btn = cur != null ? (cur.label?.CapitalizeFirst() ?? cur.defName)
                        : (t.defName.NullOrEmpty() ? "(select)" : $"(unknown: {t.defName})");
                    if (Widgets.ButtonText(listing.GetRect(24f), btn))
                    {
                        var opts = DefDatabase<PawnRelationDef>.AllDefsListForReading
                            .OrderBy(rd => rd.label ?? rd.defName)
                            .Select(rd => { string dn = rd.defName; string lbl = !rd.label.NullOrEmpty() ? rd.label.CapitalizeFirst() : rd.defName; return new FloatMenuOption(lbl, () => t.defName = dn); })
                            .ToList();
                        Find.WindowStack.Add(new FloatMenu(opts));
                    }
                    DrawBoolToggle(listing, t, "Has none", "Has");
                },
            });

            // Equipped weapon — ThingDef dropdown filtered to weapons only.
            Register(new PawnQueryDef
            {
                Type = PawnPropertyType.EquippedWeapon, MenuLabel = "Equipped weapon", ExtraHeight = _ => 70f,
                Evaluate = (p, t) =>
                {
                    ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(t.defName);
                    return d != null && (p.equipment?.Primary?.def == d) != t.boolCondition;
                },
                Describe = t =>
                {
                    string lbl = DefLabel<ThingDef>(t.defName);
                    return t.boolCondition ? $"{PawnPrefix(t)} NOT equipped: {lbl}" : $"{PawnPrefix(t)} equipped with: {lbl}";
                },
                DrawUI = (t, listing) =>
                {
                    DrawDefDropdown<ThingDef>(listing, t, "Weapon:", src => src.Where(td => td.IsWeapon));
                    DrawBoolToggle(listing, t, "NOT equipped", "Equipped");
                },
            });

            // Pawn name — text search (case-insensitive contains).
            Register(new PawnQueryDef
            {
                Type = PawnPropertyType.PawnName, MenuLabel = "Pawn name (text search)", ExtraHeight = _ => 60f,
                Evaluate = (p, t) =>
                {
                    if (t.defName.NullOrEmpty()) return false;
                    string fullName = p.Name?.ToStringFull?.ToLower() ?? "";
                    return fullName.Contains(t.defName.ToLower()) == t.boolCondition;
                },
                Describe = t =>
                {
                    string s = t.defName.NullOrEmpty() ? "???" : t.defName;
                    return t.boolCondition ? $"{PawnPrefix(t)} name contains: \"{s}\""
                                          : $"{PawnPrefix(t)} name does NOT contain: \"{s}\"";
                },
                DrawUI = (t, listing) =>
                {
                    listing.Label("Name contains (case-insensitive):");
                    t.defName = listing.TextEntry(t.defName);
                    DrawBoolToggle(listing, t, "Contains", "Does NOT contain");
                },
            });

            // Skill passion — skill dropdown + None/Minor/Major toggle.
            // Stores passion level in threshold field (0/1/2).
            Register(new PawnQueryDef
            {
                Type = PawnPropertyType.SkillPassion, MenuLabel = "Skill passion (none / minor / major)", ExtraHeight = _ => 70f,
                Evaluate = (p, t) =>
                {
                    SkillDef d = DefDatabase<SkillDef>.GetNamedSilentFail(t.defName);
                    if (d == null) return false;
                    SkillRecord sr = p.skills?.GetSkill(d);
                    return sr != null && sr.passion == (Passion)(int)t.threshold;
                },
                Describe = t => $"{PawnPrefix(t)} {DefLabel<SkillDef>(t.defName)} passion is {PassionLabel((int)t.threshold)}",
                DrawUI = (t, listing) =>
                {
                    DrawDefDropdown<SkillDef>(listing, t, "Skill:");
                    Rect row = listing.GetRect(24f);
                    float w = row.width / 3f;
                    int cur = (int)t.threshold;
                    PawnFilterHelper.DrawToggleBtn(new Rect(row.x,         row.y, w, 24f), "None",  cur == 0, () => t.threshold = 0f);
                    PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w,     row.y, w, 24f), "Minor", cur == 1, () => t.threshold = 1f);
                    PawnFilterHelper.DrawToggleBtn(new Rect(row.x + w * 2, row.y, w, 24f), "Major", cur == 2, () => t.threshold = 2f);
                },
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  Private utilities
        // ════════════════════════════════════════════════════════════════════

        /// Returns the pawn's psylink level (0 if no psylink or Royalty DLC absent).
        private static int GetPsylinkLevel(Pawn p)
        {
            HediffDef amplifierDef = DefDatabase<HediffDef>.GetNamedSilentFail("PsychicAmplifier");
            if (amplifierDef == null) return 0;
            Hediff amp = p.health?.hediffSet?.GetFirstHediffOfDef(amplifierDef);
            return amp != null ? (int)amp.Severity : 0;
        }

        private static void Register(PawnQueryDef def) => _defs[def.Type] = def;
    }
}
