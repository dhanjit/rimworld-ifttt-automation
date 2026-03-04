using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Contacts the first available orbital trade ship via the comms console.
    /// Requires a powered comms console and a passing trade ship.
    /// </summary>
    public class Action_ContactOrbitalTrader : AutomationAction
    {
        public override string Label       => "Contact orbital trader";
        public override string Description => "Uses comms console to open trade with an orbital ship.";

        public override bool HasConfig => false;

        public override void Execute(Map map)
        {
            // Find a powered comms console
            Building_CommsConsole comms = map.listerBuildings
                .AllBuildingsColonistOfClass<Building_CommsConsole>()
                .FirstOrDefault(b => b.Spawned
                             && b.TryGetComp<CompPowerTrader>() is CompPowerTrader pt && pt.PowerOn);

            if (comms == null)
            {
                Messages.Message(
                    "[IFTTT] ContactOrbitalTrader: No powered comms console available.",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // Find a passing trade ship
            TradeShip ship = map.passingShipManager?.passingShips
                ?.OfType<TradeShip>()
                .FirstOrDefault();

            if (ship == null)
            {
                Messages.Message(
                    "[IFTTT] ContactOrbitalTrader: No trade ships currently in orbit.",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // Find the best negotiator (highest social skill colonist near comms)
            Pawn negotiator = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && p.skills != null)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level)
                .FirstOrDefault();

            if (negotiator == null)
            {
                Messages.Message(
                    "[IFTTT] ContactOrbitalTrader: No available colonist to negotiate.",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Find.WindowStack.Add(new Dialog_Trade(negotiator, ship));

            Messages.Message(
                $"[IFTTT] {negotiator.LabelShort} opened trade with {ship.TraderName}.",
                MessageTypeDefOf.PositiveEvent, historical: false);
        }
    }
}
