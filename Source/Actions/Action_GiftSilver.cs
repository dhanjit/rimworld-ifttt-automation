using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Transfers a configurable amount of silver from the colony stockpile to the
    /// best allied faction, increasing goodwill proportionally.
    /// NOTE: This uses goodwill adjustment directly (not a real caravan gift).
    ///       The silver is destroyed from the map.
    /// </summary>
    public class Action_GiftSilver : AutomationAction
    {
        public override string Label       => "Gift silver to ally";
        public override string Description => "Destroys silver from the stockpile and adjusts " +
                                              "goodwill with the best allied faction.";

        public override bool HasConfig => true;

        // ── Config ────────────────────────────────────────────────────────────
        public int giftAmount = 200;

        // ── Logic ─────────────────────────────────────────────────────────────
        public override bool Execute(Map map)
        {
            // 1. Find the ally with lowest goodwill (needs it most, but still ally).
            Faction target = Find.FactionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer && f.PlayerRelationKind == FactionRelationKind.Ally)
                .OrderBy(f => f.PlayerGoodwill)
                .FirstOrDefault();

            if (target == null)
            {
                Log.Message("[IFTTT] GiftSilver: No allied faction found.");
                return false;
            }

            // 2. Gather silver from the map (up to giftAmount).
            List<Thing> silverThings = map.listerThings
                .ThingsOfDef(ThingDefOf.Silver)
                .Where(t => !t.IsForbidden(Faction.OfPlayer))
                .ToList();

            int remaining = giftAmount;
            List<Thing> toDestroy = new List<Thing>();

            foreach (Thing stack in silverThings)
            {
                if (remaining <= 0) break;
                int take = System.Math.Min(stack.stackCount, remaining);
                remaining -= take;

                if (take >= stack.stackCount)
                    toDestroy.Add(stack);
                else
                    stack.stackCount -= take;
            }

            int actualGifted = giftAmount - remaining;
            if (actualGifted <= 0)
            {
                Messages.Message(
                    "[IFTTT] GiftSilver: Not enough silver in stockpile.",
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return false;
            }

            // 3. Destroy the gathered silver.
            foreach (Thing t in toDestroy)
                t.Destroy();

            // 4. Adjust goodwill: vanilla gives ~1 goodwill per 10 silver (rough estimate).
            int goodwillGain = Mathf.RoundToInt(actualGifted / 10f);
            target.TryAffectGoodwillWith(Faction.OfPlayer, goodwillGain, canSendMessage: false,
                reason: HistoryEventDefOf.GaveGift);

            Messages.Message(
                $"[IFTTT] Gifted {actualGifted} silver to {target.Name}. " +
                $"Goodwill +{goodwillGain}.",
                MessageTypeDefOf.PositiveEvent,
                historical: false);

            return true;
        }

        // ── Config UI ─────────────────────────────────────────────────────────
        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = giftAmount.ToString();
            listing.TextFieldNumericLabeled("Silver to gift: ", ref giftAmount, ref buf, 1, 9999);
        }

        // ── Save/load ─────────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref giftAmount, "giftAmount", 200);
        }
    }
}
