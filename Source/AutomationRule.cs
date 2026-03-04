using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWorldIFTTT
{
    /// <summary>
    /// An automation rule: [IF triggers] THEN [actions].
    ///
    /// Supports:
    ///   • Multiple triggers combined with AND or OR logic
    ///   • Trigger negation (NOT) per trigger entry
    ///   • Multiple sequenced actions
    ///   • Per-rule check frequency (how often triggers are evaluated)
    ///   • Per-rule cooldown (minimum time between actual firings)
    ///   • Priority, category, one-shot mode, max-fires cap
    /// </summary>
    public class AutomationRule : IExposable
    {
        // ── Identity ──────────────────────────────────────────────────────────
        public string       name     = "Unnamed Rule";
        public string       notes    = "";         // player notes / description
        public bool         enabled  = true;
        public RuleCategory category = RuleCategory.Custom;

        /// <summary>Lower number = evaluated first. Useful when rules interact.</summary>
        public int priority = 50;

        // ── Trigger composition ───────────────────────────────────────────────
        /// <summary>How multiple triggers are combined: All (AND) or Any (OR).</summary>
        public TriggerMode triggerMode = TriggerMode.All;

        /// <summary>
        /// The list of trigger entries. Each entry wraps a trigger and an optional NOT flag.
        /// </summary>
        public List<TriggerEntry> triggerEntries = new List<TriggerEntry>();

        // ── Actions ───────────────────────────────────────────────────────────
        /// <summary>Actions executed in order when all conditions are met.</summary>
        public List<AutomationAction> actions = new List<AutomationAction>();

        // ── Check frequency ───────────────────────────────────────────────────
        /// <summary>
        /// How often (ticks) this rule's triggers are evaluated.
        /// Default 2500 ≈ 1 in-game hour. Use larger values for expensive or
        /// infrequent checks (e.g., 60000 = 1 day for animal-gear inspection).
        /// This is independent of cooldown: a rule can be checked every hour but
        /// only fire at most once per day.
        /// </summary>
        public int checkFrequencyTicks = 2500;

        /// <summary>Game tick when triggers were last evaluated (persisted).</summary>
        public int lastCheckedTick = -999999;

        /// <summary>True when enough time has passed since the last trigger evaluation.</summary>
        public bool IsDueForCheck(int currentTick)
            => (currentTick - lastCheckedTick) >= checkFrequencyTicks;

        // ── Cooldown ──────────────────────────────────────────────────────────
        /// <summary>Minimum ticks between firings (2500 = ~1 in-game hour).</summary>
        public int cooldownTicks  = 2500;
        public int lastFiredTick  = -999999;

        // ── Fire limits ───────────────────────────────────────────────────────
        /// <summary>If true, this rule disables itself after firing once.</summary>
        public bool oneShotRule   = false;

        /// <summary>Maximum total fires (0 = unlimited).</summary>
        public int maxFires       = 0;

        /// <summary>Total times this rule has fired (persisted to save).</summary>
        public int totalFireCount = 0;

        // ── Runtime state (not saved) ─────────────────────────────────────────
        [System.NonSerialized] public int  sessionFireCount = 0;
        [System.NonSerialized] public bool lastEvaluationResult = false;

        // ── Convenience accessors (backward compat with single-trigger UI) ────
        public AutomationTrigger FirstTrigger
        {
            get => triggerEntries.Count > 0 ? triggerEntries[0].trigger : null;
            set
            {
                if (triggerEntries.Count == 0) triggerEntries.Add(new TriggerEntry());
                triggerEntries[0].trigger = value;
            }
        }

        public AutomationAction FirstAction
        {
            get => actions.Count > 0 ? actions[0] : null;
            set
            {
                if (actions.Count == 0) actions.Add(value);
                else actions[0] = value;
            }
        }

        // ── Evaluation ────────────────────────────────────────────────────────
        public bool CanFire(int currentTick)
        {
            if (!enabled)                      return false;
            if (triggerEntries.Count == 0)     return false;
            if (actions.Count == 0)            return false;
            if ((currentTick - lastFiredTick) < cooldownTicks) return false;
            if (maxFires > 0 && totalFireCount >= maxFires)    return false;
            return true;
        }

        public bool EvaluateTriggers(Map map)
        {
            if (triggerMode == TriggerMode.All)
                return triggerEntries.All(e => e.Evaluate(map));
            else
                return triggerEntries.Any(e => e.Evaluate(map));
        }

        /// <summary>
        /// Evaluate triggers; if conditions are met, execute all actions in order.
        /// Returns true if any actions executed.
        /// </summary>
        public bool TryFire(Map map, int currentTick)
        {
            if (!CanFire(currentTick)) return false;

            bool triggered = EvaluateTriggers(map);
            lastEvaluationResult = triggered;
            if (!triggered) return false;

            foreach (AutomationAction action in actions)
            {
                try   { action.Execute(map); }
                catch (Exception ex)
                {
                    Log.Error($"[IFTTT] Action '{action.Label}' in rule '{name}' threw: {ex}");
                }
            }

            lastFiredTick = currentTick;
            totalFireCount++;
            sessionFireCount++;

            Log.Message($"[IFTTT] Rule '{name}' fired (total: {totalFireCount}).");

            if (oneShotRule) enabled = false;

            return true;
        }

        // ── IExposable ────────────────────────────────────────────────────────
        public void ExposeData()
        {
            Scribe_Values.Look(ref name,                "name",                "Unnamed Rule");
            Scribe_Values.Look(ref notes,               "notes",               "");
            Scribe_Values.Look(ref enabled,             "enabled",             true);
            Scribe_Values.Look(ref category,            "category",            RuleCategory.Custom);
            Scribe_Values.Look(ref priority,            "priority",            50);
            Scribe_Values.Look(ref triggerMode,         "triggerMode",         TriggerMode.All);
            Scribe_Values.Look(ref checkFrequencyTicks, "checkFrequencyTicks", 2500);
            Scribe_Values.Look(ref lastCheckedTick,     "lastCheckedTick",     -999999);
            Scribe_Values.Look(ref cooldownTicks,       "cooldownTicks",       2500);
            Scribe_Values.Look(ref lastFiredTick,       "lastFiredTick",       -999999);
            Scribe_Values.Look(ref oneShotRule,         "oneShotRule",         false);
            Scribe_Values.Look(ref maxFires,            "maxFires",            0);
            Scribe_Values.Look(ref totalFireCount,      "totalFireCount",      0);

            Scribe_Collections.Look(ref triggerEntries, "triggerEntries", LookMode.Deep);
            Scribe_Collections.Look(ref actions,        "actions",        LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                triggerEntries ??= new List<TriggerEntry>();
                actions        ??= new List<AutomationAction>();
            }
        }

        public override string ToString() => $"Rule({name}, cat={category}, pri={priority})";
    }

    // ── TriggerEntry ─────────────────────────────────────────────────────────
    /// <summary>
    /// Wraps a trigger inside a rule list.  The `negate` flag inverts the result
    /// so you can express "NOT under attack" conditions.
    /// </summary>
    public class TriggerEntry : IExposable
    {
        public AutomationTrigger trigger;
        /// <summary>If true, the trigger result is inverted (logical NOT).</summary>
        public bool negate = false;

        public bool Evaluate(Map map)
        {
            if (trigger == null) return false;
            bool result = trigger.IsTriggered(map);
            return negate ? !result : result;
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref trigger, "trigger");
            Scribe_Values.Look(ref negate, "negate", false);
        }
    }
}
