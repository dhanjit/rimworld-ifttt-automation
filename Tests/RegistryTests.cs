using System;
using System.Linq;
using NUnit.Framework;

namespace RimWorldIFTTT.Tests
{
    /// <summary>
    /// Verifies that all registered trigger/action types can be instantiated,
    /// and that registry labels are non-empty.
    /// These tests do not require a live game — only the DLL reflection.
    /// </summary>
    [TestFixture]
    public class RegistryTests
    {
        [Test]
        public void TriggerRegistry_HasAtLeastTenTypes()
        {
            Assert.GreaterOrEqual(TriggerRegistry.AllTypes.Count, 10,
                "Expected at least 10 trigger types registered.");
        }

        [Test]
        public void ActionRegistry_HasAtLeastTenTypes()
        {
            Assert.GreaterOrEqual(ActionRegistry.AllTypes.Count, 10,
                "Expected at least 10 action types registered.");
        }

        [Test]
        public void TriggerRegistry_AllTypesInstantiable()
        {
            foreach (Type t in TriggerRegistry.AllTypes)
            {
                AutomationTrigger inst = null;
                Assert.DoesNotThrow(
                    () => inst = TriggerRegistry.CreateInstance(t),
                    $"Trigger type '{t.Name}' could not be instantiated.");
                Assert.IsNotNull(inst, $"Instance of '{t.Name}' was null.");
            }
        }

        [Test]
        public void ActionRegistry_AllTypesInstantiable()
        {
            foreach (Type t in ActionRegistry.AllTypes)
            {
                AutomationAction inst = null;
                Assert.DoesNotThrow(
                    () => inst = ActionRegistry.CreateInstance(t),
                    $"Action type '{t.Name}' could not be instantiated.");
                Assert.IsNotNull(inst, $"Instance of '{t.Name}' was null.");
            }
        }

        [Test]
        public void TriggerRegistry_AllLabelsNonEmpty()
        {
            foreach (Type t in TriggerRegistry.AllTypes)
            {
                string label = TriggerRegistry.GetLabel(t);
                Assert.IsFalse(string.IsNullOrWhiteSpace(label),
                    $"Trigger '{t.Name}' has empty or null label.");
            }
        }

        [Test]
        public void ActionRegistry_AllLabelsNonEmpty()
        {
            foreach (Type t in ActionRegistry.AllTypes)
            {
                string label = ActionRegistry.GetLabel(t);
                Assert.IsFalse(string.IsNullOrWhiteSpace(label),
                    $"Action '{t.Name}' has empty or null label.");
            }
        }

        [Test]
        public void TriggerRegistry_NoDuplicateTypes()
        {
            var duplicates = TriggerRegistry.AllTypes
                .GroupBy(t => t)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.Name)
                .ToList();

            Assert.IsEmpty(duplicates,
                $"Duplicate trigger types found: {string.Join(", ", duplicates)}");
        }

        [Test]
        public void ActionRegistry_NoDuplicateTypes()
        {
            var duplicates = ActionRegistry.AllTypes
                .GroupBy(t => t)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.Name)
                .ToList();

            Assert.IsEmpty(duplicates,
                $"Duplicate action types found: {string.Join(", ", duplicates)}");
        }
    }
}
