using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RimWorldIFTTT.Tests.Stubs;

namespace RimWorldIFTTT.Tests
{
    /// <summary>
    /// Tests for AutomationRule logic that does NOT require a live Map.
    /// These tests cover: CanFire, cooldown, priority, one-shot, maxFires,
    /// TriggerGroup AND/OR, TriggerEntry negate, multi-group boolean logic.
    ///
    /// Updated for v2 TriggerGroup architecture:
    ///   - Use rule.AddTrigger(trigger, negate) instead of rule.triggerEntries.Add(...)
    ///   - Use rule.FirstGroupMode to set the AND/OR mode of the first group
    ///   - For multi-group tests, build TriggerGroup objects directly
    /// </summary>
    [TestFixture]
    public class AutomationRuleTests
    {
        private AutomationRule MakeRule(bool enabled = true, int cooldown = 0)
        {
            return new AutomationRule
            {
                name          = "Test Rule",
                enabled       = enabled,
                cooldownTicks = cooldown,
                lastFiredTick = -999999,
            };
        }

        private TriggerEntry MakeEntry(bool result, bool negate = false)
        {
            return new TriggerEntry
            {
                trigger = new StubTrigger { ReturnValue = result },
                negate  = negate,
            };
        }

        // ── CanFire tests ──────────────────────────────────────────────────────

        [Test]
        public void CanFire_ReturnsFalse_WhenDisabled()
        {
            var rule = MakeRule(enabled: false);
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.actions.Add(new StubAction());
            Assert.IsFalse(rule.CanFire(1000));
        }

        [Test]
        public void CanFire_ReturnsFalse_WhenNoTriggers()
        {
            var rule = MakeRule();
            rule.actions.Add(new StubAction());
            Assert.IsFalse(rule.CanFire(1000));
        }

        [Test]
        public void CanFire_ReturnsFalse_WhenNoActions()
        {
            var rule = MakeRule();
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            Assert.IsFalse(rule.CanFire(1000));
        }

        [Test]
        public void CanFire_ReturnsTrue_WhenEnabledWithTriggerAndAction()
        {
            var rule = MakeRule();
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.actions.Add(new StubAction());
            Assert.IsTrue(rule.CanFire(1000));
        }

        [Test]
        public void CanFire_ReturnsFalse_WhenCooldownNotElapsed()
        {
            var rule = MakeRule(cooldown: 2500);
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.actions.Add(new StubAction());
            rule.lastFiredTick = 1000;
            Assert.IsFalse(rule.CanFire(1001));   // only 1 tick elapsed, need 2500
        }

        [Test]
        public void CanFire_ReturnsTrue_WhenCooldownElapsed()
        {
            var rule = MakeRule(cooldown: 100);
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.actions.Add(new StubAction());
            rule.lastFiredTick = 1000;
            Assert.IsTrue(rule.CanFire(1100));    // exactly 100 ticks elapsed
        }

        [Test]
        public void CanFire_ReturnsFalse_WhenMaxFiresReached()
        {
            var rule = MakeRule();
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.actions.Add(new StubAction());
            rule.maxFires       = 3;
            rule.totalFireCount = 3;
            Assert.IsFalse(rule.CanFire(1000));
        }

        [Test]
        public void CanFire_ReturnsTrue_WhenMaxFiresZeroUnlimited()
        {
            var rule = MakeRule();
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.actions.Add(new StubAction());
            rule.maxFires       = 0;
            rule.totalFireCount = 9999;
            Assert.IsTrue(rule.CanFire(1000));
        }

        // ── Single-group TriggerMode AND tests ────────────────────────────────

        [Test]
        public void EvaluateTriggers_AND_ReturnsFalse_WhenAnyFalse()
        {
            var rule = MakeRule();
            rule.FirstGroupMode = TriggerMode.All;
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.AddTrigger(new StubTrigger { ReturnValue = false });
            Assert.IsFalse(rule.EvaluateTriggers(null));
        }

        [Test]
        public void EvaluateTriggers_AND_ReturnsTrue_WhenAllTrue()
        {
            var rule = MakeRule();
            rule.FirstGroupMode = TriggerMode.All;
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            Assert.IsTrue(rule.EvaluateTriggers(null));
        }

        // ── Single-group TriggerMode OR tests ─────────────────────────────────

        [Test]
        public void EvaluateTriggers_OR_ReturnsTrue_WhenAnyTrue()
        {
            var rule = MakeRule();
            rule.FirstGroupMode = TriggerMode.Any;
            rule.AddTrigger(new StubTrigger { ReturnValue = false });
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            Assert.IsTrue(rule.EvaluateTriggers(null));
        }

