using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Forbids or unforbids all items in the home zone.
    /// Useful for lockdowns or re-allowing after emergencies.
    /// Can filter to a specific item category.
    /// </summary>
    public class Action_ForbidAllItems : AutomationAction
    {
        public override string Label       => "Forbid/unforbid items";
        public override string Description => "Toggles the forbidden status of all items in the home zone.";

        public override bool HasConfig => true;

        public bool forbid          = true;
        public bool homeZoneOnly    = true;

        public override void Execute(Map map)
        {
            IEnumerable<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                .Where(t => t.Spawned && !t.Destroyed);

            if (homeZoneOnly)
                things = things.Where(t => map.areaManager.Home[t.Position]);

            int count = 0;
            foreach (Thing t in things)
            {
                if (t.IsForbidden(Faction.OfPlayer) != forbid)
                {
                    t.SetForbidden(forbid);
                    count++;
                }
            }

            Messages.Message(
                $"[IFTTT] {(forbid ? "Forbidden" : "Unforbidden")} {count} item(s).",
                MessageTypeDefOf.NeutralEvent, historical: false);
        }

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.CheckboxLabeled("Forbid items (unchecked = unforbid)", ref forbid);
            listing.CheckboxLabeled("Home zone only (unchecked = entire map)", ref homeZoneOnly);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref forbid,       "forbid",       true);
            Scribe_Values.Look(ref homeZoneOnly, "homeZoneOnly", true);
        }
    }
}
