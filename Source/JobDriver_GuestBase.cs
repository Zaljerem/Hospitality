using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using GuestUtility = Hospitality.Utilities.GuestUtility;

namespace Hospitality;

public abstract class JobDriver_GuestBase : JobDriver_ChatWithPrisoner
{
    protected abstract InteractionDef InteractionDef { get; }

    public static Toil GotoGuest(Pawn pawn, Pawn talkee, bool mayBeSleeping = false)
    {
        var toil = Toils_Interpersonal.GotoInteractablePosition(TargetIndex.A);
        toil.AddFailCondition(() => !GuestUtility.ViableGuestTarget(talkee, mayBeSleeping));
        return toil;
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOnDowned(TargetIndex.A);
        this.FailOnNotCasualInterruptible(TargetIndex.A);
        FailOnJobNotSuspendable(TargetIndex.A);

        var gotoGuest = GotoGuest(pawn, Talkee); // Jump target
        yield return gotoGuest;
        //yield return Toils_Reserve.ReserveQueue(TargetIndex.A);

        //yield return Toils_Interpersonal.WaitToBeAbleToInteract(pawn);
        yield return Toils_General.Wait(10).JumpIf(() => !CanInteract(pawn, Talkee), gotoGuest);

        yield return Interact(Talkee, InteractionDef, GuestUtility.InteractIntervalAbsoluteMin);

        foreach (var toil in Perform())
        {
            yield return toil;
        }

        yield return Toils_Interpersonal.SetLastInteractTime(TargetIndex.A);
    }


    private void FailOnJobNotSuspendable(TargetIndex index)
    {
        AddEndCondition(delegate
        {
            var actor = GetActor();
            if (actor != null)
            {
                var target = (Pawn)actor.jobs.curJob.GetTarget(index).Thing;
                if (!JobIsSuspendable(target))
                {
                    return JobCondition.Incompletable;
                }
            }

            return JobCondition.Ongoing;
        });
    }

    internal static bool JobIsSuspendable(Pawn pawn)
    {
        var curJob = pawn?.CurJob;
        if (curJob?.def == JobDefOf.Wait) return true; // Wait is not suspendable, but acceptable
        return curJob?.def.suspendable != false;
    }

    protected abstract IEnumerable<Toil> Perform();

    protected Toil Interact(Pawn talkee, InteractionDef intDef, int duration)
    {
        var toil = new Toil
        {
            initAction = () =>
            {
                if (talkee.interactions.InteractedTooRecentlyToInteract()
                    || pawn.interactions.InteractedTooRecentlyToInteract()) return;

                PawnUtility.ForceWait(talkee, duration, pawn);
                TargetThingB = pawn;
                MoteMaker.MakeInteractionBubble(pawn, talkee, intDef.interactionMote, intDef.GetSymbol(pawn.Faction, pawn.Ideo), intDef.GetSymbolColor(pawn.Faction));
            },
            socialMode = RandomSocialMode.Off,
            defaultCompleteMode = ToilCompleteMode.Delay,
            defaultDuration = duration
        };
        return toil.WithProgressBarToilDelay(TargetIndex.B);
    }

    protected virtual bool FailCondition()
    {
        return !GuestUtility.ViableGuestTarget(Talkee) || (!pawn.HasReserved(Talkee) && !pawn.CanReserve(Talkee));
    }

    protected static bool CanInteract(Pawn pawn, Pawn talkee)
    {
        // Log.Message($"{pawn.NameShortColored} can interact with {talkee.NameShortColored}? IsBusy = {IsBusy(talkee)} FirstRespectedReserver = {talkee.Map.reservationManager.FirstRespectedReserver(talkee, pawn)?.NameShortColored}");
        if (IsBusy(talkee)) return false;
        if (talkee.Map.reservationManager.FirstRespectedReserver(talkee, pawn) == pawn) return true;
        return false;
    }

    protected static bool IsBusy(Pawn p)
    {
        // Non-suspendable job? We're busy!
        if (p.CurJob?.def.suspendable == false) return true;
        if (p.CurJob?.def.casualInterruptible == false) return true;

        return p.interactions.InteractedTooRecentlyToInteract() || GuestUtility.IsInTherapy(p);
    }
}