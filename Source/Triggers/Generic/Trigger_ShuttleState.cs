using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Trigger that checks the state of transport ships (shuttles) on the map.
    /// Requires Royalty or Biotech DLC for shuttle support.
    ///
    /// Queryable states:
    ///  - AnyShuttleDocked:  any shuttle is spawned and waiting on this map
    ///  - AnyShuttleLoading: any shuttle has loading in progress
    ///  - AnyShuttleReady:   any shuttle is fully loaded and ready to launch
    ///  - NoShuttleDocked:   no shuttles present on this map
    ///  - ShuttleWaiting:    a player-controlled shuttle is waiting (no auto-leave timer)
    ///  - ShuttleLeavingSoon: a shuttle is about to depart automatically
    /// </summary>
    public class Trigger_ShuttleState : AutomationTrigger
    {
        public override string Label => "Shuttle state";
        public override string Description
        {
            get
            {
                return $"Transport shuttle is: {shuttleState}";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight => 62f;

        // ── Config ───────────────────────────────────────────────────────────
        public ShuttleQueryState shuttleState = ShuttleQueryState.AnyShuttleDocked;

        // ── IsTriggered ──────────────────────────────────────────────────────
        public override bool IsTriggered(Map map)
        {
            // TransportShipManager may not exist if DLC not loaded
            var manager = Find.TransportShipManager;
            if (manager == null) return false;

            List<TransportShip> allShips = manager.AllTransportShips;
            if (allShips == null) return false;

            // Filter to ships that are on THIS map
            var shipsOnMap = allShips
                .Where(s => s.ShipExistsAndIsSpawned && s.shipThing?.Map == map)
                .ToList();

            switch (shuttleState)
            {
                case ShuttleQueryState.AnyShuttleDocked:
                    return shipsOnMap.Any(s => s.Waiting);

                case ShuttleQueryState.AnyShuttleLoading:
                    return shipsOnMap.Any(s =>
                        s.TransporterComp?.LoadingInProgressOrReadyToLaunch == true);

                case ShuttleQueryState.AnyShuttleReady:
                    return shipsOnMap.Any(s =>
                        s.ShuttleComp?.AllRequiredThingsLoaded == true);

                case ShuttleQueryState.NoShuttleDocked:
                    return shipsOnMap.Count == 0;

                case ShuttleQueryState.ShuttleWaiting:
                    // Player-controlled shuttle waiting indefinitely
                    return shipsOnMap.Any(s =>
                        s.Waiting && !s.LeavingSoonAutomatically);

                case ShuttleQueryState.ShuttleLeavingSoon:
                    return shipsOnMap.Any(s => s.LeavingSoonAutomatically);

                default:
                    return false;
            }
        }

        // ── UI ───────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            listing.Label("Shuttle condition:");
            string btn = shuttleState.ToString();
            if (Widgets.ButtonText(listing.GetRect(28f), FormatStateName(shuttleState)))
            {
                var opts = new List<FloatMenuOption>();
                foreach (ShuttleQueryState st in System.Enum.GetValues(typeof(ShuttleQueryState)))
                {
                    ShuttleQueryState cap = st;
                    opts.Add(new FloatMenuOption(FormatStateName(cap),
                        () => shuttleState = cap));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        private static string FormatStateName(ShuttleQueryState st)
        {
            switch (st)
            {
                case ShuttleQueryState.AnyShuttleDocked:    return "Any shuttle docked (waiting)";
                case ShuttleQueryState.AnyShuttleLoading:   return "Any shuttle loading";
                case ShuttleQueryState.AnyShuttleReady:     return "Any shuttle ready to launch";
                case ShuttleQueryState.NoShuttleDocked:     return "No shuttles on map";
                case ShuttleQueryState.ShuttleWaiting:      return "Player shuttle waiting (no timer)";
                case ShuttleQueryState.ShuttleLeavingSoon:  return "Shuttle leaving soon (auto)";
                default: return st.ToString();
            }
        }

        // ── Persistence ──────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref shuttleState, "shuttleState", ShuttleQueryState.AnyShuttleDocked);
        }
    }

    /// <summary>
    /// Queryable shuttle states for <see cref="Trigger_ShuttleState"/>.
    /// </summary>
    public enum ShuttleQueryState
    {
        AnyShuttleDocked,
        AnyShuttleLoading,
        AnyShuttleReady,
        NoShuttleDocked,
        ShuttleWaiting,
        ShuttleLeavingSoon,
    }
}
