using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Controls transport shuttles on the map.
    /// Supports launching, holding, and unloading shuttles programmatically.
    ///
    /// Requires Royalty DLC for most operations (FlyAway, WaitForever).
    ///
    /// Modes:
    ///   Launch      — fly away (no destination; shuttle departs)
    ///   LaunchTo    — fly to a specific player settlement (configurable)
    ///   Hold        — force shuttle to wait indefinitely (cancel auto-departure)
    ///   Unload      — force shuttle to unload all cargo
    ///
    /// Target filter:
    ///   AnyWaiting       — first waiting shuttle on map
    ///   PlayerShuttle    — only player-owned shuttle (Odyssey)
    ///   AllWaiting       — all waiting shuttles
    /// </summary>
    public class Action_ShuttleControl : AutomationAction
    {
        // ── Enums ────────────────────────────────────────────────────────────
        public enum ShuttleControlMode
        {
            Launch,       // ForceJob(FlyAway) — shuttle departs with no destination
            LaunchTo,     // ForceJob(FlyAway) to a specific player settlement
            Hold,         // ForceJob(WaitForever) — cancel auto-departure
            Unload,       // ForceJob(Unload) — drop cargo at interaction cell
        }

        public enum ShuttleTarget
        {
            AnyWaiting,     // first waiting shuttle
            PlayerShuttle,  // only player-owned (Odyssey permitShuttle)
            AllWaiting,     // all waiting shuttles on map
        }

        // ── Config ───────────────────────────────────────────────────────────
        public ShuttleControlMode controlMode       = ShuttleControlMode.Launch;
        public ShuttleTarget      targetFilter      = ShuttleTarget.AnyWaiting;
        public string             destinationMapName = "";  // settlement name for LaunchTo

        // ── Identity ─────────────────────────────────────────────────────────
        public override string Label => "Shuttle control";
        public override string Description
        {
            get
            {
                string modeStr = controlMode switch
                {
                    ShuttleControlMode.Launch   => "Launch (fly away)",
                    ShuttleControlMode.LaunchTo => destinationMapName.NullOrEmpty()
                        ? "Launch to (no destination set)"
                        : $"Launch to '{destinationMapName}'",
                    ShuttleControlMode.Hold     => "Hold (wait indefinitely)",
                    ShuttleControlMode.Unload   => "Unload cargo",
                    _ => controlMode.ToString(),
                };
                string targetStr = targetFilter switch
                {
                    ShuttleTarget.AnyWaiting    => "any waiting",
                    ShuttleTarget.PlayerShuttle => "player shuttle",
                    ShuttleTarget.AllWaiting    => "all waiting",
                    _ => targetFilter.ToString(),
                };
                return $"Shuttle: {modeStr} ({targetStr})";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight =>
            controlMode == ShuttleControlMode.LaunchTo ? 140f : 108f;

        // ── Execute ──────────────────────────────────────────────────────────
        public override bool Execute(Map map)
        {
            var manager = Find.TransportShipManager;
            if (manager == null) return false;

            // Filter to ships on THIS map that are spawned and waiting
            var candidates = manager.AllTransportShips
                .Where(s => s.ShipExistsAndIsSpawned
                         && s.shipThing?.Map == map
                         && s.Waiting)
                .ToList();

            if (candidates.Count == 0)
            {
                if (AutomationGameComp.Instance?.verboseLogging == true)
                    Log.Message("[IFTTT] ShuttleControl: No waiting shuttles on map.");
                return false;
            }

            // Apply target filter
            List<TransportShip> targets;
            switch (targetFilter)
            {
                case ShuttleTarget.PlayerShuttle:
                    targets = candidates
                        .Where(s => s.ShuttleComp?.permitShuttle == true)
                        .ToList();
                    break;

                case ShuttleTarget.AllWaiting:
                    targets = candidates;
                    break;

                case ShuttleTarget.AnyWaiting:
                default:
                    targets = new List<TransportShip>();
                    if (candidates.Count > 0)
                        targets.Add(candidates[0]);
                    break;
            }

            if (targets.Count == 0) return false;

            int acted = 0;
            foreach (TransportShip ship in targets)
            {
                bool success = ApplyControl(ship, map);
                if (success) acted++;
            }

            if (acted > 0)
            {
                string verb = controlMode switch
                {
                    ShuttleControlMode.Launch   => "launched",
                    ShuttleControlMode.LaunchTo => $"launched → {destinationMapName}",
                    ShuttleControlMode.Hold     => "held",
                    ShuttleControlMode.Unload   => "unloading",
                    _ => "controlled",
                };
                Messages.Message(
                    $"[IFTTT] {acted} shuttle(s) {verb}.",
                    MessageTypeDefOf.NeutralEvent, historical: false);
            }

            return acted > 0;
        }

        private bool ApplyControl(TransportShip ship, Map map)
        {
            switch (controlMode)
            {
                case ShuttleControlMode.Launch:
                    // Simple fly away — shuttle departs with no destination
                    ship.ForceJob(ShipJobDefOf.FlyAway);
                    return true;

                case ShuttleControlMode.LaunchTo:
                    // Fly to a specific player settlement by name
                    if (destinationMapName.NullOrEmpty())
                    {
                        Log.Warning("[IFTTT] ShuttleControl: LaunchTo has no destination set — flying away instead.");
                        ship.ForceJob(ShipJobDefOf.FlyAway);
                        return true;
                    }

                    // Find the destination settlement by name
                    MapParent destParent = Find.WorldObjects.Settlements
                        .OfType<MapParent>()
                        .FirstOrDefault(s => s.Faction == Faction.OfPlayer
                                          && s.Label == destinationMapName
                                          && s.HasMap);

                    if (destParent == null || destParent.Map == map)
                    {
                        // Destination not found, not loaded, or is this map — just fly away
                        if (destParent == null)
                            Log.Warning($"[IFTTT] ShuttleControl: Destination '{destinationMapName}' not found or not loaded.");
                        ship.ForceJob(ShipJobDefOf.FlyAway);
                        return true;
                    }

                    ShipJob flyJob = ShipJobMaker.MakeShipJob(ShipJobDefOf.FlyAway);
                    if (flyJob is ShipJob_FlyAway flyAway)
                    {
                        flyAway.destinationTile = destParent.Map.Tile;
                        flyAway.arrivalAction = new TransportersArrivalAction_TransportShip(
                            destParent, ship);
                        flyAway.dropMode = TransportShipDropMode.None;
                    }
                    ship.ForceJob(flyJob);
                    return true;

                case ShuttleControlMode.Hold:
                    // Force shuttle to wait indefinitely — cancels auto-departure
                    ship.ForceJob(ShipJobDefOf.WaitForever);
                    return true;

                case ShuttleControlMode.Unload:
                    // Force shuttle to unload cargo
                    ShipJob unloadJob = ShipJobMaker.MakeShipJob(ShipJobDefOf.Unload);
                    ship.ForceJob(unloadJob);
                    return true;

                default:
                    return false;
            }
        }

        // ── UI ───────────────────────────────────────────────────────────────
        public override void DrawConfig(Listing_Standard listing)
        {
            // Row 1: Control mode
            listing.Label("Action:");
            string modeBtnLabel = controlMode switch
            {
                ShuttleControlMode.Launch   => "Launch (fly away)",
                ShuttleControlMode.LaunchTo => "Launch to settlement",
                ShuttleControlMode.Hold     => "Hold (wait forever)",
                ShuttleControlMode.Unload   => "Unload cargo",
                _ => controlMode.ToString(),
            };
            if (Widgets.ButtonText(listing.GetRect(28f), modeBtnLabel))
            {
                var opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Launch (fly away)",
                        () => controlMode = ShuttleControlMode.Launch),
                    new FloatMenuOption("Launch to settlement",
                        () => controlMode = ShuttleControlMode.LaunchTo),
                    new FloatMenuOption("Hold (wait forever)",
                        () => controlMode = ShuttleControlMode.Hold),
                    new FloatMenuOption("Unload cargo",
                        () => controlMode = ShuttleControlMode.Unload),
                };
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            // Row 1b: Destination picker (only for LaunchTo)
            if (controlMode == ShuttleControlMode.LaunchTo)
            {
                listing.Gap(4f);
                listing.Label("Destination:");
                string destLabel = destinationMapName.NullOrEmpty()
                    ? "(select settlement)"
                    : destinationMapName;
                if (Widgets.ButtonText(listing.GetRect(28f), destLabel))
                {
                    var opts = new List<FloatMenuOption>();
                    var settlements = Find.WorldObjects.Settlements
                        .Where(s => s.Faction == Faction.OfPlayer)
                        .OrderBy(s => s.Label);
                    foreach (var settlement in settlements)
                    {
                        string name = settlement.Label;
                        opts.Add(new FloatMenuOption(name, () => destinationMapName = name));
                    }
                    if (opts.Count == 0)
                        opts.Add(new FloatMenuOption("(no player settlements found)", null)
                            { Disabled = true });
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            }

            listing.Gap(4f);

            // Row 2: Target filter
            listing.Label("Target:");
            string targetBtnLabel = targetFilter switch
            {
                ShuttleTarget.AnyWaiting    => "Any waiting shuttle",
                ShuttleTarget.PlayerShuttle => "Player shuttle only",
                ShuttleTarget.AllWaiting    => "All waiting shuttles",
                _ => targetFilter.ToString(),
            };
            if (Widgets.ButtonText(listing.GetRect(28f), targetBtnLabel))
            {
                var opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Any waiting shuttle",
                        () => targetFilter = ShuttleTarget.AnyWaiting),
                    new FloatMenuOption("Player shuttle only (Odyssey)",
                        () => targetFilter = ShuttleTarget.PlayerShuttle),
                    new FloatMenuOption("All waiting shuttles",
                        () => targetFilter = ShuttleTarget.AllWaiting),
                };
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        // ── Persistence ──────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref controlMode,       "controlMode",       ShuttleControlMode.Launch);
            Scribe_Values.Look(ref targetFilter,      "targetFilter",      ShuttleTarget.AnyWaiting);
            Scribe_Values.Look(ref destinationMapName, "destinationMapName", "");
        }
    }
}
