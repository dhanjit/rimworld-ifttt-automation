using System.Reflection;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT
{
    /// <summary>
    /// Abstract base class for automation triggers (the IF side of a rule).
    /// Subclass this and register in TriggerRegistry to add a new trigger type.
    /// </summary>
    public abstract class AutomationTrigger : IExposable
    {
        /// <summary>Short label shown in the rule list (e.g. "Has skill trainer").</summary>
        public abstract string Label { get; }

        /// <summary>Longer description shown in the add/edit dialog.</summary>
        public abstract string Description { get; }

        /// <summary>
        /// Return true when this trigger's condition is currently met.
        /// Called every check interval by AutomationGameComp.
        /// </summary>
        public abstract bool IsTriggered(Map map);

        /// <summary>Save/load trigger-specific fields.</summary>
        public virtual void ExposeData() { }

        /// <summary>
        /// Draw any configuration controls inside the given rect.
        /// Called from the Add/Edit dialog.
        /// </summary>
        public virtual void DrawConfig(Listing_Standard listing) { }

        /// <summary>True if this trigger type needs any configuration beyond its defaults.</summary>
        public virtual bool HasConfig => false;

        /// <summary>
        /// Height in pixels that DrawConfig needs inside the Add/Edit dialog.
        /// Override this whenever DrawConfig draws more or fewer rows than the default (2 rows ≈ 55 px).
        /// Used by the dialog to allocate the correct entry height and scroll-view size.
        /// </summary>
        public virtual float ConfigHeight => 55f;

        /// <summary>
        /// Creates a deep copy of this trigger by reflection-copying all instance fields
        /// that are not marked <see cref="System.NonSerializedAttribute"/>.
        /// Safe for primitive, string, enum, and struct fields (copies the value).
        /// Collection/object fields are shallow-copied (sufficient for scalar config triggers).
        /// </summary>
        public virtual AutomationTrigger Clone()
        {
            var copy = (AutomationTrigger)System.Activator.CreateInstance(GetType());
            foreach (FieldInfo f in GetType().GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.GetCustomAttribute<System.NonSerializedAttribute>() != null) continue;
                f.SetValue(copy, f.GetValue(this));
            }
            return copy;
        }
    }
}
