using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Triggers
{
    /// <summary>
    /// Runtime-reflective pawn property trigger.
    ///
    /// Presents ALL public scalar (float/int/bool) properties on standard pawn
    /// sub-trackers AND on any ThingComp from loaded mod assemblies.
    /// Zero code changes needed when new mods add properties — they appear
    /// automatically in the dropdown the next time the dialog is opened.
    ///
    /// Use <see cref="Trigger_PawnState"/> for Def-parameterized properties
    /// (hediffs, needs, skills, abilities, etc.) since those require choosing
    /// a specific Def and cannot be discovered via simple scalar reflection.
    /// </summary>
    public class Trigger_PawnProperty : AutomationTrigger
    {
        // ── Serialised fields ─────────────────────────────────────────────────

        public string          propKey    = "";                              // PawnPropEntry.Key
        public bool            boolValue  = true;                           // for bool properties
        public float           threshold  = 0.5f;                           // for numeric properties
        public CountComparator comparator = CountComparator.AtLeast;
        public PawnKindFilter  pawnKind   = PawnKindFilter.Colonist;
        public string          raceDefName = "";
        public string          zoneLabel   = "";

        // ── Runtime-only ──────────────────────────────────────────────────────
        // String buffer for the threshold text field (not serialised — re-initialised from threshold on load).
        [Unsaved] private string _thresholdBuffer;

        // ── Helpers ───────────────────────────────────────────────────────────

        private PawnPropEntry GetEntry() => PawnPropertyScanner.Find(propKey);

        private string CompSym =>
            comparator == CountComparator.AtLeast ? "\u2265"
          : comparator == CountComparator.AtMost  ? "\u2264" : "=";

        private bool CompareNum(float value) =>
            comparator == CountComparator.AtLeast ? value >= threshold
          : comparator == CountComparator.AtMost  ? value <= threshold
          : Math.Abs(value - threshold) < 0.001f;

        // ── AutomationTrigger overrides ───────────────────────────────────────

        public override string Label => "Pawn property (dynamic)";

        public override string Description
        {
            get
            {
                var e = GetEntry();
                string kind = pawnKind.ToString().ToLower();
                string zone = zoneLabel.NullOrEmpty() ? "" : $" in '{zoneLabel}'";
                string prop = e?.DisplayName ?? (propKey.NullOrEmpty() ? "???" : propKey);

                if (e != null && e.IsBoolean)
                    return $"Any {kind}{zone}: {prop} is {(boolValue ? "true" : "false")}";

                return $"Any {kind}{zone}: {prop} {CompSym} {threshold:G4}";
            }
        }

        public override bool HasConfig => true;

        public override float ConfigHeight
        {
            get
            {
                var e = GetEntry();
                float h = 56f;              // "Property:" label + dropdown button
                h += (e != null && e.IsBoolean) ? 30f : 58f;   // bool toggle OR comparator+textfield
                h += 36f;                   // pawn kind row
                if (pawnKind == PawnKindFilter.Animal || pawnKind == PawnKindFilter.Any)
                    h += 48f;              // race filter
                h += 30f;                   // zone
                return h;
            }
        }

        // ── Trigger logic ─────────────────────────────────────────────────────

        public override bool IsTriggered(Map map)
        {
            var e = GetEntry();
            if (e == null) return false;

            var pawns = PawnFilterHelper.GetPawns(map, pawnKind, zoneLabel, raceDefName)
                .Where(p => !p.Dead && p.Spawned);

            if (e.IsBoolean)
                return pawns.Any(p => e.GetBool(p) == boolValue);

            return pawns.Any(p =>
            {
                float? val = e.GetFloat(p);
                return val.HasValue && CompareNum(val.Value);
            });
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public override void DrawConfig(Listing_Standard listing)
        {
            var e = GetEntry();

            // ── Property picker ──────────────────────────────────────────────
            listing.Label("Pawn property (discovered at runtime):");
            string btnLabel = e != null
                ? $"{e.DisplayName}  [{(e.IsBoolean ? "bool" : "float")}]"
                : (propKey.NullOrEmpty() ? "(select a property)" : $"(missing: {propKey})");

            if (Widgets.ButtonText(listing.GetRect(28f), btnLabel))
                Find.WindowStack.Add(new FloatMenu(BuildPropertyMenu()));

            // ── Value comparison UI ──────────────────────────────────────────
            if (e != null && e.IsBoolean)
            {
                // Bool: simple true/false toggle
                Rect row = listing.GetRect(24f);
                float hw = row.width / 2f;
                PawnFilterHelper.DrawToggleBtn(new Rect(row.x,      row.y, hw, 24f), "Is true",  boolValue,  () => boolValue = true);
                PawnFilterHelper.DrawToggleBtn(new Rect(row.x + hw, row.y, hw, 24f), "Is false", !boolValue, () => boolValue = false);
            }
            else
            {
                // Numeric: comparator buttons + text-entry threshold
                Rect compRow = listing.GetRect(24f);
                float w = compRow.width / 3f;
                PawnFilterHelper.DrawToggleBtn(new Rect(compRow.x,         compRow.y, w, 24f), "\u2265 At least", comparator == CountComparator.AtLeast, () => comparator = CountComparator.AtLeast);
                PawnFilterHelper.DrawToggleBtn(new Rect(compRow.x + w,     compRow.y, w, 24f), "\u2264 At most",  comparator == CountComparator.AtMost,  () => comparator = CountComparator.AtMost);
                PawnFilterHelper.DrawToggleBtn(new Rect(compRow.x + w * 2, compRow.y, w, 24f), "= Exactly",       comparator == CountComparator.Exactly, () => comparator = CountComparator.Exactly);

                // Text field for threshold — flexible, no slider needed since range varies per property
                if (_thresholdBuffer == null) _thresholdBuffer = threshold.ToString("G6");
                Rect fieldRow = listing.GetRect(28f);
                Rect labelRect = new Rect(fieldRow.x,          fieldRow.y, fieldRow.width * 0.35f, fieldRow.height);
                Rect fieldRect = new Rect(fieldRow.x + fieldRow.width * 0.35f, fieldRow.y, fieldRow.width * 0.65f, fieldRow.height);
                Widgets.Label(labelRect, $"Threshold ({threshold:G4}):");
                string newBuf = Widgets.TextField(fieldRect, _thresholdBuffer);
                if (newBuf != _thresholdBuffer)
                {
                    _thresholdBuffer = newBuf;
                    if (float.TryParse(newBuf, out float parsed))
                        threshold = parsed;
                }
            }

            // ── Pawn filter (always shown) ───────────────────────────────────
            PawnFilterHelper.DrawKindFilter(pawnKind, v => pawnKind = v, listing);
            if (pawnKind == PawnKindFilter.Animal || pawnKind == PawnKindFilter.Any)
            {
                listing.Label("Race filter (optional):");
                PawnFilterHelper.DrawRaceDropdown(raceDefName, v => raceDefName = v, listing.GetRect(24f));
            }
            listing.Label("Zone (optional):");
            PawnFilterHelper.DrawZoneDropdown(zoneLabel, v => zoneLabel = v, listing.GetRect(24f));
        }

        // ── Dropdown builder ──────────────────────────────────────────────────

        private List<FloatMenuOption> BuildPropertyMenu()
        {
            // Already sorted by GroupName then DisplayName by the scanner
            var opts = new List<FloatMenuOption>();
            string lastGroup = null;

            foreach (PawnPropEntry entry in PawnPropertyScanner.All)
            {
                // Insert non-clickable separator label when group changes
                if (entry.GroupName != lastGroup)
                {
                    lastGroup = entry.GroupName;
                    opts.Add(new FloatMenuOption($"── {entry.GroupName} ──", null)
                    {
                        Disabled = true,
                    });
                }

                string capturedKey = entry.Key;
                string suffix      = entry.IsBoolean ? " [bool]" : " [float]";
                // Strip group prefix from display name for cleaner sub-entries
                string localName   = entry.DisplayName.StartsWith(entry.GroupName + ": ")
                    ? "  " + entry.DisplayName.Substring(entry.GroupName.Length + 2)
                    : "  " + entry.DisplayName;

                opts.Add(new FloatMenuOption(localName + suffix, () =>
                {
                    propKey          = capturedKey;
                    threshold        = 0.5f;
                    _thresholdBuffer = null; // reset text buffer
                }));
            }

            if (opts.Count == 0)
                opts.Add(new FloatMenuOption("(no properties discovered yet — open a game first)", null) { Disabled = true });

            return opts;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref propKey,     "propKey",     "");
            Scribe_Values.Look(ref boolValue,   "boolValue",   true);
            Scribe_Values.Look(ref threshold,   "threshold",   0.5f);
            Scribe_Values.Look(ref comparator,  "comparator",  CountComparator.AtLeast);
            Scribe_Values.Look(ref pawnKind,    "pawnKind",    PawnKindFilter.Colonist);
            Scribe_Values.Look(ref raceDefName, "raceDefName", "");
            Scribe_Values.Look(ref zoneLabel,   "zoneLabel",   "");
        }
    }
}
