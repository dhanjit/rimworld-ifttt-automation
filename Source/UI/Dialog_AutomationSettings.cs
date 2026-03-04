using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.UI
{
    /// <summary>
    /// Settings dialog for the Automation mod.
    /// Controls check interval, verbose logging, and global enable toggle.
    /// </summary>
    public class Dialog_AutomationSettings : Window
    {
        public override Vector2 InitialSize => new Vector2(420f, 280f);

        private readonly AutomationGameComp comp;
        private string intervalBuf;

        public Dialog_AutomationSettings(AutomationGameComp comp)
        {
            this.comp    = comp;
            intervalBuf  = comp.checkIntervalTicks.ToString();
            doCloseButton           = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 32f), "Automation Settings");
            Text.Font = GameFont.Small;

            Listing_Standard ls = new Listing_Standard();
            ls.Begin(new Rect(0, 40f, inRect.width, inRect.height - 80f));

            ls.Label("Check interval (ticks):");
            ls.Label("  250 ticks ≈ 4 real-seconds | 2500 = ~1 in-game hour");
            ls.TextFieldNumeric(ref comp.checkIntervalTicks, ref intervalBuf, 10, 60000);
            ls.Gap(8f);

            ls.CheckboxLabeled("Verbose logging (logs every rule evaluation — can spam console!)",
                ref comp.verboseLogging);
            ls.Gap(8f);

            ls.Label($"Rules loaded: {comp.rules.Count}");
            ls.Label($"Recent events tracked: {comp.recentEvents.Count}");

            ls.End();

            if (Widgets.ButtonText(new Rect(inRect.width - 100f, inRect.yMax - 36f, 100f, 32f), "Close"))
                Close();
        }
    }
}
