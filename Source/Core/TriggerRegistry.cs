using System;
using System.Collections.Generic;
using RimWorldIFTTT.Triggers;

namespace RimWorldIFTTT
{
    /// <summary>
    /// Central registry of all available trigger types.
    /// Organized by category for readability; the UI uses the flat AllTypes list.
    /// </summary>
    public static class TriggerRegistry
    {
        public static readonly List<Type> AllTypes = new List<Type>
        {
            // ── Original triggers ─────────────────────────────────────────────
            typeof(Trigger_HasSkillTrainer),
            typeof(Trigger_UnderAttack),
            typeof(Trigger_CanContactFaction),
            typeof(Trigger_SurplusSilver),
            typeof(Trigger_VisitorPresent),

            // ── Combat ────────────────────────────────────────────────────────
            typeof(Trigger_ColonistDowned),
            typeof(Trigger_FireOnMap),
            typeof(Trigger_EnemyCount),
            typeof(Trigger_MechanoidPresent),
            typeof(Trigger_RaidIncoming),

            // ── Colony management ─────────────────────────────────────────────
            typeof(Trigger_FoodLow),
            typeof(Trigger_ItemCount),
            typeof(Trigger_PawnMoodLow),
            typeof(Trigger_ColonistIdle),
            typeof(Trigger_PowerFailure),
            typeof(Trigger_SeasonIs),
            typeof(Trigger_TimeOfDay),
            typeof(Trigger_WeatherIs),
            typeof(Trigger_AnimalTamable),
            typeof(Trigger_AnimalOverpopulated),
            typeof(Trigger_AnimalGearWornOut),
            typeof(Trigger_ColonistCount),
            typeof(Trigger_ResearchQueueEmpty),
            typeof(Trigger_BedCapacity),
            typeof(Trigger_MentalBreak),
            typeof(Trigger_SkillTrainerAvailable),

            // ── Economy ───────────────────────────────────────────────────────
            typeof(Trigger_WealthAbove),
            typeof(Trigger_StockpileItem),
            typeof(Trigger_TraderArrived),
            typeof(Trigger_DrillResourceAbove),

            // ── Social ────────────────────────────────────────────────────────
            typeof(Trigger_FactionGoodwillBelow),
            typeof(Trigger_PrisonerPresent),
            typeof(Trigger_OrbitalTraderPresent),
            typeof(Trigger_NeutralFactionNearby),

            // ── Medical ───────────────────────────────────────────────────────
            typeof(Trigger_PawnHasInjury),
            typeof(Trigger_PawnHasDisease),
            typeof(Trigger_PawnBloodLoss),
            typeof(Trigger_PawnToxicityHigh),

            // ── Animal enhancement ────────────────────────────────────────────
            typeof(Trigger_AnimalMissingHediff),
        };

        public static AutomationTrigger CreateInstance(Type type)
            => (AutomationTrigger)Activator.CreateInstance(type);

        public static string GetLabel(Type type)
        {
            try { return CreateInstance(type).Label; }
            catch { return type.Name; }
        }
    }
}
