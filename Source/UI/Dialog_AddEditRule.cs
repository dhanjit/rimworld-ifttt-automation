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
    ///   IF section: Trigger groups (each group has ALL/ANY toggle + trigger list)
    ///               Groups are always ANDed; triggers within a group use its mode.
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
            float hours = ticks / 2500f;
            return hours >= 24f
                ? $"Every {hours / 24f:0.##} day(s)"
                : $"Every {hours:0.##} hour(s)";
        }

        // ── State ─────────────────────────────────────────────────────────────
        private readonly AutomationGameComp comp;
        private readonly AutomationRule     existingRule;

        // Working copies of scalar rule fields
        private string            ruleName;
        private string            ruleNotes;
        private bool              ruleEnabled;
        private RuleCategory      ruleCategory;
        private int               rulePriority;
        private string            priorityBuf;
        private int               checkFrequencyTicks;
        private int               cooldownHours;
        private string            cooldownBuf;
        private bool              oneShotRule;
        private bool              notifyOnFire;
        private bool              notifyOnFailure;
        private int               maxFires;
        private string            maxFiresBuf;

        // Mutable working lists (deep-copied so Cancel discards changes)
        private List<TriggerGroup>      triggerGroups;
        private List<AutomationAction>  actions;
        private List<AutomationTrigger> actionGuards;  // parallel to actions: null = no guard

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
            checkFrequencyTicks = 2500;
            cooldownHours       = 1;
            cooldownBuf         = "1";
            oneShotRule         = false;
            notifyOnFire        = false;
            notifyOnFailure     = false;
            maxFires            = 0;
            maxFiresBuf         = "0";

            triggerGroups = new List<TriggerGroup>();
            actions       = new List<AutomationAction>();
            actionGuards  = new List<AutomationTrigger>();

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
            checkFrequencyTicks = rule.checkFrequencyTicks;
            cooldownHours       = rule.cooldownTicks / 2500;
            cooldownBuf         = cooldownHours.ToString();
            oneShotRule         = rule.oneShotRule;
            notifyOnFire        = rule.notifyOnFire;
            notifyOnFailure     = rule.notifyOnFailure;
            maxFires            = rule.maxFires;
            maxFiresBuf         = rule.maxFires.ToString();

            // Deep-copy trigger groups so Cancel works correctly
            triggerGroups = new List<TriggerGroup>();
            foreach (var grp in rule.triggerGroups)
            {
                var newGrp = new TriggerGroup { label = grp.label, mode = grp.mode };
                foreach (var e in grp.triggers)
                    newGrp.triggers.Add(new TriggerEntry { trigger = e.trigger, negate = e.negate });
                triggerGroups.Add(newGrp);
            }
            actions      = new List<AutomationAction>(rule.actions);
            actionGuards = new List<AutomationTrigger>();
            for (int i = 0; i < rule.actions.Count; i++)
                actionGuards.Add(rule.GetActionGuard(i));

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

            float contentY = 38f;
            float btnH     = 42f;
            Rect contentRect = new Rect(0, contentY, inRect.width, inRect.height - contentY - btnH);

            // ── Compute scroll height ──────────────────────────────────────────
            float triggersH = 0f;
            for (int gi = 0; gi < triggerGroups.Count; gi++)
            {
                var grp = triggerGroups[gi];
                triggersH += 26f; // group header bar
                foreach (var e in grp.triggers)
                    triggersH += 70f + (e.trigger?.HasConfig == true ? e.trigger.ConfigHeight : 0f) + 2f;
                triggersH += 32f; // "+ Add Trigger" button
                if (gi < triggerGroups.Count - 1)
                    triggersH += 18f; // "AND" separator between groups
            }
            float actionsH = 0f;
            for (int i = 0; i < actions.Count; i++)
            {
                var a = actions[i];
                var g = GuardAt(i);
                actionsH += 70f + (a.HasConfig ? a.ConfigHeight : 0f)   // action header + config
                          + 30f + (g?.HasConfig == true ? g.ConfigHeight : 0f)  // guard row + guard config
                          + 2f;
            }

            float innerH = 510f           // settings block (460 + ~50 for 2 notify checkboxes)
                         + 44f            // two section headers
                         + triggersH
                         + 32f            // "+ Add Group" button
                         + actionsH
                         + 32f;           // "+ Add Action" button
            Rect viewRect = new Rect(0, 0, contentRect.width - 20f, innerH);

            Widgets.BeginScrollView(contentRect, ref scrollPos, viewRect);

            float y = 0f;
            float w = viewRect.width;

            // ── Settings ──────────────────────────────────────────────────────
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(new Rect(0, y, w, 2000f));

            ls.Label("Rule name:");
            ruleName = ls.TextEntry(ruleName);
            ls.Gap(2f);

            ls.Label("Notes / description:");
            ruleNotes = ls.TextEntry(ruleNotes);
            ls.Gap(4f);

            ls.Label($"Check frequency:  <b>{FrequencyLabel(checkFrequencyTicks)}</b>  " +
                     "(how often triggers are evaluated)");
            if (ls.ButtonText($"Change frequency → {FrequencyLabel(checkFrequencyTicks)}"))
                OpenFrequencyPicker();

            ls.Gap(2f);
            ls.TextFieldNumericLabeled("Cooldown (hours between firings): ", ref cooldownHours, ref cooldownBuf, 0, 9999);
            ls.TextFieldNumericLabeled("Priority (lower fires first): ",      ref rulePriority,  ref priorityBuf,  0, 999);
            ls.TextFieldNumericLabeled("Max fires (0 = unlimited): ",         ref maxFires,      ref maxFiresBuf,  0, 99999);

            ls.CheckboxLabeled("Enabled",                                      ref ruleEnabled);
            ls.CheckboxLabeled("One-shot (disables itself after firing once)", ref oneShotRule);
            ls.CheckboxLabeled("Notify when fired (shows green banner)",       ref notifyOnFire);
            ls.CheckboxLabeled("Notify on action failure (shows warning when an action had nothing to do)", ref notifyOnFailure);

            ls.Gap(4f);
            DrawCategoryButtons(ls);

            y += ls.CurHeight;
            ls.End();

            // ── Trigger groups ────────────────────────────────────────────────
            y = DrawSectionHeader(y, w,
                "IF  (all groups must match — AND between groups; within each group use ALL or ANY):");

            TriggerGroup grpToRemove = null;
            for (int gi = 0; gi < triggerGroups.Count; gi++)
            {
                TriggerGroup grp = triggerGroups[gi];

                // ── Group header bar ───────────────────────────────────────────
                Rect headerR = new Rect(0, y, w, 24f);
                GUI.color = new Color(0.45f, 0.7f, 1f, 0.12f);
                Widgets.DrawBox(headerR);
                GUI.color = Color.white;

                float hx   = 4f;
                bool  isAll = grp.mode == TriggerMode.All;
                PawnFilterHelper.DrawToggleBtn(new Rect(hx, y + 2f, 54f, 20f), "ALL", isAll,  () => grp.mode = TriggerMode.All);
                hx += 58f;
                PawnFilterHelper.DrawToggleBtn(new Rect(hx, y + 2f, 54f, 20f), "ANY", !isAll, () => grp.mode = TriggerMode.Any);
                hx += 62f;

                string groupDesc = isAll ? "ALL triggers must match" : "ANY trigger matches";
                Widgets.Label(new Rect(hx, y + 2f, 250f, 20f), $"Group {gi + 1}  —  {groupDesc}");

                if (triggerGroups.Count > 1)
                {
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    if (Widgets.ButtonText(new Rect(w - 106f, y + 2f, 102f, 20f), "Remove Group"))
                        grpToRemove = grp;
                    GUI.color = Color.white;
                }
                y += 26f;

                // ── Triggers within this group ────────────────────────────────
                TriggerEntry toRemoveTrigger = null;
                for (int i = 0; i < grp.triggers.Count; i++)
                {
                    TriggerEntry entry  = grp.triggers[i];
                    float        entryH = 70f + (entry.trigger?.HasConfig == true ? entry.trigger.ConfigHeight : 0f);
                    Rect         entryR = new Rect(8f, y, w - 8f, entryH);
                    if (i % 2 == 0) Widgets.DrawAltRect(entryR);

                    float ex = 12f;
                    Widgets.CheckboxLabeled(new Rect(ex, y + 4f, 60f, 22f), "NOT", ref entry.negate);
                    ex += 68f;

                    string trigLabel = entry.trigger != null
                        ? TriggerRegistry.GetLabel(entry.trigger.GetType())
                        : "-- select --";
                    TriggerGroup grpCap = grp;
                    if (Widgets.ButtonText(new Rect(ex, y + 4f, 210f, 24f), trigLabel))
                        OpenTriggerPicker(entry, grpCap);

                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    if (Widgets.ButtonText(new Rect(w - 68f, y + 4f, 64f, 24f), "Remove"))
                        toRemoveTrigger = entry;
                    GUI.color = Color.white;

                    if (entry.trigger?.HasConfig == true)
                    {
                        Rect cfgRect = new Rect(12f, y + 32f, w - 16f, entryH - 36f);
                        Listing_Standard cfgLs = new Listing_Standard();
                        cfgLs.Begin(cfgRect);
                        entry.trigger.DrawConfig(cfgLs);
                        cfgLs.End();
                    }

                    y += entryH + 2f;
                }
                if (toRemoveTrigger != null) grp.triggers.Remove(toRemoveTrigger);

                // "+ Add Trigger" for this group
                TriggerGroup addGrp = grp;
                if (Widgets.ButtonText(new Rect(12f, y, 160f, 26f), "+ Add Trigger"))
                    OpenTriggerPicker(null, addGrp);
                y += 32f;

                // "── AND ──" separator between groups
                if (gi < triggerGroups.Count - 1)
                {
                    float mid = w / 2f;
                    GUI.color = new Color(0.6f, 0.85f, 1f);
                    Widgets.DrawLineHorizontal(mid - 120f, y + 8f, 88f);
                    Widgets.DrawLineHorizontal(mid +  34f, y + 8f, 88f);
                    GUI.color = Color.white;
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(mid - 30f, y, 60f, 16f), "── AND ──");
                    Text.Font = GameFont.Small;
                    y += 18f;
                }
            }
            if (grpToRemove != null) triggerGroups.Remove(grpToRemove);

            // "+ Add Group" button
            if (Widgets.ButtonText(new Rect(4f, y, 160f, 26f), "+ Add Group"))
                triggerGroups.Add(new TriggerGroup());
            y += 32f;

            // ── Action list ───────────────────────────────────────────────────
            y = DrawSectionHeader(y, w, "THEN (Actions — executed in order; each may have an optional Guard condition):");

            int removeActionIdx = -1;
            int moveUpIdx = -1, moveDownIdx = -1;
            for (int i = 0; i < actions.Count; i++)
            {
                AutomationAction  act      = actions[i];
                AutomationTrigger guard    = GuardAt(i);
                float guardCfgH  = guard?.HasConfig == true ? guard.ConfigHeight : 0f;
                float actH       = 70f + (act.HasConfig ? act.ConfigHeight : 0f)
                                 + 30f + guardCfgH;
                Rect  actR       = new Rect(0, y, w, actH);
                if (i % 2 == 0) Widgets.DrawAltRect(actR);

                // ── Up / Down ──────────────────────────────────────────────
                if (i > 0 && Widgets.ButtonText(new Rect(4f, y + 4f, 24f, 22f), "^"))
                    moveUpIdx = i;
                if (i < actions.Count - 1 && Widgets.ButtonText(new Rect(30f, y + 4f, 24f, 22f), "v"))
                    moveDownIdx = i;

                // ── Action type picker ─────────────────────────────────────
                string actLabel = act != null
                    ? ActionRegistry.GetLabel(act.GetType())
                    : "-- select --";
                if (Widgets.ButtonText(new Rect(60f, y + 4f, 210f, 24f), actLabel))
                    OpenActionPicker(i);

                // ── Remove ────────────────────────────────────────────────
                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (Widgets.ButtonText(new Rect(w - 68f, y + 4f, 64f, 24f), "Remove"))
                    removeActionIdx = i;
                GUI.color = Color.white;

                // ── Action config ──────────────────────────────────────────
                float cfgBottomY = y + 32f;
                if (act.HasConfig)
                {
                    Rect cfgRect = new Rect(4f, y + 32f, w - 8f, act.ConfigHeight);
                    Listing_Standard cfgLs = new Listing_Standard();
                    cfgLs.Begin(cfgRect);
                    act.DrawConfig(cfgLs);
                    cfgLs.End();
                    cfgBottomY += act.ConfigHeight;
                }

                // ── Guard row ──────────────────────────────────────────────
                float gy = cfgBottomY + 2f;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.5f);
                Widgets.Label(new Rect(12f, gy + 5f, 54f, 18f), "Guard:");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                string guardBtnLabel = guard != null
                    ? TriggerRegistry.GetLabel(guard.GetType())
                    : "None  ▼";
                int iCap = i;
                if (Widgets.ButtonText(new Rect(70f, gy, 200f, 24f), guardBtnLabel))
                    OpenGuardPicker(iCap);

                if (guard != null)
                {
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    if (Widgets.ButtonText(new Rect(276f, gy, 52f, 24f), "Clear"))
                        SetGuardAt(iCap, null);
                    GUI.color = Color.white;
                }

                // ── Guard config ───────────────────────────────────────────
                if (guard?.HasConfig == true)
                {
                    Rect gCfgRect = new Rect(4f, gy + 28f, w - 8f, guardCfgH);
                    Listing_Standard gCfgLs = new Listing_Standard();
                    gCfgLs.Begin(gCfgRect);
                    guard.DrawConfig(gCfgLs);
                    gCfgLs.End();
                }

                y += actH + 2f;
            }

            // ── Deferred mutations (after loop to avoid modifying list mid-draw) ──
            if (removeActionIdx >= 0)
            {
                actions.RemoveAt(removeActionIdx);
                if (removeActionIdx < actionGuards.Count) actionGuards.RemoveAt(removeActionIdx);
            }
            if (moveUpIdx > 0)
            {
                EnsureGuardsSynced();
                var tmp  = actions[moveUpIdx];      actions[moveUpIdx]      = actions[moveUpIdx - 1];      actions[moveUpIdx - 1]      = tmp;
                var gtmp = actionGuards[moveUpIdx]; actionGuards[moveUpIdx] = actionGuards[moveUpIdx - 1]; actionGuards[moveUpIdx - 1] = gtmp;
            }
            if (moveDownIdx >= 0 && moveDownIdx < actions.Count - 1)
            {
                EnsureGuardsSynced();
                var tmp  = actions[moveDownIdx];      actions[moveDownIdx]      = actions[moveDownIdx + 1];      actions[moveDownIdx + 1]      = tmp;
                var gtmp = actionGuards[moveDownIdx]; actionGuards[moveDownIdx] = actionGuards[moveDownIdx + 1]; actionGuards[moveDownIdx + 1] = gtmp;
            }

            if (Widgets.ButtonText(new Rect(4f, y, 160f, 26f), "+ Add Action"))
                OpenActionPicker(-1);

            Widgets.EndScrollView();

            // ── Bottom buttons ────────────────────────────────────────────────
            float btnY = inRect.yMax - btnH + 4f;
            if (Widgets.ButtonText(new Rect(0, btnY, 100f, 32f), "Cancel"))
                Close();

            bool hasAnyTrigger     = triggerGroups.Any(g => g.triggers.Count > 0);
            bool allTriggersPicked = triggerGroups.All(g => g.triggers.All(e => e.trigger != null));
            bool valid = !ruleName.NullOrEmpty()
                      && hasAnyTrigger
                      && allTriggersPicked
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
                string hint = !hasAnyTrigger     ? "Needs ≥1 trigger."
                            : !allTriggersPicked  ? "Select a trigger type."
                            : actions.Count == 0  ? "Needs ≥1 action."
                            : "Fill in name.";
                Widgets.Label(new Rect(110f, btnY, 400f, 32f), $"<color=red>{hint}</color>");
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

        /// <summary>
        /// Opens a trigger type picker. If existingEntry != null, replaces its trigger.
        /// Otherwise adds a new entry to targetGroup.
        /// </summary>
        private void OpenTriggerPicker(TriggerEntry existingEntry, TriggerGroup targetGroup)
        {
            var opts = new List<FloatMenuOption>();
            foreach (Type t in TriggerRegistry.AllTypes)
            {
                Type         captured = t;
                TriggerGroup grpCap   = targetGroup;
                opts.Add(new FloatMenuOption(
                    TriggerRegistry.GetLabel(captured),
                    () =>
                    {
                        AutomationTrigger inst = TriggerRegistry.CreateInstance(captured);
                        if (existingEntry != null)
                        {
                            existingEntry.trigger = inst;
                        }
                        else if (grpCap != null)
                        {
                            grpCap.triggers.Add(new TriggerEntry { trigger = inst, negate = false });
                        }
                        else
                        {
                            if (triggerGroups.Count == 0)
                                triggerGroups.Add(new TriggerGroup());
                            triggerGroups[0].triggers.Add(new TriggerEntry { trigger = inst, negate = false });
                        }
                    }));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void OpenActionPicker(int replaceIdx)
        {
            var opts = new List<FloatMenuOption>();
            foreach (Type t in ActionRegistry.AllTypes)
            {
                Type captured = t;
                int  idxCap   = replaceIdx;
                opts.Add(new FloatMenuOption(
                    ActionRegistry.GetLabel(captured),
                    () =>
                    {
                        AutomationAction inst = ActionRegistry.CreateInstance(captured);
                        if (idxCap >= 0 && idxCap < actions.Count)
                        {
                            actions[idxCap] = inst;
                            SetGuardAt(idxCap, null); // clear guard when action type changes
                        }
                        else
                        {
                            actions.Add(inst);
                            actionGuards.Add(null);
                        }
                    }));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        /// <summary>Opens a FloatMenu to pick (or clear) the guard trigger for action at actionIdx.</summary>
        private void OpenGuardPicker(int actionIdx)
        {
            var opts = new List<FloatMenuOption>();
            opts.Add(new FloatMenuOption("None (remove guard)", () => SetGuardAt(actionIdx, null)));
            foreach (Type t in TriggerRegistry.AllTypes)
            {
                Type captured = t;
                int  idxCap   = actionIdx;
                opts.Add(new FloatMenuOption(
                    TriggerRegistry.GetLabel(captured),
                    () =>
                    {
                        AutomationTrigger inst = TriggerRegistry.CreateInstance(captured);
                        SetGuardAt(idxCap, inst);
                    }));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        // ── Guard helpers ─────────────────────────────────────────────────────
        private AutomationTrigger GuardAt(int i)
            => i >= 0 && i < actionGuards.Count ? actionGuards[i] : null;

        private void SetGuardAt(int i, AutomationTrigger guard)
        {
            while (actionGuards.Count <= i) actionGuards.Add(null);
            actionGuards[i] = guard;
        }

        private void EnsureGuardsSynced()
        {
            while (actionGuards.Count < actions.Count) actionGuards.Add(null);
        }

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
            target.checkFrequencyTicks = checkFrequencyTicks;
            target.cooldownTicks       = cooldownHours * 2500;
            target.oneShotRule         = oneShotRule;
            target.notifyOnFire        = notifyOnFire;
            target.notifyOnFailure     = notifyOnFailure;
            target.maxFires            = maxFires;

            target.triggerGroups = triggerGroups;
            target.actions       = actions;
            target.actionGuards  = actionGuards;

            if (existingRule == null)
                comp.rules.Add(target);
        }
    }
}
