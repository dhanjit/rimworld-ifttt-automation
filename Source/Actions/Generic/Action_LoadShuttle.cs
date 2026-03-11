using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Initiates shuttle loading — queues items for colonists to haul into the shuttle.
    ///
    /// Uses the vanilla TransporterUtility.InitiateLoading() + CompTransporter.AddToTheToLoadList()
    /// pipeline. Colonists with hauling enabled will physically carry items to the shuttle.
    ///
    /// Target shuttle uses the same filter pattern as Action_ShuttleControl.
    /// Items are specified by ThingDef + count.
    /// </summary>
    public class Action_LoadShuttle : AutomationAction
    {
        // ── Config ───────────────────────────────────────────────────────────
        public Action_ShuttleControl.ShuttleTarget targetFilter = Action_ShuttleControl.ShuttleTarget.AnyWaiting;
        public string shuttleName   = "";
        public string thingDefName  = "";   // what to load
        public int    loadCount     = 1;    // how many to load

        [Unsaved] private string _countBuffer;

        // ── Identity ─────────────────────────────────────────────────────────
        public override string Label => "Load shuttle";
        public override string Description
        {
            get
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
                string item = def != null ? def.label.CapitalizeFirst() : (thingDefName.NullOrEmpty() ? "???" : thingDefName);
                string targetStr = targetFilter switch
                {
                    Action_ShuttleControl.ShuttleTarget.AnyWaiting  => "any shuttle",
                    Action_ShuttleControl.ShuttleTarget.ByName      => shuttleName.NullOrEmpty() ? "by name (none)" : $"'{shuttleName}'",
                    Action_ShuttleControl.ShuttleTarget.PlayerBuilt => "player-built",
                    Action_ShuttleControl.ShuttleTarget.Permit      => "permit shuttle",
                    Action_ShuttleControl.ShuttleTarget.AllWaiting  => "all shuttles",
                    _ => targetFilter.ToString(),
                };
                return $"Load {loadCount}x {item} into {targetStr}";
            }
        }

        public override bool  HasConfig    => true;
        public override float ConfigHeight
        {
            get
            {
                float h = 130f;  // item picker + count + target dropdown + labels
                if (targetFilter == Action_ShuttleControl.ShuttleTarget.ByName) h += 32f;
                return h;
            }
        }

        // ── Execute ──────────────────────────────────────────────────────────
        public override bool Execute(Map map)
        {
            if (thingDefName.NullOrEmpty()) return false;
            ThingDef itemDef = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            if (itemDef == null) return false;

            var manager = Find.TransportShipManager;
            if (manager == null) return false;

            // Find target shuttle(s)
            var candidates = manager.AllTransportShips
                .Where(s => s.ShipExistsAndIsSpawned
                         && s.shipThing?.Map == map
                         && s.Waiting)
                .ToList();

            if (candidates.Count == 0) return false;

            TransportShip target = FindTarget(candidates);
            if (target == null) return false;

            CompTransporter transporter = target.shipThing?.TryGetComp<CompTransporter>();
            if (transporter == null) return false;

            // Initiate loading session if not already in progress
            if (!transporter.LoadingInProgressOrReadyToLaunch)
                TransporterUtility.InitiateLoading(Gen.YieldSingle(transporter));

            // Find items on the map to queue for loading
            var available = map.listerThings.ThingsOfDef(itemDef)
                .Where(t => t.Spawned && !t.Destroyed && !t.IsForbidden(Faction.OfPlayer))
                .ToList();

            if (available.Count == 0)
            {
                if (AutomationGameComp.Instance?.verboseLogging == true)
                    Log.Message($"[IFTTT] LoadShuttle: No {itemDef.label} available on map.");
                return false;
            }

            // Queue items for loading via AddToTheToLoadList
            int remaining = loadCount;
            foreach (Thing item in available)
            {
                if (remaining <= 0) break;
                int toLoad = Mathf.Min(remaining, item.stackCount);

                TransferableOneWay transferable = new TransferableOneWay();
                transferable.things.Add(item);
                transporter.AddToTheToLoadList(transferable, toLoad);

                remaining -= toLoad;
            }

            int queued = loadCount - remaining;
            if (queued > 0)
            {
                Messages.Message(
                    $"[IFTTT] Queued {queued}x {itemDef.label} for shuttle loading.",
                    MessageTypeDefOf.NeutralEvent, historical: false);
            }

            return queued > 0;
        }

        private TransportShip FindTarget(List<TransportShip> candidates)
        {
            switch (targetFilter)
            {
                case Action_ShuttleControl.ShuttleTarget.ByName:
                    if (shuttleName.NullOrEmpty()) return null;
                    return candidates.FirstOrDefault(s => s.shipThing?.Label == shuttleName);

                case Action_ShuttleControl.ShuttleTarget.PlayerBuilt:
                    return candidates.FirstOrDefault(s => s.def?.playerShuttle == true);

                case Action_ShuttleControl.ShuttleTarget.Permit:
                    return candidates.FirstOrDefault(s => s.ShuttleComp?.permitShuttle == true);

                case Action_ShuttleControl.ShuttleTarget.AllWaiting:
                    return candidates.FirstOrDefault(); // load into first — can't split across shuttles

                case Action_ShuttleControl.ShuttleTarget.AnyWaiting:
                default:
                    return candidates.FirstOrDefault();
            }
        }

        // ── UI ───────────────────────────────────────────────────────────────
        public override void DrawConfig(Listing_Standard listing)
        {
            // Item picker
            listing.Label("Item to load:");
            ThingDef cur = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            string itemLabel = cur != null ? cur.label.CapitalizeFirst()
                : (thingDefName.NullOrEmpty() ? "(select item)" : $"(unknown: {thingDefName})");
            if (Widgets.ButtonText(listing.GetRect(28f), itemLabel))
            {
                var opts = new List<FloatMenuOption>();
                foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => !d.label.NullOrEmpty() && d.category == ThingCategory.Item)
                    .OrderBy(d => d.label))
                {
                    string dn = d.defName;
                    opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(), () => thingDefName = dn));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            // Count
            if (_countBuffer == null) _countBuffer = loadCount.ToString();
            Rect countRow = listing.GetRect(28f);
            Rect lRect = new Rect(countRow.x, countRow.y, countRow.width * 0.35f, countRow.height);
            Rect fRect = new Rect(countRow.x + countRow.width * 0.35f, countRow.y, countRow.width * 0.65f, countRow.height);
            Widgets.Label(lRect, $"Count ({loadCount}):");
            string newBuf = Widgets.TextField(fRect, _countBuffer);
            if (newBuf != _countBuffer)
            {
                _countBuffer = newBuf;
                if (int.TryParse(newBuf, out int parsed) && parsed > 0)
                    loadCount = parsed;
            }

            listing.Gap(4f);

            // Target filter
            listing.Label("Target shuttle:");
            string targetBtnLabel = targetFilter switch
            {
                Action_ShuttleControl.ShuttleTarget.AnyWaiting  => "Any waiting shuttle",
                Action_ShuttleControl.ShuttleTarget.ByName      => "Shuttle by name",
                Action_ShuttleControl.ShuttleTarget.PlayerBuilt => "Player-built (Odyssey)",
                Action_ShuttleControl.ShuttleTarget.Permit      => "Permit shuttle (Royalty)",
                Action_ShuttleControl.ShuttleTarget.AllWaiting  => "All waiting shuttles",
                _ => targetFilter.ToString(),
            };
            if (Widgets.ButtonText(listing.GetRect(28f), targetBtnLabel))
            {
                var opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Any waiting shuttle",
                        () => targetFilter = Action_ShuttleControl.ShuttleTarget.AnyWaiting),
                    new FloatMenuOption("Shuttle by name (Odyssey)",
                        () => targetFilter = Action_ShuttleControl.ShuttleTarget.ByName),
                    new FloatMenuOption("Player-built (Odyssey)",
                        () => targetFilter = Action_ShuttleControl.ShuttleTarget.PlayerBuilt),
                    new FloatMenuOption("Permit shuttle (Royalty)",
                        () => targetFilter = Action_ShuttleControl.ShuttleTarget.Permit),
                    new FloatMenuOption("All waiting (first available)",
                        () => targetFilter = Action_ShuttleControl.ShuttleTarget.AllWaiting),
                };
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            // Name picker (ByName)
            if (targetFilter == Action_ShuttleControl.ShuttleTarget.ByName)
            {
                listing.Gap(4f);
                listing.Label("Shuttle name:");
                string nameLabel = shuttleName.NullOrEmpty() ? "(select shuttle)" : shuttleName;
                if (Widgets.ButtonText(listing.GetRect(28f), nameLabel))
                {
                    var opts = new List<FloatMenuOption>();
                    var ships = Find.TransportShipManager?.AllTransportShips;
                    if (ships != null)
                    {
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
                        opts.Add(new FloatMenuOption("(no shuttles found)", null) { Disabled = true });
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            }
        }

        // ── Persistence ──────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref targetFilter,  "targetFilter",  Action_ShuttleControl.ShuttleTarget.AnyWaiting);
            Scribe_Values.Look(ref shuttleName,   "shuttleName",   "");
            Scribe_Values.Look(ref thingDefName,  "thingDefName",  "");
            Scribe_Values.Look(ref loadCount,     "loadCount",     1);
        }
    }
}
