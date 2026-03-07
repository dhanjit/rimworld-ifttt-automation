using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Sets, increments, decrements, multiplies, or resets a named automation variable.
    ///
    /// Variables are stored in <see cref="AutomationGameComp.numericVars"/> —
    /// a global, persisted <c>Dictionary&lt;string, float&gt;</c> shared across all rules
    /// and all maps. They survive save/load. Use <see cref="RimWorldIFTTT.Triggers.Trigger_Variable"/>
    /// to read them in trigger conditions.
    ///
    /// Typical state-machine usage:
    ///   IF oreLow AND phase==0  → SET phase = 1          (transitions to "mission active")
    ///   IF phase==1 AND done    → ADD phase   value=1    (transitions to "phase 2")
    ///   IF phase==2             → RESET phase            (returns to idle)
    ///
    /// Variables are created on first write; reading a variable that was never set
    /// returns 0 (configurable in Trigger_Variable).
    /// </summary>
    public class Action_SetVariable : AutomationAction
    {
        // ── Operation enum ────────────────────────────────────────────────────
        public enum SetMode
        {
            Set,       // var  = value
            Add,       // var += value
            Subtract,  // var -= value
            Multiply,  // var *= value
            Reset,     // remove var (returns to "not set")
        }

        // ── Config ────────────────────────────────────────────────────────────
        public string  variableName = "myVariable";
        public SetMode mode         = SetMode.Set;
        public float   value        = 1f;

        // UI buffer — not persisted; lazily initialised from value
        [System.NonSerialized] private string _buf;

        // ── Identity ──────────────────────────────────────────────────────────

        public override string Label => "Set variable";
        public override string Description => mode == SetMode.Reset
            ? $"Reset '{variableName}'"
            : $"'{variableName}' {ModeSymbol(mode)} {value:G}";

        public override bool  HasConfig    => true;
        public override float ConfigHeight => mode == SetMode.Reset ? 108f : 150f;

        // ── Execute ───────────────────────────────────────────────────────────

        public override bool Execute(Map map)
        {
            var comp = AutomationGameComp.Instance;
            if (comp == null) return false;
            if (variableName.NullOrEmpty()) return false;

            comp.numericVars.TryGetValue(variableName, out float current);

            float newValue;
            switch (mode)
            {
                case SetMode.Set:
                    newValue = value;
                    comp.numericVars[variableName] = newValue;
                    break;
                case SetMode.Add:
                    newValue = current + value;
                    comp.numericVars[variableName] = newValue;
                    break;
                case SetMode.Subtract:
                    newValue = current - value;
                    comp.numericVars[variableName] = newValue;
                    break;
                case SetMode.Multiply:
                    newValue = current * value;
                    comp.numericVars[variableName] = newValue;
                    break;
                case SetMode.Reset:
                    comp.numericVars.Remove(variableName);
                    if (comp.verboseLogging)
                        Log.Message($"[IFTTT] Variable '{variableName}' reset (removed).");
                    return true;
                default:
                    return false;
            }

            if (comp.verboseLogging)
                Log.Message($"[IFTTT] Variable '{variableName}' {ModeSymbol(mode)} {value:G} = {newValue:G}");

            return true;
        }

        // ── DrawConfig ────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            // Variable name
            listing.Label("Variable name:");
            variableName = listing.TextEntry(variableName ?? "");

            listing.Gap(2f);
            listing.Label("Operation:");

            // 5 mode toggle buttons across one row
            Rect modeRow = listing.GetRect(26f);
            float bw = modeRow.width / 5f;

            DrawModeBtn(new Rect(modeRow.x + bw * 0, modeRow.y, bw - 2f, 26f),
                        SetMode.Set,      "= (set)");
            DrawModeBtn(new Rect(modeRow.x + bw * 1, modeRow.y, bw - 2f, 26f),
                        SetMode.Add,      "+ (add)");
            DrawModeBtn(new Rect(modeRow.x + bw * 2, modeRow.y, bw - 2f, 26f),
                        SetMode.Subtract, "- (sub)");
            DrawModeBtn(new Rect(modeRow.x + bw * 3, modeRow.y, bw - 2f, 26f),
                        SetMode.Multiply, "\u00D7 (mul)");  // ×
            DrawModeBtn(new Rect(modeRow.x + bw * 4, modeRow.y, bw - 2f, 26f),
                        SetMode.Reset,    "del");

            // Value field (hidden for Reset since there's nothing to specify)
            if (mode != SetMode.Reset)
            {
                listing.Gap(2f);
                Rect valRow  = listing.GetRect(26f);
                string label = ModePrefix(mode) + " :";
                float  lw    = Text.CalcSize(label).x + 10f;
                Widgets.Label(new Rect(valRow.x, valRow.y + 3f, lw, 26f), label);
                _buf ??= value.ToString("G");
                Widgets.TextFieldNumeric(
                    new Rect(valRow.x + lw, valRow.y, valRow.width - lw, 26f),
                    ref value, ref _buf, -9999999f, 9999999f);
            }

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

        // ── Helpers ───────────────────────────────────────────────────────────

        private void DrawModeBtn(Rect r, SetMode m, string label)
            => PawnFilterHelper.DrawToggleBtn(r, label, mode == m, () =>
            {
                mode = m;
                _buf = null;  // reset value buffer when mode changes
            });

        private static string ModeSymbol(SetMode m) => m switch
        {
            SetMode.Set      => "=",
            SetMode.Add      => "+=",
            SetMode.Subtract => "-=",
            SetMode.Multiply => "*=",
            SetMode.Reset    => "reset",
            _                => "?",
        };

        private static string ModePrefix(SetMode m) => m switch
        {
            SetMode.Set      => "Set to",
            SetMode.Add      => "Add",
            SetMode.Subtract => "Subtract",
            SetMode.Multiply => "Multiply by",
            _                => "Value",
        };

        // ── ExposeData ────────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref variableName, "variableName", "myVariable");
            Scribe_Values.Look(ref mode,         "mode",         SetMode.Set);
            Scribe_Values.Look(ref value,        "value",        1f);
        }
    }
}
