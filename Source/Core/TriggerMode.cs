namespace RimWorldIFTTT
{
    /// <summary>
    /// Controls how multiple triggers on a single rule are combined.
    /// </summary>
    public enum TriggerMode
    {
        /// <summary>ALL triggers must be firing simultaneously (logical AND).</summary>
        All = 0,

        /// <summary>ANY one trigger firing is enough (logical OR).</summary>
        Any = 1,
    }
}
