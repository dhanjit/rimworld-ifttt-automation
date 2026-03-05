using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldIFTTT.Actions
{
    /// <summary>
    /// Drafts colonists who have shooting passion or a shooting skill above a
    /// configurable minimum threshold.  A message alerts the player to position them.
    /// </summary>
    public class Action_DraftShooters : AutomationAction
    {
        public override string Label       => "Draft shooters";
        public override string Description => "Drafts colonists with strong shooting skills so the " +
                                              "player can quickly position them.";

        public override bool HasConfig => true;

        // ── Config ────────────────────────────────────────────────────────────
        /// <summary>Minimum shooting skill level to be considered a "shooter".</summary>
        public int minShootingLevel = 6;

        // ── Logic ─────────────────────────────────────────────────────────────
        public override bool Execute(Map map)
        {
            SkillDef shootingDef = DefDatabase<SkillDef>.GetNamedSilentFail("Shooting");
            if (shootingDef == null)
            {
                Log.Warning("[IFTTT] DraftShooters: Shooting SkillDef not found.");
                return false;
            }

            List<Pawn> drafted = new List<Pawn>();

            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.Downed || pawn.Dead || pawn.drafter == null) continue;
                if (pawn.drafter.Drafted) continue; // already drafted

                SkillRecord skill = pawn.skills?.GetSkill(shootingDef);
                if (skill == null) continue;

                bool qualifies = skill.Level >= minShootingLevel || skill.passion != Passion.None;
                if (!qualifies) continue;

                pawn.drafter.Drafted = true;
                drafted.Add(pawn);
            }

            if (drafted.Count == 0)
            {
                Messages.Message(
                    "[IFTTT] No un-drafted shooters found to draft.",
                    MessageTypeDefOf.NeutralEvent,
                    historical: false);
                return false;
            }

            string names = string.Join(", ", drafted.Select(p => p.LabelShort));
            Messages.Message(
                $"[IFTTT] Drafted shooters: {names}. Position them now!",
                drafted[0],
                MessageTypeDefOf.CautionInput,
                historical: false);

            return true;
        }

        // ── Config UI ─────────────────────────────────────────────────────────
        public override void DrawConfig(Listing_Standard listing)
        {
            string buf = minShootingLevel.ToString();
            listing.TextFieldNumericLabeled("Min shooting level: ", ref minShootingLevel, ref buf, 0, 20);
        }

        // ── Save/load ─────────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minShootingLevel, "minShootingLevel", 6);
        }
    }
}
