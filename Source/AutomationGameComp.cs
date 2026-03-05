using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWorldIFTTT
{
    /// <summary>
    /// Heart of the mod. GameComponent that:
    ///  - Stores all automation rules per save file.
    ///  - Polls rules every checkIntervalTicks (global baseline, default 250).
    ///  - Each rule is only evaluated when its own checkFrequencyTicks has elapsed
    ///    since its last evaluation (per-rule frequency gate).
    ///  - Tracks a session log of recent firing events.
    /// </summary>
    public class AutomationGameComp : GameComponent
    {
        // ── Config ────────────────────────────────────────────────────────────
        /// <summary>
        /// Global polling resolution (ticks). The game component wakes up this
        /// often to poll rules. Individual rules gate themselves further via their
        /// own checkFrequencyTicks. Default 250 ≈ 4 real-seconds at 1× speed.
        /// </summary>
        public int checkIntervalTicks = 250;

        /// <summary>Whether to log every rule evaluation (verbose mode for debugging).</summary>
        public bool verboseLogging = false;

        // ── Data ──────────────────────────────────────────────────────────────
        public List<AutomationRule> rules = new List<AutomationRule>();

        // ── Session log (not saved) ───────────────────────────────────────────
        /// <summary>Ring-buffer of recent fire events for the UI log panel.</summary>
        [System.NonSerialized]
        public readonly List<RuleFireEvent> recentEvents = new List<RuleFireEvent>();
        private const int MaxLogEvents = 100;

        // ── Constructor ───────────────────────────────────────────────────────
        public AutomationGameComp(Game game) { }

        // ── Static accessor ───────────────────────────────────────────────────
        public static AutomationGameComp Instance
            => Verse.Current.Game?.GetComponent<AutomationGameComp>();

        // ── Sorted rule view ──────────────────────────────────────────────────
        public IEnumerable<AutomationRule> RulesSortedByPriority
            => rules.OrderBy(r => r.priority);

        // ── Tick ──────────────────────────────────────────────────────────────
        public override void GameComponentTick()
        {
            int tick = Find.TickManager.TicksGame;

            // Global polling gate — cheap early-out most ticks.
            if (tick % checkIntervalTicks != 0) return;

            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;

            foreach (AutomationRule rule in RulesSortedByPriority)
            {
                // Per-rule frequency gate: skip until enough time has elapsed.
                if (!rule.IsDueForCheck(tick))
                {
                    if (verboseLogging)
                        Log.Message($"[IFTTT][v] Skipping '{rule.name}' (next check in {rule.checkFrequencyTicks - (tick - rule.lastCheckedTick)} ticks).");
                    continue;
                }

                // Mark as checked now, regardless of whether it fires.
                rule.lastCheckedTick = tick;

                try
                {
                    if (verboseLogging)
                        Log.Message($"[IFTTT][v] Evaluating rule '{rule.name}'...");

                    bool fired = rule.TryFire(map, tick);
                    if (fired)
                        LogEvent(rule, tick);
                }
                catch (Exception ex)
                {
                    Log.Error($"[IFTTT] Exception evaluating rule '{rule.name}': {ex}");
                }
            }
        }

        private void LogEvent(AutomationRule rule, int tick)
        {
            recentEvents.Add(new RuleFireEvent
            {
                ruleName  = rule.name,
                category  = rule.category,
                tick      = tick,
                timestamp = DateTime.Now.ToString("HH:mm:ss"),
            });
            if (recentEvents.Count > MaxLogEvents)
                recentEvents.RemoveAt(0);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        public List<AutomationRule> GetRulesByCategory(RuleCategory cat)
            => cat == RuleCategory.All
                ? rules
                : rules.Where(r => r.category == cat).ToList();

        public AutomationRule AddNewRule(string name = "New Rule",
            RuleCategory cat = RuleCategory.Custom)
        {
            var rule = new AutomationRule { name = name, category = cat };
            rules.Add(rule);
            return rule;
        }

        public void DuplicateRule(AutomationRule src)
        {
            var copy = new AutomationRule
            {
                name                = src.name + " (copy)",
                notes               = src.notes,
                enabled             = false,
                category            = src.category,
                priority            = src.priority,
                checkFrequencyTicks = src.checkFrequencyTicks,
                cooldownTicks       = src.cooldownTicks,
                oneShotRule         = src.oneShotRule,
                maxFires            = src.maxFires,
                notifyOnFire        = src.notifyOnFire,
                notifyOnFailure     = src.notifyOnFailure,
            };
            // Copy trigger groups (shallow-copy trigger instances; config fields are shared)
            foreach (var grp in src.triggerGroups)
            {
                var newGrp = new TriggerGroup { label = grp.label, mode = grp.mode };
                foreach (var e in grp.triggers)
                    newGrp.triggers.Add(new TriggerEntry { trigger = e.trigger, negate = e.negate });
                copy.triggerGroups.Add(newGrp);
            }
            foreach (var a in src.actions)
                copy.actions.Add(a);

            int idx = rules.IndexOf(src);
            rules.Insert(idx + 1, copy);
        }

        // ── IExposable ────────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref checkIntervalTicks, "checkIntervalTicks", 250);
            Scribe_Values.Look(ref verboseLogging,     "verboseLogging",     false);
            Scribe_Collections.Look(ref rules, "rules", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                rules ??= new List<AutomationRule>();
        }
    }

    // ── Event log entry ───────────────────────────────────────────────────────
    public struct RuleFireEvent
    {
        public string       ruleName;
        public RuleCategory category;
        public int          tick;
        public string       timestamp;
    }
}
