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
    ///  - Supports multi-settlement (AnyHomeMap / AllHomeMaps / SpecificMap scope).
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

            // Collect all player home maps once (avoids repeated LINQ over Find.Maps).
            // Materialise to a List so we can safely iterate.
            var homeMaps = Find.Maps
                .Where(m => m.IsPlayerHome)
                .ToList();

            if (homeMaps.Count == 0) return;

            foreach (AutomationRule rule in RulesSortedByPriority)
            {
                // Per-rule frequency gate: skip until enough time has elapsed.
                if (!rule.IsDueForCheck(tick))
                {
                    if (verboseLogging)
                        Log.Message($"[IFTTT][v] Skipping '{rule.name}' (next check in " +
                                    $"{rule.checkFrequencyTicks - (tick - rule.lastCheckedTick)} ticks).");
                    continue;
                }

                // Mark as checked now, regardless of whether it fires.
                rule.lastCheckedTick = tick;

                try
                {
                    EvaluateRuleOnMaps(rule, homeMaps, tick);
                }
                catch (Exception ex)
                {
                    Log.Error($"[IFTTT] Exception evaluating rule '{rule.name}': {ex}");
                }
            }
        }

        /// <summary>
        /// Dispatches rule evaluation to the correct map(s) based on <see cref="RuleMapScope"/>.
        /// </summary>
        private void EvaluateRuleOnMaps(AutomationRule rule, List<Map> homeMaps, int tick)
        {
            switch (rule.mapScope)
            {
                // ── AnyHomeMap (legacy / default) ─────────────────────────────
                // Fire on the "primary" home map — the one RimWorld considers first.
                // Shares a single global cooldown. Backward-compatible with single-colony saves.
                case RuleMapScope.AnyHomeMap:
                {
                    Map map = Find.AnyPlayerHomeMap;
                    if (map == null) return;

                    if (verboseLogging)
                        Log.Message($"[IFTTT][v] Evaluating '{rule.name}' (AnyHomeMap)...");

                    if (rule.TryFire(map, tick))
                        LogEvent(rule, tick, map);
                    break;
                }

                // ── AllHomeMaps ───────────────────────────────────────────────
                // Evaluate independently on every loaded player home map.
                // Each settlement has its own per-tile cooldown.
                case RuleMapScope.AllHomeMaps:
                {
                    if (verboseLogging)
                        Log.Message($"[IFTTT][v] Evaluating '{rule.name}' (AllHomeMaps, {homeMaps.Count} maps)...");

                    foreach (Map map in homeMaps)
                    {
                        try
                        {
                            if (rule.TryFireOnTile(map, tick, map.Tile))
                                LogEvent(rule, tick, map);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[IFTTT] Exception evaluating rule '{rule.name}' on tile {map.Tile}: {ex}");
                        }
                    }
                    break;
                }

                // ── SpecificMap ───────────────────────────────────────────────
                // Fire only on the settlement with the configured world tile.
                // Silently skips if that tile's map isn't currently loaded.
                case RuleMapScope.SpecificMap:
                {
                    Map map = homeMaps.FirstOrDefault(m => m.Tile == rule.specificMapTile);
                    if (map == null)
                    {
                        if (verboseLogging)
                            Log.Message($"[IFTTT][v] Rule '{rule.name}' (SpecificMap tile {rule.specificMapTile}): map not loaded, skipping.");
                        return;
                    }

                    if (verboseLogging)
                    {
                        string mapLabel = map.Parent?.Label ?? $"tile {map.Tile}";
                        Log.Message($"[IFTTT][v] Evaluating '{rule.name}' (SpecificMap: {mapLabel})...");
                    }

                    if (rule.TryFire(map, tick))
                        LogEvent(rule, tick, map);
                    break;
                }
            }
        }

        private void LogEvent(AutomationRule rule, int tick, Map map = null)
        {
            string mapName = map?.Parent?.Label
                ?? (map != null ? $"tile {map.Tile}" : null);

            recentEvents.Add(new RuleFireEvent
            {
                ruleName  = rule.name,
                category  = rule.category,
                tick      = tick,
                timestamp = DateTime.Now.ToString("HH:mm:ss"),
                mapName   = mapName,
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

        /// <summary>
        /// Duplicates a rule by creating a deep clone via <see cref="AutomationRule.Clone"/>.
        /// The copy starts disabled and with reset fire counters/timestamps.
        /// Inserted immediately after the source in the list.
        /// </summary>
        public void DuplicateRule(AutomationRule src)
        {
            AutomationRule copy = src.Clone();
            copy.name           = src.name + " (copy)";
            copy.enabled        = false;
            // Runtime state already zeroed by Clone(); explicit reset for safety:
            copy.totalFireCount  = 0;
            copy.sessionFireCount = 0;
            copy.lastFiredTick   = -999999;
            copy.lastCheckedTick = -999999;

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
        /// <summary>Settlement name (or tile reference) for multi-map logging. Null in single-map mode.</summary>
        public string       mapName;
    }
}