        [Test]
        public void EvaluateTriggers_OR_ReturnsFalse_WhenAllFalse()
        {
            var rule = MakeRule();
            rule.FirstGroupMode = TriggerMode.Any;
            rule.AddTrigger(new StubTrigger { ReturnValue = false });
            rule.AddTrigger(new StubTrigger { ReturnValue = false });
            Assert.IsFalse(rule.EvaluateTriggers(null));
        }

        // ── Multi-group boolean logic tests (v2) ──────────────────────────────

        [Test]
        public void EvaluateTriggers_TwoGroups_BothTrue_ReturnsTrue()
        {
            // (T AND T) AND (T OR F) == true
            var rule = MakeRule();

            var grp1 = new TriggerGroup { mode = TriggerMode.All };
            grp1.triggers.Add(new TriggerEntry { trigger = new StubTrigger { ReturnValue = true } });
            grp1.triggers.Add(new TriggerEntry { trigger = new StubTrigger { ReturnValue = true } });

            var grp2 = new TriggerGroup { mode = TriggerMode.Any };
            grp2.triggers.Add(new TriggerEntry { trigger = new StubTrigger { ReturnValue = false } });
            grp2.triggers.Add(new TriggerEntry { trigger = new StubTrigger { ReturnValue = true } });

            rule.triggerGroups.Add(grp1);
            rule.triggerGroups.Add(grp2);
            rule.actions.Add(new StubAction());

            Assert.IsTrue(rule.EvaluateTriggers(null));
        }

        [Test]
        public void EvaluateTriggers_TwoGroups_OneGroupFalse_ReturnsFalse()
        {
            // (T AND F) AND (T) == false — first group fails
            var rule = MakeRule();

            var grp1 = new TriggerGroup { mode = TriggerMode.All };
            grp1.triggers.Add(new TriggerEntry { trigger = new StubTrigger { ReturnValue = true } });
            grp1.triggers.Add(new TriggerEntry { trigger = new StubTrigger { ReturnValue = false } });

            var grp2 = new TriggerGroup { mode = TriggerMode.All };
            grp2.triggers.Add(new TriggerEntry { trigger = new StubTrigger { ReturnValue = true } });

            rule.triggerGroups.Add(grp1);
            rule.triggerGroups.Add(grp2);
            rule.actions.Add(new StubAction());

            Assert.IsFalse(rule.EvaluateTriggers(null));
        }

        [Test]
        public void EvaluateTriggers_EmptyGroup_PassesThrough()
        {
            // An empty group returns true (pass-through) and doesn't block the rule.
            // Rule: [empty group] AND [T] == true
            var rule = MakeRule();

            var grp1 = new TriggerGroup { mode = TriggerMode.All }; // empty — pass-through
            var grp2 = new TriggerGroup { mode = TriggerMode.All };
            grp2.triggers.Add(new TriggerEntry { trigger = new StubTrigger { ReturnValue = true } });

            rule.triggerGroups.Add(grp1);
            rule.triggerGroups.Add(grp2);
            rule.actions.Add(new StubAction());

            // EvaluateTriggers returns true; but CanFire would return false because
            // !triggerGroups.Any(g => g.triggers.Count > 0) is false here (grp2 has triggers).
            Assert.IsTrue(rule.EvaluateTriggers(null));
        }

        // ── TriggerEntry negate tests ──────────────────────────────────────────

        [Test]
        public void TriggerEntry_Negate_InvertsResult()
        {
            TriggerEntry entry = MakeEntry(result: true, negate: true);
            Assert.IsFalse(entry.Evaluate(null));

            TriggerEntry entry2 = MakeEntry(result: false, negate: true);
            Assert.IsTrue(entry2.Evaluate(null));
        }

        [Test]
        public void TriggerEntry_NoNegate_PassesThrough()
        {
            Assert.IsTrue(MakeEntry(result: true,  negate: false).Evaluate(null));
            Assert.IsFalse(MakeEntry(result: false, negate: false).Evaluate(null));
        }

        // ── TryFire tests ──────────────────────────────────────────────────────

        [Test]
        public void TryFire_ExecutesAction_WhenConditionsMet()
        {
            var rule   = MakeRule();
            var action = new StubAction();
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.actions.Add(action);

            bool fired = rule.TryFire(null, 1000);

            Assert.IsTrue(fired);
            Assert.AreEqual(1, action.ExecuteCallCount);
        }

