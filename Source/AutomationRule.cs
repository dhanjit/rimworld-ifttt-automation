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

        // ── Per-action guard conditions ───────────────────────────────────────
        /// <summary>
        /// Parallel list to <see cref="actions"/>: actionGuards[i] is an optional
        /// condition that must be true for actions[i] to execute.
        /// null at index i means "no guard — always execute".
        /// </summary>
        public List<AutomationTrigger> actionGuards = new List<AutomationTrigger>();

        /// <summary>Returns the guard trigger for action at index i, or null if none.</summary>
        public AutomationTrigger GetActionGuard(int i)
            => (i >= 0 && i < actionGuards.Count) ? actionGuards[i] : null;

        /// <summary>Sets the guard trigger for action at index i (expanding with nulls if needed).</summary>
        public void SetActionGuard(int i, AutomationTrigger guard)
        {
            while (actionGuards.Count <= i) actionGuards.Add(null);
            actionGuards[i] = guard;
        }

        // ── ELSE actions (execute when triggers DON'T match) ─────────────────
        public List<AutomationAction>  elseActions      = new List<AutomationAction>();
        public List<AutomationTrigger> elseActionGuards = new List<AutomationTrigger>();

        public AutomationTrigger GetElseActionGuard(int i)
            => (i >= 0 && i < elseActionGuards.Count) ? elseActionGuards[i] : null;

        public void SetElseActionGuard(int i, AutomationTrigger guard)
        {
            while (elseActionGuards.Count <= i) elseActionGuards.Add(null);
            elseActionGuards[i] = guard;
        }

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

        // ── Map scope ─────────────────────────────────────────────────────────
        /// <summary>Which maps this rule evaluates against (default: primary home map).</summary>
        public RuleMapScope mapScope        = RuleMapScope.AnyHomeMap;
        /// <summary>World tile index used when mapScope == SpecificMap. -1 = not set.</summary>
        public int          specificMapTile = -1;

        /// <summary>
        /// Per-tile last-fired tick for AllHomeMaps scope.
        /// Not persisted — resets to empty on every load (acceptable; avoids save bloat).
        /// </summary>
        [System.NonSerialized]
        private Dictionary<int, int> _perTileLastFired;

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
            if (!enabled)                                              return false;
            if (!triggerGroups.Any(g => g.triggers.Count > 0))       return false;
            if (actions.Count == 0 && elseActions.Count == 0)         return false;
            if ((currentTick - lastFiredTick) < cooldownTicks)        return false;
            if (maxFires > 0 && totalFireCount >= maxFires)            return false;
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

            // Decide which branch to run: THEN (triggered) or ELSE (!triggered)
            List<AutomationAction>  activeActions;
            List<AutomationTrigger> activeGuards;
            string branchLabel;

            if (triggered && actions.Count > 0)
            {
                activeActions = actions;
                activeGuards  = actionGuards;
                branchLabel   = "THEN";
            }
            else if (!triggered && elseActions.Count > 0)
            {
                activeActions = elseActions;
                activeGuards  = elseActionGuards;
                branchLabel   = "ELSE";
            }
            else
            {
                return false; // no applicable branch
            }

            var (anyFailed, failedLabel) = ExecuteActionList(map, activeActions, activeGuards);

            lastFiredTick = currentTick;
            totalFireCount++;
            sessionFireCount++;

            if (AutomationGameComp.Instance?.verboseLogging == true)
                Log.Message($"[IFTTT] Rule '{name}' fired ({branchLabel}, total: {totalFireCount}).");

            if (oneShotRule) enabled = false;

            // ── Player notifications ───────────────────────────────────────
            if (notifyOnFire)
            {
                string suffix = branchLabel == "ELSE" ? " (else)" : "";
                Messages.Message($"[IFTTT] '{name}' fired{suffix}.", MessageTypeDefOf.TaskCompletion, false);
            }
            if (notifyOnFailure && anyFailed)
                Messages.Message(
                    $"[IFTTT] '{name}': '{failedLabel}' had nothing to do.",
                    MessageTypeDefOf.CautionInput, false);

            return true;
        }

        // ── AllHomeMaps per-tile firing ───────────────────────────────────────

        /// <summary>
        /// Checks whether this rule is allowed to fire on a specific world tile in AllHomeMaps mode.
        /// Respects global enabled/limits and per-tile cooldown (does NOT check global cooldown).
        /// </summary>
        public bool CanFireOnTile(int currentTick, int tile)
        {
            if (!enabled)                                              return false;
            if (!triggerGroups.Any(g => g.triggers.Count > 0))       return false;
            if (actions.Count == 0 && elseActions.Count == 0)         return false;
            if (maxFires > 0 && totalFireCount >= maxFires)            return false;

            _perTileLastFired ??= new Dictionary<int, int>();
            if (_perTileLastFired.TryGetValue(tile, out int last))
                if ((currentTick - last) < cooldownTicks) return false;

            return true;
        }

        /// <summary>
        /// Fires this rule against a specific map using per-tile cooldown tracking.
        /// Used by AutomationGameComp for AllHomeMaps scope.
        /// Returns true if any actions were executed.
        /// </summary>
        public bool TryFireOnTile(Map map, int currentTick, int tile)
        {
            if (!CanFireOnTile(currentTick, tile)) return false;

            bool triggered = EvaluateTriggers(map);
            lastEvaluationResult = triggered;

            List<AutomationAction>  activeActions;
            List<AutomationTrigger> activeGuards;
            string branchLabel;

            if (triggered && actions.Count > 0)
            {
                activeActions = actions;
                activeGuards  = actionGuards;
                branchLabel   = "THEN";
            }
            else if (!triggered && elseActions.Count > 0)
            {
                activeActions = elseActions;
                activeGuards  = elseActionGuards;
                branchLabel   = "ELSE";
            }
            else
            {
                return false;
            }

            var (anyFailed, failedLabel) = ExecuteActionList(map, activeActions, activeGuards);

            // Update per-tile cooldown (NOT the global lastFiredTick — that stays for AnyHomeMap/SpecificMap)
            _perTileLastFired ??= new Dictionary<int, int>();
            _perTileLastFired[tile] = currentTick;

            totalFireCount++;
            sessionFireCount++;

            if (AutomationGameComp.Instance?.verboseLogging == true)
            {
                string mapLabel = map.Parent?.Label ?? $"tile {tile}";
                Log.Message($"[IFTTT] Rule '{name}' fired on {mapLabel} ({branchLabel}, total: {totalFireCount}).");
            }

            if (oneShotRule) enabled = false;

            if (notifyOnFire)
            {
                string suffix   = branchLabel == "ELSE" ? " (else)" : "";
                string mapLabel = map.Parent?.Label ?? $"tile {tile}";
                Messages.Message($"[IFTTT] '{name}' fired{suffix} on {mapLabel}.",
                    MessageTypeDefOf.TaskCompletion, false);
            }
            if (notifyOnFailure && anyFailed)
                Messages.Message(
                    $"[IFTTT] '{name}': '{failedLabel}' had nothing to do.",
                    MessageTypeDefOf.CautionInput, false);

            return true;
        }

        // ── Clone ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a deep configuration clone of this rule — triggers and actions are deep-copied
        /// so edits to the clone do not affect the original (and vice-versa).
        /// Runtime state (lastFiredTick, totalFireCount, etc.) is intentionally reset to defaults.
        /// </summary>
        public AutomationRule Clone()
        {
            var copy = new AutomationRule
            {
                name                = name,
                notes               = notes,
                enabled             = enabled,
                category            = category,
                priority            = priority,
                checkFrequencyTicks = checkFrequencyTicks,
                cooldownTicks       = cooldownTicks,
                oneShotRule         = oneShotRule,
                maxFires            = maxFires,
                notifyOnFire        = notifyOnFire,
                notifyOnFailure     = notifyOnFailure,
                mapScope            = mapScope,
                specificMapTile     = specificMapTile,
                // Runtime state deliberately NOT copied (start fresh)
            };

            // Deep-copy trigger groups
            foreach (var grp in triggerGroups)
            {
                var newGrp = new TriggerGroup { label = grp.label, mode = grp.mode };
                foreach (var e in grp.triggers)
                    newGrp.triggers.Add(new TriggerEntry { trigger = e.trigger?.Clone(), negate = e.negate });
                copy.triggerGroups.Add(newGrp);
            }

            // Deep-copy THEN actions + guards
            foreach (var a in actions)
                copy.actions.Add(a?.Clone());
            for (int i = 0; i < actions.Count; i++)
                copy.SetActionGuard(i, GetActionGuard(i)?.Clone());

            // Deep-copy ELSE actions + guards
            foreach (var a in elseActions)
                copy.elseActions.Add(a?.Clone());
            for (int i = 0; i < elseActions.Count; i++)
                copy.SetElseActionGuard(i, GetElseActionGuard(i)?.Clone());

            return copy;
        }

        /// <summary>
        /// Executes a list of actions with their parallel guard triggers.
        /// Returns (anyFailed, firstFailedLabel) for notification purposes.
        /// </summary>
        private (bool anyFailed, string failedLabel) ExecuteActionList(
            Map map, List<AutomationAction> actionList, List<AutomationTrigger> guardList)
        {
            bool   anyFailed   = false;
            string failedLabel = null;

            for (int ai = 0; ai < actionList.Count; ai++)
            {
                AutomationAction action = actionList[ai];
                try
                {
                    AutomationTrigger guard = (ai >= 0 && ai < guardList.Count) ? guardList[ai] : null;
                    if (guard != null && !guard.IsTriggered(map))
                        continue;

                    bool ok = action.Execute(map);
                    if (!ok && !anyFailed)
                    {
                        anyFailed   = true;
                        failedLabel = action.Label;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[IFTTT] Action '{action.Label}' in rule '{name}' threw: {ex}");
                    if (!anyFailed)
                    {
                        anyFailed   = true;
                        failedLabel = action.Label;
                    }
                }
            }
            return (anyFailed, failedLabel);
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

            // ── v2.2: map scope ─────────────────────────────────────────────
            Scribe_Values.Look(ref mapScope,        "mapScope",        RuleMapScope.AnyHomeMap);
            Scribe_Values.Look(ref specificMapTile, "specificMapTile", -1);

            // ── v2: trigger groups ──────────────────────────────────────────
            Scribe_Collections.Look(ref triggerGroups, "triggerGroups", LookMode.Deep);

            // ── v1 migration: load old flat trigger list (only in old saves) ─
            List<TriggerEntry> legacyEntries = null;
            TriggerMode        legacyMode    = TriggerMode.All;
            Scribe_Collections.Look(ref legacyEntries, "triggerEntries", LookMode.Deep);
            Scribe_Values.Look(ref legacyMode,         "triggerMode",    TriggerMode.All);

            // ── Actions ─────────────────────────────────────────────────────
            Scribe_Collections.Look(ref actions,           "actions",           LookMode.Deep);
            Scribe_Collections.Look(ref actionGuards,      "actionGuards",      LookMode.Deep);
            Scribe_Collections.Look(ref elseActions,       "elseActions",       LookMode.Deep);
            Scribe_Collections.Look(ref elseActionGuards,  "elseActionGuards",  LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                triggerGroups    ??= new List<TriggerGroup>();
                actions          ??= new List<AutomationAction>();
                actionGuards     ??= new List<AutomationTrigger>();
                elseActions      ??= new List<AutomationAction>();
                elseActionGuards ??= new List<AutomationTrigger>();

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
