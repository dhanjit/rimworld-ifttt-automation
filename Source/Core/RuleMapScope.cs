namespace RimWorldIFTTT
{
    /// <summary>
    /// Determines which maps an automation rule is evaluated against.
    ///
    /// RimWorld 1.6 (Odyssey) supports multiple simultaneous player settlements.
    /// This enum lets each rule target the right map(s).
    /// </summary>
    public enum RuleMapScope
    {
        /// <summary>
        /// Evaluated on the "primary" home map only — matches legacy (pre-multi-settlement) behaviour.
        /// Uses a single shared cooldown. Best for single-colony saves or colony-wide rules that
        /// only make sense for the main base.
        /// </summary>
        AnyHomeMap,

        /// <summary>
        /// Evaluated independently on every loaded player home map.
        /// Each settlement has its own per-tile cooldown so the same rule can fire on
        /// settlement A at T=0 and fire again on settlement B at T=0 without interference.
        /// Use this for rules that should apply equally to every colony (e.g. "restrict colonists
        /// when under attack" — you want that to happen at all bases, not just the first one).
        /// </summary>
        AllHomeMaps,

        /// <summary>
        /// Pinned to a single settlement identified by its world-tile index.
        /// Useful when two settlements have very different roles and you need a rule
        /// that only applies to one of them (e.g. "mine-site base" vs "main base").
        /// Uses a single shared cooldown.
        /// </summary>
        SpecificMap,
    }
}
