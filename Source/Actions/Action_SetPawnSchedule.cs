using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Sets a time-assignment (Work, Sleep, Recreation, Anything) for all colonists
    /// during a specific hour range. Good for optimizing work schedules.
    /// </summary>
    public class Action_SetPawnSchedule : AutomationAction
    {
        public override string Label       => "Set colonist schedule";
        public override string Description => "Sets a time-block assignment for all colonists.";

        public override bool HasConfig    => true;
        public override float ConfigHeight => 240f; // Label + Slider + Label + Slider + Label + 4 ButtonTexts

        public int  startHour  = 8;
        public int  endHour    = 20;
        public int  assignment = 0; // 0=Anything, 1=Work, 2=Recreation, 3=Sleep

        private static readonly string[] AssignmentLabels = { "Anything", "Work", "Joy", "Sleep" };

        private static TimeAssignmentDef GetAssignmentDef(int idx) => idx switch
        {
            1 => TimeAssignmentDefOf.Work,
            2 => TimeAssignmentDefOf.Joy,
            3 => TimeAssignmentDefOf.Sleep,
            _ => TimeAssignmentDefOf.Anything,
        };

        public override void Execute(Map map)
        {
            TimeAssignmentDef def = GetAssignmentDef(assignment);
            int count = 0;

            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.timetable == null) continue;
                for (int h = startHour; h != (endHour + 1) % 24; h = (h + 1) % 24)
                    p.timetable.SetAssignment(h, def);
                count++;
            }

            Messages.Message(
                $"[IFTTT] Set schedule to '{def.label}' for hours {startHour:D2}–{endHour:D2} on {count} colonist(s).",
                MessageTypeDefOf.NeutralEvent, historical: false);
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            float sH = startHour, eH = endHour;
            listing.Label($"Start hour: {startHour:D2}:00");
            startHour = (int)listing.Slider(sH, 0f, 23f);

            listing.Label($"End hour: {endHour:D2}:00");
            endHour = (int)listing.Slider(eH, 0f, 23f);

            listing.Label($"Assignment: {AssignmentLabels[assignment]}");
            for (int i = 0; i < AssignmentLabels.Length; i++)
            {
                int captured = i;
                if (listing.ButtonText(AssignmentLabels[i]))
                    assignment = captured;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startHour,  "startHour",  8);
            Scribe_Values.Look(ref endHour,    "endHour",    20);
            Scribe_Values.Look(ref assignment, "assignment", 0);
        }
    }
}
