using Verse;

namespace RimWorldIFTTT.Tests.Stubs
{
    /// <summary>
    /// A minimal stub trigger whose result is controlled by a field.
    /// Does NOT call any RimWorld game APIs.
    /// </summary>
    public class StubTrigger : AutomationTrigger
    {
        public bool ReturnValue { get; set; }

        public override string Label       => "Stub Trigger";
        public override string Description => "Stub trigger for testing.";
        public override bool   HasConfig   => false;

        public override bool IsTriggered(Map map) => ReturnValue;
    }
}
