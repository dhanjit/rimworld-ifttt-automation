using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.UI
{
    /// <summary>
    /// Dialog for creating a new rule or editing an existing one.
    ///
    /// Layout:
    ///   Header: Name, category, priority, cooldown, enabled, one-shot, notes
    ///   IF section: TriggerMode toggle + list of TriggerEntries (add/remove/negate/config)
    ///   THEN section: ordered list of Actions (add/remove/reorder/config)
    ///   [Cancel]  [Save]
    /// </summary>
    public class Dialog_AddEditRule : Window
    {
        // ── Layout ────────────────────────────────────────────────────────────
        public override Vector2 InitialSize => new Vector2(720f, 780f);
        private Vector2 scrollPos;

        // ── Frequency presets (label, ticks) ──────────────────────────────────
        // 1 in-game hour = 2 500 ticks.  1 in-game day = 60 000 ticks.
        private static readonly (string label, int ticks)[] FrequencyPresets =
        {
            ("Every 15 min (250 ticks)",  250),
            ("Every 1 hour",              2_500),
            ("Every 2 hours",             5_000),
            ("Every 6 hours",            15_000),
            ("Every 12 hours",           30_000),
            ("Every 1 day",              60_000),
            ("Every 2 days",            120_000),
            ("Every 5 days",            300_000),
            ("Every 15 days",           900_000),
        };

        private static string FrequencyLabel(int ticks)
        {
            foreach (var (label, t) in FrequencyPresets)
                if (t == ticks) return label;
            // Custom value: express as hours
            float hours = ticks / 2500f;
            return hours >= 24f
                ? $"Every {hours / 24f:0.##} day(s)"
                : $"Every {hours:0.##} hour(s)";
        }

        // ── State ─────────────────────────────────────────────────────────────
        private readonly AutomationGameComp comp;
        private readonly AutomationRule     existingRule;

        // Working copies
        private string            ruleName;
        private string            ruleNotes;
        private bool              ruleEnabled;
        private RuleCategory      ruleCategory;
        private int               rulePriority;
        private string            priorityBuf;
        private TriggerMode       triggerMode;
        private int               checkFrequencyTicks;    // per-rule evaluation rate
        private int               cooldownHours;
        private string            cooldownBuf;
        private bool              oneShotRule;
        private int               maxFires;
        private string            maxFiresBuf;

        // Mutable working lists
        private List<TriggerEntry>      triggerEntries;
        private List<AutomationAction>  actions;

        // ── Constructors ──────────────────────────────────────────────────────
        public Dialog_AddEditRule(AutomationGameComp comp)
        {
            this.comp         = comp;
            this.existingRule = null;

            ruleName            = "My Rule";
            ruleNotes           = "";
            ruleEnabled         = true;
            ruleCategory        = RuleCategory.Custom;
            rulePriority        = 50;
            priorityBuf         = "50";
            triggerMode         = TriggerMode.All;
            checkFrequencyTicks = 2500;   // default: check every 1 in-game hour
            cooldownHours       = 1;
            cooldownBuf         = "1";
            oneShotRule         = false;
            maxFires            = 0;
            maxFiresBuf         = "0";

            triggerEntries = new List<TriggerEntry>();
            actions        = new List<AutomationAction>();

            doCloseButton           = false;
            absorbInputAroundWindow = true;
        }

        public Dialog_AddEditRule(AutomationGameComp comp, AutomationRule rule)
        {
            this.comp         = comp;
            this.existingRule = rule;

            ruleName            = rule.name;
            ruleNotes           = rule.notes;
            ruleEnabled         = rule.enabled;
            ruleCategory        = rule.category;
            rulePriority        = rule.priority;
            priorityBuf         = rule.priority.ToString();
            triggerMode         = rule.triggerMode;
            checkFrequencyTicks = rule.checkFrequencyTicks;
            cooldownHours       = rule.cooldownTicks / 2500;
            cooldownBuf         = cooldownHours.ToString();
            oneShotRule         = rule.oneShotRule;
            maxFires            = rule.maxFires;
            maxFiresBuf         = rule.maxFires.ToString();

            // Deep-copy trigger entries and actions so Cancel works correctly
            triggerEntries = rule.triggerEntries
                .Select(e => new TriggerEntry { trigger = e.trigger, negate = e.negate })
                .ToList();
            actions = new List<AutomationAction>(rule.actions);

            doCloseButton           = false;
            absorbInputAroundWindow = true;
        }

        // ── DoWindowContents ──────────────────────────────────────────────────
        public override void DoWindowContents(Rect inRect)
        {
            string title = existingRule == null ? "Add Automation Rule" : $"Edit: {existingRule.name}";
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 32f), title);
            Text.Font = GameFont.Small;

            // Content area (scrollable)
            float contentY = 38f;
            float btnH     = 42f;
            Rect contentRect = new Rect(0, contentY, inRect.width, inRect.height - contentY - btnH);

            // Compute scroll-view height accurately so nothing is clipped.
            // Settings block: ~520px covers all fields (name, notes, frequency, cooldown,
            //   priority, maxFires, enabled, oneshot, category, triggerMode).
            // Each trigger/action entry: 70px header + ConfigHeight (0 if no config) + 2px gap.
            // Two section-header bars (22px each) + two "+ Add" buttons (32px each).
            float triggersH = 0f;
            foreach (var e in triggerEntries)
                triggersH += 70f + (e.trigger?.HasConfig == true ? e.trigger.ConfigHeight : 0f) + 2f;
            float actionsH = 0f;
            foreach (var a in actions)
                actionsH += 70f + (a.HasConfig ? a.ConfigHeight : 0f) + 2f;
            float innerH = 520f           // settings block
                         + 44f            // two section headers
                         + triggersH
                         + 32f            // "+ Add Trigger" button
                         + actionsH
                         + 32f;           // "+ Add Action" button
            Rect viewRect = new Rect(0, 0, contentRect.width - 20f, innerH);

            Widgets.BeginScrollView(contentRect, ref scrollPos, viewRect);

            float y = 0f;
            float w = viewRect.width;

            // ── Settings ──────────────────────────────────────────────────────
            Listing_Standard ls = new Listing_Standard();
            // Use a large height so the listing is never artificially capped inside the scroll view;
            // CurHeight still reports exact space consumed and advances y correctly.
            ls.Begin(new Rect(0, y, w, 2000f));

            ls.Label("Rule name:");
            ruleName = ls.TextEntry(ruleName);
            ls.Gap(2f);

            ls.Label("Notes / description:");
            ruleNotes = ls.TextEntry(ruleNotes);
            ls.Gap(4f);

            // ── Check frequency (how often triggers are evaluated) ─────────────
            ls.Label($"Check frequency:  <b>{FrequencyLabel(checkFrequencyTicks)}</b>  " +
                     "(how often triggers are evaluated)");
            if (ls.ButtonText($"Change frequency → {FrequencyLabel(checkFrequencyTicks)}"))
                OpenFrequencyPicker();

            ls.Gap(2f);
            ls.TextFieldNumericLabeled("Cooldown (hours between firings): ", ref cooldownHours, ref cooldownBuf, 0, 9999);
            ls.TextFieldNumericLabeled("Priority (lower fires first): ", ref rulePriority, ref priorityBuf, 0, 999);
            ls.TextFieldNumericLabeled("Max fires (0 = unlimited): ", ref maxFires, ref maxFiresBuf, 0, 99999);

            ls.CheckboxLabeled("Enabled",            ref ruleEnabled);
            ls.CheckboxLabeled("One-shot (disables itself after firing once)", ref oneShotRule);

            ls.Gap(4f);
            ls.Label($"Category: {ruleCategory}");
            DrawCategoryButtons(ls);

            ls.Gap(4f);
            ls.Label($"Trigger mode: {(triggerMode == TriggerMode.All ? "ALL (AND)" : "ANY (OR)")}");
            if (ls.ButtonText(triggerMode == TriggerMode.All ? "Switch to ANY (OR)" : "Switch to ALL (AND)"))
                triggerMode = triggerMode == TriggerMode.All ? TriggerMode.Any : TriggerMode.All;

            y += ls.CurHeight;
            ls.End();

            // ── Trigger list ──────────────────────────────────────────────────
            y = DrawSectionHeader(y, w, "IF (Triggers):");

            TriggerEntry toRemoveTrigger = null;
            for (int i = 0; i < triggerEntries.Count; i++)
            {
                TriggerEntry entry  = triggerEntries[i];
                float        entryH = 70f + (entry.trigger?.HasConfig == true ? entry.trigger.ConfigHeight : 0f);
                Rect         entryR = new Rect(0, y, w, entryH);
                if (i % 2 == 0) Widgets.DrawAltRect(entryR);

                float ex = 4f;
                // NOT checkbox
                Widgets.CheckboxLabeled(new Rect(ex, y + 4f, 60f, 22f), "NOT", ref entry.negate);
                ex += 68f;

                // Trigger type dropdown
                string trigLabel = entry.trigger != null
                    ? TriggerRegistry.GetLabel(entry.trigger.GetType())
                    : "-- select --";
                if (Widgets.ButtonText(new Rect(ex, y + 4f, 200f, 24f), trigLabel))
                    OpenTriggerPicker(entry);
                ex += 208f;

                // Remove button
                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (Widgets.ButtonText(new Rect(w - 64f, y + 4f, 60f, 24f), "Remove"))
                    toRemoveTrigger = entry;
                GUI.color = Color.white;

                // Config (inline)
                if (entry.trigger?.HasConfig == true)
                {
                    Rect cfgRect = new Rect(4f, y + 32f, w - 8f, entryH - 36f);
                    Listing_Standard cfgLs = new Listing_Standard();
                    cfgLs.Begin(cfgRect);
                    entry.trigger.DrawConfig(cfgLs);
                    cfgLs.End();
                }

                y += entryH + 2f;
            }
            if (toRemoveTrigger != null) triggerEntries.Remove(toRemoveTrigger);

            // Add trigger button
            if (Widgets.ButtonText(new Rect(4f, y, 160f, 26f), "+ Add Trigger"))
                OpenTriggerPicker(null);
            y += 32f;

            // ── Action list ───────────────────────────────────────────────────
            y = DrawSectionHeader(y, w, "THEN (Actions — executed in order):");

            AutomationAction toRemoveAction = null;
            int moveUpIdx = -1, moveDownIdx = -1;
            for (int i = 0; i < actions.Count; i++)
            {
                AutomationAction act    = actions[i];
                float            actH  = 70f + (act.HasConfig ? act.ConfigHeight : 0f);
                Rect             actR  = new Rect(0, y, w, actH);
                if (i % 2 == 0) Widgets.DrawAltRect(actR);

                // Order buttons
                if (i > 0 && Widgets.ButtonText(new Rect(4f, y + 4f, 24f, 22f), "^"))
                    moveUpIdx = i;
                if (i < actions.Count - 1 && Widgets.ButtonText(new Rect(30f, y + 4f, 24f, 22f), "v"))
                    moveDownIdx = i;

                // Action type dropdown
                string actLabel = act != null
                    ? ActionRegistry.GetLabel(act.GetType())
                    : "-- select --";
                if (Widgets.ButtonText(new Rect(60f, y + 4f, 200f, 24f), actLabel))
                    OpenActionPicker(i);

                // Remove
                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (Widgets.ButtonText(new Rect(w - 64f, y + 4f, 60f, 24f), "Remove"))
                    toRemoveAction = act;
                GUI.color = Color.white;

                // Config
                if (act.HasConfig)
                {
                    Rect cfgRect = new Rect(4f, y + 32f, w - 8f, actH - 36f);
                    Listing_Standard cfgLs = new Listing_Standard();
                    cfgLs.Begin(cfgRect);
                    act.DrawConfig(cfgLs);
                    cfgLs.End();
                }

                y += actH + 2f;
            }
            if (toRemoveAction != null) actions.Remove(toRemoveAction);
            if (moveUpIdx   > 0) { var tmp = actions[moveUpIdx]; actions[moveUpIdx] = actions[moveUpIdx - 1]; actions[moveUpIdx - 1] = tmp; }
            if (moveDownIdx >= 0 && moveDownIdx < actions.Count - 1) { var tmp = actions[moveDownIdx]; actions[moveDownIdx] = actions[moveDownIdx + 1]; actions[moveDownIdx + 1] = tmp; }

            if (Widgets.ButtonText(new Rect(4f, y, 160f, 26f), "+ Add Action"))
                OpenActionPicker(-1);
            y += 32f;

            Widgets.EndScrollView();

            // ── Bottom buttons ────────────────────────────────────────────────
            float btnY = inRect.yMax - btnH + 4f;
            if (Widgets.ButtonText(new Rect(0, btnY, 100f, 32f), "Cancel"))
                Close();

            bool valid = !ruleName.NullOrEmpty()
                      && triggerEntries.Count > 0
                      && triggerEntries.All(e => e.trigger != null)
                      && actions.Count > 0;

            if (!valid) GUI.color = Color.gray;
            if (Widgets.ButtonText(new Rect(inRect.width - 120f, btnY, 120f, 32f), "Save Rule") && valid)
            {
                SaveRule();
                Close();
            }
            GUI.color = Color.white;

            if (!valid)
            {
                string hint = triggerEntries.Count == 0 ? "Needs ≥1 trigger."
                            : actions.Count == 0        ? "Needs ≥1 action."
                            : triggerEntries.Any(e => e.trigger == null) ? "Select a trigger type."
                            : "Fill in name.";
                Widgets.Label(new Rect(110f, btnY, 350f, 32f), $"<color=red>{hint}</color>");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void DrawCategoryButtons(Listing_Standard ls)
        {
            var categories = new[]
            {
                RuleCategory.Custom, RuleCategory.Combat, RuleCategory.ColonyManagement,
                RuleCategory.Economy, RuleCategory.Social, RuleCategory.Medical,
                RuleCategory.Research, RuleCategory.Notifications,
            };
            var opts = new List<FloatMenuOption>();
            foreach (RuleCategory cat in categories)
            {
                RuleCategory captured = cat;
                opts.Add(new FloatMenuOption(cat.ToString(),
                    () => ruleCategory = captured,
                    extraPartWidth: 0f));
            }
            if (ls.ButtonText($"Category: {ruleCategory}"))
                Find.WindowStack.Add(new FloatMenu(opts));
        }

        private float DrawSectionHeader(float y, float w, string label)
        {
            GUI.color = new Color(0.7f, 0.9f, 1f);
            Widgets.DrawLineHorizontal(0f, y, w);
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(4f, y + 2f, w, 18f), label);
            Text.Font = GameFont.Small;
            return y + 22f;
        }

        private void OpenTriggerPicker(TriggerEntry existingEntry)
        {
            var opts = new List<FloatMenuOption>();
            foreach (Type t in TriggerRegistry.AllTypes)
            {
                Type captured = t;
                opts.Add(new FloatMenuOption(
                    TriggerRegistry.GetLabel(captured),
                    () =>
                    {
                        AutomationTrigger inst = TriggerRegistry.CreateInstance(captured);
                        if (existingEntry != null)
                            existingEntry.trigger = inst;
                        else
                            triggerEntries.Add(new TriggerEntry { trigger = inst, negate = false });
                    }));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void OpenActionPicker(int replaceIdx)
        {
            var opts = new List<FloatMenuOption>();
            foreach (Type t in ActionRegistry.AllTypes)
            {
                Type captured  = t;
                int  idxCap    = replaceIdx;
                opts.Add(new FloatMenuOption(
                    ActionRegistry.GetLabel(captured),
                    () =>
                    {
                        AutomationAction inst = ActionRegistry.CreateInstance(captured);
                        if (idxCap >= 0 && idxCap < actions.Count)
                            actions[idxCap] = inst;
                        else
                            actions.Add(inst);
                    }));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        // ── Frequency picker ──────────────────────────────────────────────────
        private void OpenFrequencyPicker()
        {
            var opts = new List<FloatMenuOption>();
            foreach (var (label, ticks) in FrequencyPresets)
            {
                int captured = ticks;
                opts.Add(new FloatMenuOption(label, () => checkFrequencyTicks = captured));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        // ── Save ──────────────────────────────────────────────────────────────
        private void SaveRule()
        {
            AutomationRule target = existingRule ?? new AutomationRule();

            target.name                = ruleName.NullOrEmpty() ? "Rule" : ruleName;
            target.notes               = ruleNotes;
            target.enabled             = ruleEnabled;
            target.category            = ruleCategory;
            target.priority            = rulePriority;
            target.triggerMode         = triggerMode;
            target.checkFrequencyTicks = checkFrequencyTicks;
            target.cooldownTicks       = cooldownHours * 2500;
            target.oneShotRule         = oneShotRule;
            target.maxFires            = maxFires;

            target.triggerEntries = triggerEntries;
            target.actions        = actions;

            if (existingRule == null)
                comp.rules.Add(target);
        }
    }
}
