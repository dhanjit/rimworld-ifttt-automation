using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT
{
    public enum PawnKindFilter { Colonist, Animal, Prisoner, Any }

    /// <summary>
    /// Shared helpers for filtering pawns by kind, race, and area zone.
    /// UI helpers use Action&lt;T&gt; setters instead of ref parameters so they
    /// can be safely captured in lambda callbacks.
    /// </summary>
    public static class PawnFilterHelper
    {
        // ── Pawn enumeration ──────────────────────────────────────────────────

        public static IEnumerable<Pawn> GetPawns(
            Map map,
            PawnKindFilter kind,
            string zoneLabel   = null,
            string raceDefName = null)
        {
            IEnumerable<Pawn> pawns;
            switch (kind)
            {
                case PawnKindFilter.Colonist:
                    pawns = map.mapPawns.FreeColonistsSpawned;
                    break;
                case PawnKindFilter.Animal:
                    pawns = map.mapPawns.AllPawnsSpawned
                        .Where(p => p.RaceProps.Animal && p.Faction == Faction.OfPlayer);
                    break;
                case PawnKindFilter.Prisoner:
                    pawns = map.mapPawns.PrisonersOfColonySpawned;
                    break;
                default:
                    pawns = map.mapPawns.AllPawnsSpawned.Where(p => p.Spawned);
                    break;
            }

            if (!zoneLabel.NullOrEmpty())
            {
                Area zone = map.areaManager.AllAreas
                    .FirstOrDefault(a => a.Label == zoneLabel);
                if (zone != null)
                    pawns = pawns.Where(p => zone[p.Position]);
            }

            if (!raceDefName.NullOrEmpty())
                pawns = pawns.Where(p => p.def.defName == raceDefName);

            return pawns;
        }

        // ── UI helpers ────────────────────────────────────────────────────────
        // Note: all setters are Action<T> (not ref params) so they can safely
        // be captured inside FloatMenuOption lambdas.

        /// <summary>Draws a zone/area dropdown. Calls setter with the chosen label.</summary>
        public static void DrawZoneDropdown(string current, Action<string> set, Rect rect, Map map = null)
        {
            map = map ?? Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            string btn = current.NullOrEmpty() ? "(any zone)" : current;
            if (Widgets.ButtonText(rect, btn))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("(any zone)", () => set(""))
                };
                if (map != null)
                {
                    foreach (Area area in map.areaManager.AllAreas)
                    {
                        string label = area.Label;
                        options.Add(new FloatMenuOption(label, () => set(label)));
                    }
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        /// <summary>Draws a 4-button pawn kind selector. Calls setter with chosen value.</summary>
        public static void DrawKindFilter(PawnKindFilter current, Action<PawnKindFilter> set, Listing_Standard listing)
        {
            listing.Label("Target pawn type:");
            Rect row = listing.GetRect(24f);
            float w   = row.width / 4f;

            DrawToggleBtn(new Rect(row.x,         row.y, w, 24f), "Colonist", current == PawnKindFilter.Colonist, () => set(PawnKindFilter.Colonist));
            DrawToggleBtn(new Rect(row.x + w,     row.y, w, 24f), "Animal",   current == PawnKindFilter.Animal,   () => set(PawnKindFilter.Animal));
            DrawToggleBtn(new Rect(row.x + w * 2, row.y, w, 24f), "Prisoner", current == PawnKindFilter.Prisoner, () => set(PawnKindFilter.Prisoner));
            DrawToggleBtn(new Rect(row.x + w * 3, row.y, w, 24f), "Any",      current == PawnKindFilter.Any,      () => set(PawnKindFilter.Any));
        }

        /// <summary>Draws an animal race dropdown. Calls setter with chosen defName.</summary>
        public static void DrawRaceDropdown(string current, Action<string> set, Rect rect)
        {
            ThingDef cur = DefDatabase<ThingDef>.GetNamedSilentFail(current);
            string btn   = cur != null ? cur.label.CapitalizeFirst()
                : (current.NullOrEmpty() ? "(any race)" : $"(unknown: {current})");

            if (Widgets.ButtonText(rect, btn))
            {
                var opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption("(any race)", () => set(""))
                };
                foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.race?.Animal == true)
                    .OrderBy(d => d.label))
                {
                    string defName = d.defName;
                    opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(), () => set(defName)));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        /// <summary>Draws a hediff dropdown. Calls setter with chosen defName.</summary>
        public static void DrawHediffDropdown(string current, Action<string> set, Rect rect, string noFilterLabel = "(no filter)")
        {
            HediffDef cur = DefDatabase<HediffDef>.GetNamedSilentFail(current);
            string btn    = cur != null ? cur.label.CapitalizeFirst()
                : (current.NullOrEmpty() ? noFilterLabel : $"(unknown: {current})");

            if (Widgets.ButtonText(rect, btn))
            {
                var opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption(noFilterLabel, () => set(""))
                };
                foreach (HediffDef d in DefDatabase<HediffDef>.AllDefsListForReading
                    .Where(d => !d.label.NullOrEmpty())
                    .OrderBy(d => d.label))
                {
                    string defName = d.defName;
                    opts.Add(new FloatMenuOption(d.label.CapitalizeFirst(), () => set(defName)));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        /// <summary>Active/inactive toggle button — green tint when active.</summary>
        public static void DrawToggleBtn(Rect r, string label, bool active, Action onClick)
        {
            Color old = GUI.color;
            if (active) GUI.color = new Color(0.35f, 0.75f, 0.35f);
            if (Widgets.ButtonText(r, label)) onClick();
            GUI.color = old;
        }
    }
}
