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
    ///   AnyWaiting    — first waiting shuttle on map
    ///   ByName        — match by shuttle name (Odyssey renamed shuttles)
    ///   PlayerBuilt   — Odyssey-built shuttles only (def.playerShuttle)
    ///   Permit        — Royalty permit shuttles only (CompShuttle.permitShuttle)
    ///   AllWaiting    — all waiting shuttles on map
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
            ByName,         // match by Building_PassengerShuttle.Label (Odyssey)
            PlayerBuilt,    // Odyssey-built shuttles (def.playerShuttle)
            Permit,         // Royalty permit shuttles (CompShuttle.permitShuttle)
            AllWaiting,     // all waiting shuttles on map
        }

        // ── Config ───────────────────────────────────────────────────────────
        public ShuttleControlMode controlMode       = ShuttleControlMode.Launch;
        public ShuttleTarget      targetFilter      = ShuttleTarget.AnyWaiting;
        public string             destinationMapName = "";  // settlement name for LaunchTo
        public string             shuttleName        = "";  // shuttle name for ByName filter

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
                    ShuttleTarget.AnyWaiting   => "any waiting",
                    ShuttleTarget.ByName       => shuttleName.NullOrEmpty() ? "by name (none)" : $"'{shuttleName}'",
                    ShuttleTarget.PlayerBuilt  => "player-built",
                    ShuttleTarget.Permit       => "permit shuttle",
                    ShuttleTarget.AllWaiting   => "all waiting",
                    _ => targetFilter.ToString(),
                };
                return $"Shuttle: {modeStr} ({targetStr})";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight
        {
            get
            {
                float h = 108f;  // base: mode dropdown + target dropdown + labels
                if (controlMode == ShuttleControlMode.LaunchTo) h += 32f;  // destination picker
                if (targetFilter == ShuttleTarget.ByName) h += 32f;        // name picker
                return h;
            }
        }

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
            List<TransportShip> targets = ApplyTargetFilter(candidates);

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

        private List<TransportShip> ApplyTargetFilter(List<TransportShip> candidates)
        {
            switch (targetFilter)
            {
                case ShuttleTarget.ByName:
                    if (shuttleName.NullOrEmpty()) return new List<TransportShip>();
                    return candidates
                        .Where(s => s.shipThing?.Label == shuttleName)
                        .Take(1).ToList();

                case ShuttleTarget.PlayerBuilt:
                    // Odyssey-built shuttles: def.playerShuttle = true
                    return candidates
                        .Where(s => s.def?.playerShuttle == true)
                        .ToList();

                case ShuttleTarget.Permit:
                    // Royalty permit shuttles: CompShuttle.permitShuttle = true
                    return candidates
                        .Where(s => s.ShuttleComp?.permitShuttle == true)
                        .ToList();

                case ShuttleTarget.AllWaiting:
                    return candidates;

                case ShuttleTarget.AnyWaiting:
                default:
                    return candidates.Count > 0
                        ? new List<TransportShip> { candidates[0] }
                        : new List<TransportShip>();
            }
        }

        private bool ApplyControl(TransportShip ship, Map map)
        {
            switch (controlMode)
            {
                case ShuttleControlMode.Launch:
                    ship.ForceJob(ShipJobDefOf.FlyAway);
                    return true;

                case ShuttleControlMode.LaunchTo:
                    if (destinationMapName.NullOrEmpty())
                    {
                        Log.Warning("[IFTTT] ShuttleControl: LaunchTo has no destination set — flying away instead.");
                        ship.ForceJob(ShipJobDefOf.FlyAway);
                        return true;
                    }

                    // Find destination: player settlements + space map parents
                    MapParent destParent = Find.WorldObjects.AllWorldObjects
                        .OfType<MapParent>()
                        .FirstOrDefault(mp => mp.Faction == Faction.OfPlayer
                                           && mp.Label == destinationMapName
                                           && mp.HasMap);

                    if (destParent == null || destParent.Map == map)
                    {
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
                    ship.ForceJob(ShipJobDefOf.WaitForever);
                    return true;

                case ShuttleControlMode.Unload:
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

            // Destination picker (only for LaunchTo)
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
                    // Include both settlements and space map parents
                    var playerBases = Find.WorldObjects.AllWorldObjects
                        .OfType<MapParent>()
                        .Where(mp => mp.Faction == Faction.OfPlayer)
                        .OrderBy(mp => mp.Label);
                    foreach (var mp in playerBases)
                    {
                        string name = mp.Label;
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
                ShuttleTarget.AnyWaiting   => "Any waiting shuttle",
                ShuttleTarget.ByName       => "Shuttle by name",
                ShuttleTarget.PlayerBuilt  => "Player-built (Odyssey)",
                ShuttleTarget.Permit       => "Permit shuttle (Royalty)",
                ShuttleTarget.AllWaiting   => "All waiting shuttles",
                _ => targetFilter.ToString(),
            };
            if (Widgets.ButtonText(listing.GetRect(28f), targetBtnLabel))
            {
                var opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Any waiting shuttle",
                        () => targetFilter = ShuttleTarget.AnyWaiting),
                    new FloatMenuOption("Shuttle by name (Odyssey)",
                        () => targetFilter = ShuttleTarget.ByName),
                    new FloatMenuOption("Player-built (Odyssey)",
                        () => targetFilter = ShuttleTarget.PlayerBuilt),
                    new FloatMenuOption("Permit shuttle (Royalty)",
                        () => targetFilter = ShuttleTarget.Permit),
                    new FloatMenuOption("All waiting shuttles",
                        () => targetFilter = ShuttleTarget.AllWaiting),
                };
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            // Name picker (only for ByName)
            if (targetFilter == ShuttleTarget.ByName)
            {
                listing.Gap(4f);
                listing.Label("Shuttle name:");
                string nameLabel = shuttleName.NullOrEmpty()
                    ? "(select shuttle)"
                    : shuttleName;
                if (Widgets.ButtonText(listing.GetRect(28f), nameLabel))
                {
                    var opts = new List<FloatMenuOption>();
                    var ships = Find.TransportShipManager?.AllTransportShips;
                    if (ships != null)
                    {
                        // Collect unique shuttle names from all spawned shuttles
                        var names = ships
                            .Where(s => s.ShipExistsAndIsSpawned && s.shipThing != null)
                            .Select(s => s.shipThing.Label)
                            .Where(n => !n.NullOrEmpty())
                            .Distinct()
                            .OrderBy(n => n);
                        foreach (string n in names)
                        {
                            string captured = n;
                            opts.Add(new FloatMenuOption(captured, () => shuttleName = captured));
                        }
                    }
                    if (opts.Count == 0)
                        opts.Add(new FloatMenuOption("(no shuttles found — load a game first)", null)
                            { Disabled = true });
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            }
        }

        // ── Persistence ──────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref controlMode,        "controlMode",        ShuttleControlMode.Launch);
            Scribe_Values.Look(ref targetFilter,       "targetFilter",       ShuttleTarget.AnyWaiting);
            Scribe_Values.Look(ref destinationMapName,  "destinationMapName",  "");
            Scribe_Values.Look(ref shuttleName,         "shuttleName",         "");
        }
    }
}
