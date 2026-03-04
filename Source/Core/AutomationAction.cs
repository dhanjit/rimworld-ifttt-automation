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
        /// </summary>
        public abstract void Execute(Map map);

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
    }
}