        [Test]
        public void TryFire_DoesNotExecute_WhenTriggerFalse()
        {
            var rule   = MakeRule();
            var action = new StubAction();
            rule.AddTrigger(new StubTrigger { ReturnValue = false });
            rule.actions.Add(action);

            bool fired = rule.TryFire(null, 1000);

            Assert.IsFalse(fired);
            Assert.AreEqual(0, action.ExecuteCallCount);
        }

        [Test]
        public void TryFire_IncrementsFireCount()
        {
            var rule = MakeRule();
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.actions.Add(new StubAction());

            rule.TryFire(null, 100);
            rule.TryFire(null, 200);

            Assert.AreEqual(2, rule.totalFireCount);
            Assert.AreEqual(2, rule.sessionFireCount);
        }

        [Test]
        public void TryFire_UpdatesLastFiredTick()
        {
            var rule = MakeRule();
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.actions.Add(new StubAction());

            rule.TryFire(null, 7500);
            Assert.AreEqual(7500, rule.lastFiredTick);
        }

        [Test]
        public void TryFire_OneShotRule_DisablesRuleAfterFire()
        {
            var rule = MakeRule();
            rule.oneShotRule = true;
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.actions.Add(new StubAction());

            Assert.IsTrue(rule.enabled);
            rule.TryFire(null, 1000);
            Assert.IsFalse(rule.enabled, "One-shot rule should be disabled after firing.");
        }

        [Test]
        public void TryFire_ExecutesAllActions_InOrder()
        {
            var rule = MakeRule();
            rule.AddTrigger(new StubTrigger { ReturnValue = true });

            var action1 = new StubAction();
            var action2 = new StubAction();
            var action3 = new StubAction();
            rule.actions.Add(action1);
            rule.actions.Add(action2);
            rule.actions.Add(action3);

            rule.TryFire(null, 1000);

            Assert.AreEqual(1, action1.ExecuteCallCount);
            Assert.AreEqual(1, action2.ExecuteCallCount);
            Assert.AreEqual(1, action3.ExecuteCallCount);
        }

        // ── Priority sorting (via LINQ) ────────────────────────────────────────

        [Test]
        public void Rules_SortedByPriority_LowerFirst()
        {
            var rules = new List<AutomationRule>
            {
                new AutomationRule { priority = 50 },
                new AutomationRule { priority = 10 },
                new AutomationRule { priority = 99 },
                new AutomationRule { priority = 1  },
            };

            var sorted = rules.OrderBy(r => r.priority).ToList();

            Assert.AreEqual(1,  sorted[0].priority);
            Assert.AreEqual(10, sorted[1].priority);
            Assert.AreEqual(50, sorted[2].priority);
            Assert.AreEqual(99, sorted[3].priority);
        }

        // ── AddTrigger convenience ─────────────────────────────────────────────

        [Test]
        public void AddTrigger_CreatesFirstGroupIfEmpty()
        {
            var rule = MakeRule();
            Assert.AreEqual(0, rule.triggerGroups.Count);

            rule.AddTrigger(new StubTrigger { ReturnValue = true });

            Assert.AreEqual(1, rule.triggerGroups.Count);
            Assert.AreEqual(1, rule.triggerGroups[0].triggers.Count);
        }

        [Test]
        public void AddTrigger_AddsToExistingFirstGroup()
        {
            var rule = MakeRule();
            rule.AddTrigger(new StubTrigger { ReturnValue = true });
            rule.AddTrigger(new StubTrigger { ReturnValue = false });

            Assert.AreEqual(1, rule.triggerGroups.Count, "Should still be 1 group");
            Assert.AreEqual(2, rule.triggerGroups[0].triggers.Count);
        }

        [Test]
        public void AddTrigger_WithNegate_SetsNegateFlag()
        {
            var rule = MakeRule();
            rule.AddTrigger(new StubTrigger { ReturnValue = true }, negate: true);

            Assert.IsTrue(rule.triggerGroups[0].triggers[0].negate);
        }

        [Test]
        public void FirstGroupMode_GetSet_WorksCorrectly()
        {
            var rule = MakeRule();
            rule.AddTrigger(new StubTrigger { ReturnValue = true });

            rule.FirstGroupMode = TriggerMode.Any;
            Assert.AreEqual(TriggerMode.Any, rule.FirstGroupMode);

            rule.FirstGroupMode = TriggerMode.All;
            Assert.AreEqual(TriggerMode.All, rule.FirstGroupMode);
        }
    }
}
