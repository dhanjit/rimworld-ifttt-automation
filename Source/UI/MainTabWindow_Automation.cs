using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.UI
{
    /// <summary>
    /// Main IFTTT tab window.
    /// Layout:
    ///   Top: category filter tabs
    ///   Left (70%): rule list with checkbox / name+priority / trigger summary / action summary / edit/dupe/delete
    ///   Right (30%): recent fire event log panel (toggleable)
    ///   Footer: stats, Add Rule, Settings
    /// </summary>
    public class MainTabWindow_Automation : MainTabWindow
    {
        // ── Constants ─────────────────────────────────────────────────────────
        private const float RowH    = 40f;
        private const float TabH    = 28f;
        private const float FooterH = 36f;
        private const float LogW    = 260f;
        private const float ChkW    = 26f;
        private const float Pad     = 6f;
        private const float BtnW    = 64f;

        // ── State ─────────────────────────────────────────────────────────────
        private RuleCategory   selectedCategory = RuleCategory.All;
        private Vector2        ruleScrollPos;
        private Vector2        logScrollPos;
        private AutomationRule pendingDelete;
        private AutomationRule pendingEdit;
        private bool           showLog = true;

        public override Vector2 RequestedTabSize => new Vector2(980f, 620f);

        private static AutomationGameComp GetComp() => AutomationGameComp.Instance;

        // ── DoWindowContents ──────────────────────────────────────────────────
        public override void DoWindowContents(Rect inRect)
        {
            pendingDelete = null;
            pendingEdit   = null;

            AutomationGameComp comp = GetComp();
            if (comp == null)
            {
                Widgets.Label(inRect, "Automation component not available. Load a save first.");
                return;
            }

            // Category tabs
            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, TabH);
            DrawCategoryTabs(tabRect, comp);

            float bodyY = inRect.y + TabH + Pad;
            float bodyH = inRect.height - TabH - FooterH - Pad * 2f;

            float logPanelW = showLog ? LogW : 0f;
            float ruleW     = inRect.width - logPanelW - (showLog ? Pad : 0f);

            Rect ruleArea = new Rect(inRect.x, bodyY, ruleW, bodyH);
            Rect logArea  = showLog
                ? new Rect(inRect.x + ruleW + Pad, bodyY, logPanelW, bodyH)
                : Rect.zero;

            List<AutomationRule> filtered = comp.GetRulesByCategory(selectedCategory);
            DrawRuleList(ruleArea, filtered, comp);

            if (showLog)
                DrawLogPanel(logArea, comp);

            DrawFooter(new Rect(inRect.x, inRect.yMax - FooterH, inRect.width, FooterH), comp);

            // Deferred actions (avoid mutating list while drawing)
            if (pendingDelete != null)
            {
                AutomationRule captured = pendingDelete;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"Delete rule '{captured.name}'?",
                    () => comp.rules.Remove(captured)));
            }
            if (pendingEdit != null)
                Find.WindowStack.Add(new Dialog_AddEditRule(comp, pendingEdit));
        }

        // ── Category tabs ─────────────────────────────────────────────────────
        private static readonly RuleCategory[] CategoryOrder =
        {
            RuleCategory.All, RuleCategory.Combat, RuleCategory.ColonyManagement,
            RuleCategory.Economy, RuleCategory.Social, RuleCategory.Medical,
            RuleCategory.Research, RuleCategory.Notifications, RuleCategory.Custom,
        };

        private static readonly Dictionary<RuleCategory, string> CategoryLabels = new Dictionary<RuleCategory, string>
        {
            { RuleCategory.All,              "All"      },
            { RuleCategory.Combat,           "Combat"   },
            { RuleCategory.ColonyManagement, "Colony"   },
            { RuleCategory.Economy,          "Economy"  },
            { RuleCategory.Social,           "Social"   },
            { RuleCategory.Medical,          "Medical"  },
            { RuleCategory.Research,         "Research" },
            { RuleCategory.Notifications,    "Alerts"   },
            { RuleCategory.Custom,           "Custom"   },
        };

        private void DrawCategoryTabs(Rect r, AutomationGameComp comp)
        {
            float tabW = (r.width - BtnW - Pad * 2f) / CategoryOrder.Length;
            float x    = r.x;

            foreach (RuleCategory cat in CategoryOrder)
            {
                bool active = cat == selectedCategory;
                int  count  = cat == RuleCategory.All
                    ? comp.rules.Count
                    : comp.rules.Count(rr => rr.category == cat);

                Rect tabR = new Rect(x, r.y, tabW - 2f, TabH);
                if (active) GUI.color = new Color(0.6f, 0.85f, 1f);
                if (Widgets.ButtonText(tabR, $"{CategoryLabels[cat]} ({count})"))
                    selectedCategory = cat;
                GUI.color = Color.white;
                x += tabW;
            }

            string logLabel = showLog ? "Log ▶" : "◀ Log";
            if (Widgets.ButtonText(new Rect(r.xMax - BtnW - 2f, r.y, BtnW, TabH), logLabel))
                showLog = !showLog;
        }

        // ── Rule list ─────────────────────────────────────────────────────────
        private void DrawRuleList(Rect listRect, List<AutomationRule> rules, AutomationGameComp comp)
        {
            float hdrH = 22f;
            DrawRuleColumnHeaders(new Rect(listRect.x, listRect.y, listRect.width, hdrH));

            Rect scrollArea  = new Rect(listRect.x, listRect.y + hdrH, listRect.width, listRect.height - hdrH);
            Rect viewContent = new Rect(0, 0, listRect.width - 20f, rules.Count * RowH);

            Widgets.BeginScrollView(scrollArea, ref ruleScrollPos, viewContent);
            for (int i = 0; i < rules.Count; i++)
            {
                Rect row = new Rect(0, i * RowH, viewContent.width, RowH);
                if (i % 2 == 0) Widgets.DrawAltRect(row);
                DrawRuleRow(row, rules[i], comp);
            }
            Widgets.EndScrollView();
        }

        private static void DrawRuleColumnHeaders(Rect r)
        {
            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(r.x, r.yMax - 1f, r.width);
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            float x = r.x + ChkW + Pad;
            Widgets.Label(new Rect(x,        r.y, 145f, r.height), "Name [Priority]");
            Widgets.Label(new Rect(x + 150f, r.y, 200f, r.height), "IF (Trigger)");
            Widgets.Label(new Rect(x + 355f, r.y, 180f, r.height), "THEN (Action)");
            Text.Font = GameFont.Small;
        }

        private void DrawRuleRow(Rect row, AutomationRule rule, AutomationGameComp comp)
        {
            float cx = row.x + Pad;
            float cy = row.y + (RowH - 24f) / 2f;

            Widgets.Checkbox(cx, cy, ref rule.enabled, size: 24f);
            cx += ChkW + Pad;

            // Name + priority
            GUI.color = rule.enabled ? Color.white : Color.gray;
            Widgets.Label(new Rect(cx, row.y + 2f, 142f, RowH - 4f),
                $"[{rule.priority}] {rule.name}".Truncate(142f));
            GUI.color = Color.white;
            cx += 150f;

            // Trigger summary (v2 groups)
            GUI.color = new Color(0.55f, 0.85f, 1f);
            int totalTriggers = rule.triggerGroups.Sum(g => g.triggers.Count);
            string trigSummary;
            if (totalTriggers == 0)
                trigSummary = "<none>";
            else if (rule.triggerGroups.Count == 1 && totalTriggers == 1)
            {
                var e = rule.triggerGroups[0].triggers[0];
                trigSummary = (e.negate ? "NOT " : "") + (e.trigger?.Label ?? "?");
            }
            else if (rule.triggerGroups.Count == 1)
            {
                string gMode = rule.triggerGroups[0].mode == TriggerMode.All ? "ALL" : "ANY";
                trigSummary = $"[{gMode}] {totalTriggers} triggers";
            }
            else
                trigSummary = $"{rule.triggerGroups.Count} groups, {totalTriggers} triggers";
            Widgets.Label(new Rect(cx, row.y + 2f, 198f, RowH - 4f), trigSummary.Truncate(198f));
            GUI.color = Color.white;
            cx += 205f;

            // Action summary
            GUI.color = new Color(0.55f, 1f, 0.6f);
            string actSummary = rule.actions.Count == 0
                ? "<none>"
                : rule.actions.Count == 1
                    ? rule.actions[0].Label
                    : $"{rule.actions.Count} actions";
            Widgets.Label(new Rect(cx, row.y + 2f, 175f, RowH - 4f), actSummary.Truncate(175f));
            GUI.color = Color.white;

            // Buttons (right-aligned)
            float rightX = row.xMax - BtnW * 3f - Pad * 3f;
            if (Widgets.ButtonText(new Rect(rightX, cy, BtnW, 26f), "Edit"))
                pendingEdit = rule;

            GUI.color = new Color(0.5f, 0.8f, 1f);
            if (Widgets.ButtonText(new Rect(rightX + BtnW + Pad, cy, BtnW, 26f), "Dupe"))
                comp.DuplicateRule(rule);
            GUI.color = Color.white;

            GUI.color = new Color(1f, 0.45f, 0.45f);
            if (Widgets.ButtonText(new Rect(rightX + BtnW * 2f + Pad * 2f, cy, BtnW, 26f), "Delete"))
                pendingDelete = rule;
            GUI.color = Color.white;
        }

        // ── Log panel ─────────────────────────────────────────────────────────
        private void DrawLogPanel(Rect r, AutomationGameComp comp)
        {
            GUI.color = new Color(0.18f, 0.18f, 0.22f);
            Widgets.DrawBox(r);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(r.x + 4f, r.y + 2f, r.width, 18f),
                $"Recent events ({comp.recentEvents.Count}):");
            Text.Font = GameFont.Small;

            Rect scrollArea  = new Rect(r.x, r.y + 22f, r.width, r.height - 22f);
            const float logRowH = 34f;
            Rect viewContent = new Rect(0, 0, r.width - 20f, comp.recentEvents.Count * logRowH);

            Widgets.BeginScrollView(scrollArea, ref logScrollPos, viewContent);

            var events = comp.recentEvents.AsEnumerable().Reverse().ToList();
            for (int i = 0; i < events.Count; i++)
            {
                RuleFireEvent ev = events[i];
                Rect          eR = new Rect(0, i * logRowH, viewContent.width, logRowH);
                if (i % 2 == 0) Widgets.DrawAltRect(eR);

                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.9f, 0.9f, 0.5f);
                string header = string.IsNullOrEmpty(ev.mapName)
                    ? $"{ev.timestamp} [{ev.category}]"
                    : $"{ev.timestamp} [{ev.category}] @ {ev.mapName}";
                Widgets.Label(new Rect(4f, eR.y + 2f, eR.width - 4f, 14f), header);
                GUI.color = Color.white;
                Widgets.Label(new Rect(4f, eR.y + 16f, eR.width - 4f, 16f),
                    ev.ruleName.Truncate(eR.width - 8f));
                Text.Font = GameFont.Small;
            }

            Widgets.EndScrollView();
        }

        // ── Footer ────────────────────────────────────────────────────────────
        private void DrawFooter(Rect r, AutomationGameComp comp)
        {
            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(r.x, r.y, r.width);
            GUI.color = Color.white;

            string info = $"Rules: {comp.rules.Count}  |  Check: {comp.checkIntervalTicks} ticks  |  " +
                          $"Verbose: {(comp.verboseLogging ? "ON" : "off")}";
            Widgets.Label(new Rect(r.x + Pad, r.y + 6f, r.width - 210f, r.height), info);

            float addW = 120f;
            if (Widgets.ButtonText(new Rect(r.xMax - addW - Pad, r.y + 4f, addW, 28f), "+ Add Rule"))
                Find.WindowStack.Add(new Dialog_AddEditRule(comp));

            float settW = 80f;
            if (Widgets.ButtonText(new Rect(r.xMax - addW - settW - Pad * 2f, r.y + 4f, settW, 28f), "Settings"))
                Find.WindowStack.Add(new Dialog_AutomationSettings(comp));
        }
    }
}
