using System.Reflection;
using Verse;

namespace RimWorldIFTTT
{
    /// <summary>
    /// Abstract base class for automation actions (the THEN side of a rule).
    /// Subclass this and register in ActionRegistry to add a new action type.
    /// </summary>
    public abstract class AutomationAction : IExposable
    {
        /// <summary>Short label shown in the rule list (e.g. "Use skill trainer").</summary>
        public abstract string Label { get; }

        /// <summary>Longer description shown in the add/edit dialog.</summary>
        public abstract string Description { get; }

        /// <summary>
        /// Execute this action on the given map.
        /// Should be safe to call even if conditions are no longer ideal.
        /// Returns true if the action did useful work (found targets, dispatched jobs, etc.),
        /// or false if there was nothing to do (no eligible targets, condition already met, etc.).
        /// </summary>
        public abstract bool Execute(Map map);

        /// <summary>Save/load action-specific fields.</summary>
        public virtual void ExposeData() { }

        /// <summary>
        /// Draw any configuration controls using the provided listing.
        /// Called from the Add/Edit dialog.
        /// </summary>
        public virtual void DrawConfig(Listing_Standard listing) { }

        /// <summary>True if this action type needs any configuration beyond its defaults.</summary>
        public virtual bool HasConfig => false;

        /// <summary>
        /// Height in pixels that DrawConfig needs inside the Add/Edit dialog.
        /// Override this whenever DrawConfig draws more or fewer rows than the default (2 rows ≈ 55 px).
        /// Used by the dialog to allocate the correct entry height and scroll-view size.
        /// </summary>
        public virtual float ConfigHeight => 55f;

        /// <summary>
        /// Creates a deep copy of this action by reflection-copying all instance fields
        /// that are not marked <see cref="System.NonSerializedAttribute"/>.
        /// Safe for primitive, string, enum, and struct fields (copies the value).
        /// Collection/object fields are shallow-copied (sufficient for scalar config actions).
        /// </summary>
        public virtual AutomationAction Clone()
        {
            var copy = (AutomationAction)System.Activator.CreateInstance(GetType());
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
