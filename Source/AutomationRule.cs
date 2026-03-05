using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT
{
    /// <summary>
    /// A trigger group: a named set of triggers combined with AND (All) or OR (Any) logic.
    ///
    /// Multiple groups inside a rule are always ANDed at the top level:
    ///   Rule fires when: Group1.Evaluate() AND Group2.Evaluate() AND ...
    ///
    /// This supports nested boolean logic without a full expression tree:
    ///   (X AND Y) AND (Z OR W)   →  two groups: [ALL: X,Y] AND [ANY: Z,W]
    ///   X AND (Y OR Z)           →  two groups: [ALL: X]   AND [ANY: Y,Z]
    /// </summary>
    public class TriggerGroup : IExposable
    {
        /// <summary>Optional player-facing label shown in the UI header.</summary>
        public string label = "";

        /// <summary>How triggers inside this group are combined: All (AND) or Any (OR).</summary>
        public TriggerMode mode = TriggerMode.All;

        public List<TriggerEntry> triggers = new List<TriggerEntry>();

        /// <summary>
        /// Evaluates all triggers in this group according to its mode.
        /// An empty group returns true (pass-through — does not block the rule).
        /// </summary>
        public bool Evaluate(Map map)
        {
            if (triggers.Count == 0) return true;
            return mode == TriggerMode.All
                ? triggers.All(e => e.Evaluate(map))
                : triggers.Any(e => e.Evaluate(map));
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label", "");
            Scribe_Values.Look(ref mode,  "mode",  TriggerMode.All);
            Scribe_Collections.Look(ref triggers, "triggers", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                triggers ??= new List<TriggerEntry>();
        }
    }

    /// <summary>
    /// An automation rule: [IF groups] THEN [actions].
    ///
    /// Groups are ANDed at the top level; each group has its own AND/OR mode.
    /// Supports: (X AND Y) AND (Z OR W), (X OR Y) AND Z, etc.
    ///
    /// Features:
    ///   • Multiple trigger groups — always AND between groups
    ///   • Per-group AND/OR mode for triggers within that group
    ///   • Per-trigger negation (NOT)
    ///   • Multiple sequenced actions
    ///   • Per-rule check frequency, cooldown, priority, one-shot, max-fires cap
    ///   • Save-compatible migration from v1 flat trigger list
    /// </summary>
    public class AutomationRule : IExposable
    {
        // ── Identity ──────────────────────────────────────────────────────────
        public string       name     = "Unnamed Rule";
        public string       notes    = "";
        public bool         enabled  = true;
        public RuleCategory category = RuleCategory.Custom;

        /// <summary>Lower number = evaluated first. Useful when rules interact.</summary>
        public int priority = 50;

        // ── Trigger groups ────────────────────────────────────────────────────
        /// <summary>
        /// All groups must evaluate to true for the rule to fire (AND between groups).
        /// Each group combines its own triggers with its configured AND/OR mode.
        /// </summary>
        public List<TriggerGroup> triggerGroups = new List<TriggerGroup>();

        // ── Actions ───────────────────────────────────────────────────────────
        public List<AutomationAction> actions = new List<AutomationAction>();

        // ── Check frequency ───────────────────────────────────────────────────
        public int checkFrequencyTicks = 2500;
        public int lastCheckedTick     = -999999;

        public bool IsDueForCheck(int currentTick)
            => (currentTick - lastCheckedTick) >= checkFrequencyTicks;

        // ── Cooldown ──────────────────────────────────────────────────────────
        public int cooldownTicks = 2500;
        public int lastFiredTick = -999999;

        // ── Fire limits ───────────────────────────────────────────────────────
        public bool oneShotRule    = false;
        public int  maxFires       = 0;
        public int  totalFireCount = 0;

        // ── Notifications ──────────────────────────────────────────────────────
        /// <summary>Show a banner message whenever this rule fires successfully.</summary>
        public bool notifyOnFire    = false;
        /// <summary>Show a warning message when an action has nothing to do (returns false).</summary>
        public bool notifyOnFailure = false;

        // ── Runtime state (not persisted) ────────────────────────────────────
        [System.NonSerialized] public int  sessionFireCount     = 0;
        [System.NonSerialized] public bool lastEvaluationResult = false;

        // ── Convenience helpers ───────────────────────────────────────────────

        /// <summary>
        /// Adds a trigger to the first group, creating the group if the list is empty.
        /// Convenience for tests and simple single-group rules.
        /// </summary>
        public void AddTrigger(AutomationTrigger trigger, bool negate = false)
        {
            if (triggerGroups.Count == 0)
                triggerGroups.Add(new TriggerGroup());
            triggerGroups[0].triggers.Add(new TriggerEntry { trigger = trigger, negate = negate });
        }

        /// <summary>Gets/sets the AND/OR mode of the first trigger group.</summary>
        public TriggerMode FirstGroupMode
        {
            get => triggerGroups.Count > 0 ? triggerGroups[0].mode : TriggerMode.All;
            set
            {
                if (triggerGroups.Count == 0) triggerGroups.Add(new TriggerGroup());
                triggerGroups[0].mode = value;
            }
        }

        /// <summary>Backward-compat: get/set the first trigger in the first group.</summary>
        public AutomationTrigger FirstTrigger
        {
            get => triggerGroups.Count > 0 && triggerGroups[0].triggers.Count > 0
                ? triggerGroups[0].triggers[0].trigger : null;
            set
            {
                if (triggerGroups.Count == 0) triggerGroups.Add(new TriggerGroup());
                if (triggerGroups[0].triggers.Count == 0)
                    triggerGroups[0].triggers.Add(new TriggerEntry());
                triggerGroups[0].triggers[0].trigger = value;
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
            if (!enabled)                                          return false;
            if (!triggerGroups.Any(g => g.triggers.Count > 0))   return false;
            if (actions.Count == 0)                               return false;
            if ((currentTick - lastFiredTick) < cooldownTicks)    return false;
            if (maxFires > 0 && totalFireCount >= maxFires)        return false;
            return true;
        }

        /// <summary>
        /// ALL groups must evaluate true (AND between groups).
        /// Materialises collection to avoid modification-during-iteration.
        /// </summary>
        public bool EvaluateTriggers(Map map)
        {
            if (triggerGroups.Count == 0) return false;
            return triggerGroups.ToList().All(g => g.Evaluate(map));
        }

        public bool TryFire(Map map, int currentTick)
        {
            if (!CanFire(currentTick)) return false;

            bool triggered = EvaluateTriggers(map);
            lastEvaluationResult = triggered;
            if (!triggered) return false;

            // Execute actions, tracking which ones had nothing to do.
            bool   anyActionFailed   = false;
            string failedActionLabel = null;
            foreach (AutomationAction action in actions)
            {
                try
                {
                    bool ok = action.Execute(map);
                    if (!ok && !anyActionFailed)
                    {
                        anyActionFailed   = true;
                        failedActionLabel = action.Label;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[IFTTT] Action '{action.Label}' in rule '{name}' threw: {ex}");
                    if (!anyActionFailed)
                    {
                        anyActionFailed   = true;
                        failedActionLabel = action.Label;
                    }
                }
            }

            lastFiredTick = currentTick;
            totalFireCount++;
            sessionFireCount++;

            if (AutomationGameComp.Instance?.verboseLogging == true)
                Log.Message($"[IFTTT] Rule '{name}' fired (total: {totalFireCount}).");

            if (oneShotRule) enabled = false;

            // ── Player notifications ───────────────────────────────────────
            if (notifyOnFire)
                Messages.Message($"[IFTTT] '{name}' fired.", MessageTypeDefOf.TaskCompletion, false);
            if (notifyOnFailure && anyActionFailed)
                Messages.Message(
                    $"[IFTTT] '{name}': '{failedActionLabel}' had nothing to do.",
                    MessageTypeDefOf.CautionInput, false);

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
            Scribe_Values.Look(ref checkFrequencyTicks, "checkFrequencyTicks", 2500);
            Scribe_Values.Look(ref lastCheckedTick,     "lastCheckedTick",     -999999);
            Scribe_Values.Look(ref cooldownTicks,       "cooldownTicks",       2500);
            Scribe_Values.Look(ref lastFiredTick,       "lastFiredTick",       -999999);
            Scribe_Values.Look(ref oneShotRule,         "oneShotRule",         false);
            Scribe_Values.Look(ref maxFires,            "maxFires",            0);
            Scribe_Values.Look(ref totalFireCount,      "totalFireCount",      0);
            Scribe_Values.Look(ref notifyOnFire,        "notifyOnFire",        false);
            Scribe_Values.Look(ref notifyOnFailure,     "notifyOnFailure",     false);

            // ── v2: trigger groups ──────────────────────────────────────────
            Scribe_Collections.Look(ref triggerGroups, "triggerGroups", LookMode.Deep);

            // ── v1 migration: load old flat trigger list (only in old saves) ─
            List<TriggerEntry> legacyEntries = null;
            TriggerMode        legacyMode    = TriggerMode.All;
            Scribe_Collections.Look(ref legacyEntries, "triggerEntries", LookMode.Deep);
            Scribe_Values.Look(ref legacyMode,         "triggerMode",    TriggerMode.All);

            // ── Actions ─────────────────────────────────────────────────────
            Scribe_Collections.Look(ref actions, "actions", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                triggerGroups ??= new List<TriggerGroup>();
                actions       ??= new List<AutomationAction>();

                // One-time migration: flat list → single group
                if (triggerGroups.Count == 0 && legacyEntries?.Count > 0)
                {
                    var group = new TriggerGroup { mode = legacyMode };
                    foreach (var e in legacyEntries)
                        if (e != null) group.triggers.Add(e);
                    triggerGroups.Add(group);
                    Log.Message($"[IFTTT] Migrated rule '{name}' from v1 flat list → group format.");
                }
            }
        }

        public override string ToString() => $"Rule({name}, cat={category}, pri={priority})";
    }

    // ── TriggerEntry ──────────────────────────────────────────────────────────
    /// <summary>
    /// Wraps a trigger inside a group. The `negate` flag inverts the result
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
