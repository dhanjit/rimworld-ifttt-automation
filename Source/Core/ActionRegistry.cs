using System;
using System.Collections.Generic;
using RimWorldIFTTT.Actions;

namespace RimWorldIFTTT
{
    /// <summary>
    /// Central registry of all available action types.
    /// Organized by category; the UI uses the flat AllTypes list.
    /// </summary>
    public static class ActionRegistry
    {
        public static readonly List<Type> AllTypes = new List<Type>
        {
            // ── Original actions ──────────────────────────────────────────────
            typeof(Action_UseSkillTrainer),
            typeof(Action_DraftShooters),
            typeof(Action_CallAlliedTrader),
            typeof(Action_GiftSilver),
            typeof(Action_NotifyPlayer),

            // ── Combat ────────────────────────────────────────────────────────
            typeof(Action_DraftAllShooters),
            typeof(Action_UndraftAll),

            // ── Colony management ─────────────────────────────────────────────
            typeof(Action_SetAllowedArea),
            typeof(Action_SetWorkPriority),
            typeof(Action_TameAnimal),
            typeof(Action_SlaughterAnimal),
            typeof(Action_SetResearchProject),
            typeof(Action_SetPawnSchedule),
            typeof(Action_ToggleWorkType),
            typeof(Action_HaulToStockpile),
            typeof(Action_ForbidAllItems),
            typeof(Action_UseSkillTrainerOnBest),
            typeof(Action_ReplaceAnimalGear),
            typeof(Action_ApplySentienceCatalyst),

            // ── Social ────────────────────────────────────────────────────────
            typeof(Action_CheerUpPawn),
            typeof(Action_RecruitPrisoner),
            typeof(Action_ReleasePrisoner),
            typeof(Action_ContactOrbitalTrader),

            // ── Medical ───────────────────────────────────────────────────────
            typeof(Action_TendInjured),
            typeof(Action_RescuePawn),

            // ── Economy / Resource control ────────────────────────────────────
            typeof(Action_SetDeepDrillForbidden),

            // ── Notifications / Control ────────────────────────────────────────
            typeof(Action_PauseGame),
            typeof(Action_SendAlert),
            typeof(Action_DropSupplyDrop),

            // ── Generic (mod-agnostic, configurable) ──────────────────────────
            typeof(Action_UseItemOnPawn),
            typeof(Action_CastAbility),
            typeof(Action_SetForbidden),
            typeof(Action_SetVariable),    // state machine: write a named numeric variable
            typeof(Action_Designate),       // generic: place any designation (mine, hunt, cut, etc.)
            typeof(Action_ShuttleControl), // shuttle: launch, hold, unload transport ships (DLC)
            typeof(Action_LoadShuttle),    // shuttle: queue items for loading into shuttle (DLC)
        };

        public static AutomationAction CreateInstance(Type type)
            => (AutomationAction)Activator.CreateInstance(type);

        public static string GetLabel(Type type)
        {
            try { return CreateInstance(type).Label; }
            catch { return type.Name; }
        }
    }
}
