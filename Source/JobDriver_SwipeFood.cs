using System.Collections.Generic;
using Hospitality.Utilities;
using RimWorld;
using Verse.AI;

namespace Hospitality;

public class JobDriver_SwipeFood : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        // Swiping shares the same food-taking path: exclude a second guest on the scrounge
        // layer while leaving the default layer available for the food owner's own meal.
        return job.GetTarget(TargetIndex.B).IsValid
            && pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, job.count, InternalDefOf.Scrounge, errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        this.FailOnAggroMentalState(TargetIndex.A);
        // A job-wide immediate-touch check failed before this pawn could walk to a sleeper.
        // Pathing handles the approach; require immediate touch only at the actual take.
        this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
        // mrbeverage, PR #13: Touch also permits standing beside sleepers on low-cost cells
        // (floor, sleeping spots and bedrolls), instead of requiring their occupied cell.
        // Stop if they wake: taking food from an awake pawn must use the asking/scrounge path.
        var approach = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOn(TargetAwake);
        approach.socialMode = RandomSocialMode.Off; // Configure the yielded toil, not a discarded copy.
        yield return approach;
        var take = ItemUtility.TakeFromPawn(job.targetB.Thing, job.targetA.Pawn.inventory.innerContainer, job.count, TargetIndex.B);
        take.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
        yield return take.FailOn(TargetAwake); // Recheck wake state before the transfer init action.
        // PR #12: the take already puts food in the hands; no ground pickup is necessary.
        yield return Toils_General.Wait(150);
        yield return ItemUtility.TakeToInventory(TargetIndex.B);
    }

    private bool TargetAwake() => job.GetTarget(TargetIndex.A).Pawn?.Awake() == true;
}
