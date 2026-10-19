using System.Collections.Generic;
using Hospitality.Utilities;
using RimWorld;
using Verse.AI;

namespace Hospitality;

public class JobDriver_SwipeFood : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        // Claim the food on the Scrounge layer, as ScroungeFood does, so Qualifies keeps a second
        // guest off it without blocking the target's own meal.
        var food = job.GetTarget(TargetIndex.B);
        return !food.IsValid || pawn.Reserve(food, job, 1, job.count, InternalDefOf.Scrounge, errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        this.FailOnAggroMentalState(TargetIndex.A);
        this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
        // A swipe is for a sleeping target. If it wakes on the way, stop; the guest's next look can
        // scrounge from it instead.
        var gotoTarget = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOn(TargetAwake);
        gotoTarget.socialMode = RandomSocialMode.Off;
        yield return gotoTarget;
        // Driver-wide end conditions run before the first toil, before the goto has moved the guest,
        // so the touch check belongs on the take.
        yield return ItemUtility.TakeFromPawn(job.targetB.Thing, job.targetA.Pawn.inventory.innerContainer, job.count, TargetIndex.B)
            .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
            .FailOn(TargetAwake);
        yield return Toils_General.Wait(150);
        yield return ItemUtility.TakeToInventory(TargetIndex.B);
    }

    private bool TargetAwake() => job.GetTarget(TargetIndex.A).Pawn?.Awake() == true;
}