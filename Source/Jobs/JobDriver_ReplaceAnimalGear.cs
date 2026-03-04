using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldIFTTT.Jobs
{
    /// <summary>
    /// Drives a colonist to:
    ///   1. Walk to a replacement piece of animal apparel in storage
    ///   2. Pick it up
    ///   3. Walk to the target animal
    ///   4. Equip it via Pawn_ApparelTracker.Wear() (AAF API)
    ///
    /// targetA = replacement Apparel item (in storage, unforbidden)
    /// targetB = target animal Pawn
    /// </summary>
    public class JobDriver_ReplaceAnimalGear : JobDriver
    {
        private Apparel Replacement => job.targetA.Thing as Apparel;
        private Pawn    Animal      => job.targetB.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Reserve the replacement item so two handlers don't race for the same piece
            return pawn.Reserve(job.targetA, job, 1, 1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A); // replacement gone
            this.FailOnDestroyedOrNull(TargetIndex.B); // animal died
            this.FailOn(() => Animal?.apparel == null); // AAF not active

            // ── 1. Walk to the replacement apparel ───────────────────────────
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.A);

            // ── 2. Pick it up ─────────────────────────────────────────────────
            yield return Toils_Haul.StartCarryThing(TargetIndex.A,
                putRemainderInQueue: false,
                subtractNumTakenFromJobCount: false);

            // ── 3. Walk to the animal ─────────────────────────────────────────
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch)
                .FailOnDestroyedOrNull(TargetIndex.B);

            // ── 4. Equip the replacement on the animal ────────────────────────
            Toil equip = ToilMaker.MakeToil("EquipAnimalGear");
            equip.initAction = () =>
            {
                Pawn    animal      = Animal;
                Apparel replacement = Replacement;

                if (animal?.apparel == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (replacement == null || replacement.Destroyed)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // If handler is still carrying it, Wear() transfers it from their inventory
                animal.apparel.Wear(replacement, dropReplacedApparel: true);

                Messages.Message(
                    $"[IFTTT] {pawn.LabelShort} replaced {replacement.def.label} on {animal.LabelShort}.",
                    animal, MessageTypeDefOf.NeutralEvent, historical: false);
            };
            equip.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return equip;
        }
    }
}
