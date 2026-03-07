using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Fires when a named automation variable satisfies a numeric comparison.
    ///
    /// Variables are written by <see cref="RimWorldIFTTT.Actions.Action_SetVariable"/> and stored
    /// in <see cref="AutomationGameComp.numericVars"/> — a global, persisted float dictionary.
    ///
    /// This is the read-side of the IFTTT state machine. By chaining rules that set variables
    /// and other rules that read them, you can build proper multi-phase workflows:
    ///
    ///   Rule A (priority 10): IF missionPhase == 0 AND ore low  → SET missionPhase = 1
    ///   Rule B (priority 20): IF missionPhase == 1              → [do work] → SET missionPhase = 2
    ///   Rule C (priority 30): IF missionPhase == 2 AND complete → SET missionPhase = 0
    ///
    /// The variable "missionPhase" persists across save/load and across map changes.
    /// </summary>
    public class Trigger_Variable : AutomationTrigger
    {
        // ── Comparator enum ───────────────────────────────────────────────────
        public enum Cmp
        {
            Equal,          // ==  exact match (tolerance 0.0001)
            NotEqual,       // ≠   any value other than threshold
            GreaterThan,    // >   strictly greater
            GreaterOrEqual, // ≥   at least
            LessThan,       // <   strictly less
            LessOrEqual,    // ≤   at most
        }

        // ── Config ────────────────────────────────────────────────────────────
        public string variableName   = "myVariable";
        public Cmp    comparator     = Cmp.GreaterOrEqual;
        public float  threshold      = 1f;

        /// <summary>
        /// When true (default): if the variable has never been set, IsTriggered returns false.
        /// When false: a missing variable is treated as 0 for comparison purposes.
        /// </summary>
        public bool missingIsFalse = true;

        // UI buffer — not persisted; lazily initialized from threshold
        [System.NonSerialized] private string _buf;

        // ── Identity ──────────────────────────────────────────────────────────

        public override string Label => "Variable condition";
        public override string Description
            => $"var '{variableName}' {CmpSymbol(comparator)} {threshold:G}";

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 132f;

        // ── Evaluation ────────────────────────────────────────────────────────

        public override bool IsTriggered(Map map)
        {
            var comp = AutomationGameComp.Instance;
            if (comp == null) return false;

            if (!comp.numericVars.TryGetValue(variableName ?? "", out float val))
                return !missingIsFalse;  // true only when "treat missing as 0" is enabled

            return Evaluate(val, comparator, threshold);
        }

        private static bool Evaluate(float val, Cmp cmp, float t) => cmp switch
        {
            Cmp.Equal          => Math.Abs(val - t) < 0.0001f,
            Cmp.NotEqual       => Math.Abs(val - t) >= 0.0001f,
            Cmp.GreaterThan    => val > t,
            Cmp.GreaterOrEqual => val >= t,
            Cmp.LessThan       => val < t,
            Cmp.LessOrEqual    => val <= t,
            _                  => false,
        };

        // ── Comparator helpers ────────────────────────────────────────────────

        public static string CmpSymbol(Cmp c) => c switch
        {
            Cmp.Equal          => "==",
            Cmp.NotEqual       => "\u2260",   // ≠
            Cmp.GreaterThan    => ">",
            Cmp.GreaterOrEqual => "\u2265",   // ≥
            Cmp.LessThan       => "<",
            Cmp.LessOrEqual    => "\u2264",   // ≤
            _                  => "?",
        };

        private static string CmpFull(Cmp c) => c switch
        {
            Cmp.Equal          => "== (equal)",
            Cmp.NotEqual       => "\u2260  (not equal)",
            Cmp.GreaterThan    => ">  (strictly greater)",
            Cmp.GreaterOrEqual => "\u2265  (at least / greater-or-equal)",
            Cmp.LessThan       => "<  (strictly less)",
            Cmp.LessOrEqual    => "\u2264  (at most / less-or-equal)",
            _                  => "?",
        };

        // ── DrawConfig ────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Variable name:");
            variableName = listing.TextEntry(variableName ?? "");

            listing.Gap(2f);

            // Comparator dropdown + threshold field on one row
            Rect row  = listing.GetRect(26f);
            float cmpW = 156f;

            if (Widgets.ButtonText(new Rect(row.x, row.y, cmpW, 26f),
                                   $"{CmpSymbol(comparator)}  \u25bc"))
            {
                var opts = new List<FloatMenuOption>();
                foreach (Cmp c in (Cmp[])Enum.GetValues(typeof(Cmp)))
                {
                    Cmp cap = c;
                    opts.Add(new FloatMenuOption(CmpFull(cap), () =>
                    {
                        comparator = cap;
                        _buf = null;  // reset UI buffer on comparator change
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            _buf ??= threshold.ToString("G");
            Widgets.TextFieldNumeric(
                new Rect(row.x + cmpW + 4f, row.y, row.width - cmpW - 4f, 26f),
                ref threshold, ref _buf, -9999999f, 9999999f);

            listing.Gap(2f);

            // "treat missing as 0" toggle
            bool treatAs0 = !missingIsFalse;
            listing.CheckboxLabeled(
                "If variable is unset, treat as 0  (unchecked = do not trigger)",
                ref treatAs0);
            missingIsFalse = !treatAs0;

            // ── Live current-value readout ──────────────────────────────────
            var comp = AutomationGameComp.Instance;
            if (comp != null && !(variableName?.NullOrEmpty() ?? true))
            {
                listing.Gap(2f);
                bool hasVal = comp.numericVars.TryGetValue(variableName, out float cur);
                Text.Font = GameFont.Tiny;
                GUI.color = hasVal
                    ? new Color(0.70f, 0.90f, 0.70f)
                    : new Color(0.55f, 0.55f, 0.55f);
                listing.Label(hasVal
                    ? $"Current value: {cur:G}"
                    : "Current value: (not set)");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }

        // ── ExposeData ────────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref variableName,   "variableName",   "myVariable");
            Scribe_Values.Look(ref comparator,     "comparator",     Cmp.GreaterOrEqual);
            Scribe_Values.Look(ref threshold,      "threshold",      1f);
            Scribe_Values.Look(ref missingIsFalse, "missingIsFalse", true);
        }
    }
}
