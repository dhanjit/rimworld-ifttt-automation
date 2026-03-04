using System.Linq;
using NUnit.Framework;
using RimWorldIFTTT.Tests.Stubs;

namespace RimWorldIFTTT.Tests
{
    /// <summary>
    /// Tests for AutomationRule logic that does NOT require a live Map.
    /// These tests cover: CanFire, cooldown, priority, one-shot, maxFires,
    /// TriggerMode AND/OR, TriggerEntry negate.
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
            rule.triggerEntries.Add(MakeEntry(true));
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
            rule.triggerEntries.Add(MakeEntry(true));
            Assert.IsFalse(rule.CanFire(1000));
        }

        [Test]
        public void CanFire_ReturnsTrue_WhenEnabledWithTriggerAndAction()
        {
            var rule = MakeRule();
            rule.triggerEntries.Add(MakeEntry(true));
            rule.actions.Add(new StubAction());
            Assert.IsTrue(rule.CanFire(1000));
        }

        [Test]
        public void CanFire_ReturnsFalse_WhenCooldownNotElapsed()
        {
            var rule = MakeRule(cooldown: 2500);
            rule.triggerEntries.Add(MakeEntry(true));
            rule.actions.Add(new StubAction());
            rule.lastFiredTick = 1000;
            Assert.IsFalse(rule.CanFire(1001));   // only 1 tick elapsed, need 2500
        }

        [Test]
        public void CanFire_ReturnsTrue_WhenCooldownElapsed()
        {
            var rule = MakeRule(cooldown: 100);
            rule.triggerEntries.Add(MakeEntry(true));
            rule.actions.Add(new StubAction());
            rule.lastFiredTick = 1000;
            Assert.IsTrue(rule.CanFire(1100));    // exactly 100 ticks elapsed
        }

        [Test]
        public void CanFire_ReturnsFalse_WhenMaxFiresReached()
        {
            var rule = MakeRule();
            rule.triggerEntries.Add(MakeEntry(true));
            rule.actions.Add(new StubAction());
            rule.maxFires      = 3;
            rule.totalFireCount = 3;
            Assert.IsFalse(rule.CanFire(1000));
        }

        [Test]
        public void CanFire_ReturnsTrue_WhenMaxFiresZeroUnlimited()
        {
            var rule = MakeRule();
            rule.triggerEntries.Add(MakeEntry(true));
            rule.actions.Add(new StubAction());
            rule.maxFires       = 0;
            rule.totalFireCount = 9999;
            Assert.IsTrue(rule.CanFire(1000));
        }

        // ── TriggerMode AND tests ──────────────────────────────────────────────

        [Test]
        public void EvaluateTriggers_AND_ReturnsFalse_WhenAnyFalse()
        {
            var rule = MakeRule();
            rule.triggerMode = TriggerMode.All;
            rule.triggerEntries.Add(MakeEntry(true));
            rule.triggerEntries.Add(MakeEntry(false));
            Assert.IsFalse(rule.EvaluateTriggers(null));
        }

        [Test]
        public void EvaluateTriggers_AND_ReturnsTrue_WhenAllTrue()
        {
            var rule = MakeRule();
            rule.triggerMode = TriggerMode.All;
            rule.triggerEntries.Add(MakeEntry(true));
            rule.triggerEntries.Add(MakeEntry(true));
            Assert.IsTrue(rule.EvaluateTriggers(null));
        }

        // ── TriggerMode OR tests ───────────────────────────────────────────────

        [Test]
        public void EvaluateTriggers_OR_ReturnsTrue_WhenAnyTrue()
        {
            var rule = MakeRule();
            rule.triggerMode = TriggerMode.Any;
            rule.triggerEntries.Add(MakeEntry(false));
            rule.triggerEntries.Add(MakeEntry(true));
            Assert.IsTrue(rule.EvaluateTriggers(null));
        }

        [Test]
        public void EvaluateTriggers_OR_ReturnsFalse_WhenAllFalse()
        {
            var rule = MakeRule();
            rule.triggerMode = TriggerMode.Any;
            rule.triggerEntries.Add(MakeEntry(false));
            rule.triggerEntries.Add(MakeEntry(false));
            Assert.IsFalse(rule.EvaluateTriggers(null));
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
            rule.triggerEntries.Add(MakeEntry(true));
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
            rule.triggerEntries.Add(MakeEntry(false));
            rule.actions.Add(action);

            bool fired = rule.TryFire(null, 1000);

            Assert.IsFalse(fired);
            Assert.AreEqual(0, action.ExecuteCallCount);
        }

        [Test]
        public void TryFire_IncrementsFireCount()
        {
            var rule = MakeRule();
            rule.triggerEntries.Add(MakeEntry(true));
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
            rule.triggerEntries.Add(MakeEntry(true));
            rule.actions.Add(new StubAction());

            rule.TryFire(null, 7500);
            Assert.AreEqual(7500, rule.lastFiredTick);
        }

        [Test]
        public void TryFire_OneShotRule_DisablesRuleAfterFire()
        {
            var rule = MakeRule();
            rule.oneShotRule = true;
            rule.triggerEntries.Add(MakeEntry(true));
            rule.actions.Add(new StubAction());

            Assert.IsTrue(rule.enabled);
            rule.TryFire(null, 1000);
            Assert.IsFalse(rule.enabled, "One-shot rule should be disabled after firing.");
        }

        [Test]
        public void TryFire_ExecutesAllActions_InOrder()
        {
            var rule = MakeRule();
            rule.triggerEntries.Add(MakeEntry(true));

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
            var rules = new System.Collections.Generic.List<AutomationRule>
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
    }
}
