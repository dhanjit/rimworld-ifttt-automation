using Verse;

namespace RimWorldIFTTT.Tests.Stubs
{
    /// <summary>
    /// A minimal stub action that records how many times it was executed.
    /// Does NOT call any RimWorld game APIs.
    /// </summary>
    public class StubAction : AutomationAction
    {
        public int ExecuteCallCount { get; private set; }

        public override string Label => "Stub Action";
        public override bool HasConfig => false;

        public override void Execute(Map map)
        {
            ExecuteCallCount++;
        }
    }
}
