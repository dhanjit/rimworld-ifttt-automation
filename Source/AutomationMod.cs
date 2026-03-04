using Verse;

namespace RimWorldIFTTT
{
    /// <summary>
    /// Mod entry point. Currently minimal — the heavy lifting is in AutomationGameComp.
    /// Could be expanded to hold per-install (non-save) settings.
    /// </summary>
    public class AutomationMod : Mod
    {
        public AutomationMod(ModContentPack content) : base(content)
        {
            Log.Message("[IFTTT] RimWorld Automation mod loaded.");
        }
    }
}
