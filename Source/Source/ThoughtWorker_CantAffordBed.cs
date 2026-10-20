using System.Linq;
using Hospitality.Utilities;
using RimWorld;
using Verse;

namespace Hospitality 
{
    /// <summary>
    /// Loaded via xml. Added so guests are upset when they can't afford a bed.
    /// </summary>
    public class ThoughtWorker_CantAffordBed : ThoughtWorkerCached
    {
        protected override bool ShouldCache(Pawn pawn)
        {
            if (pawn == null) return false;
            if (pawn.thingIDNumber == 0) return false; // What do you know!!!

            if (Current.ProgramState != ProgramState.Playing) return false;
            if (!pawn.IsArrivedGuest(out var compGuest)) return false;

            if(compGuest.rescued) return false;
            if(compGuest.HasBed) return false;
            return true;
        }

        protected override ThoughtState GetStateToCache(Pawn pawn)
        {
            var silver = pawn.inventory.innerContainer.FirstOrDefault(i => i.def == ThingDefOf.Silver);
            var money = silver?.stackCount ?? 0;

            // This thought has no multi-tick cache interval. Avoid allocating a bed array and
            // rescanning it each time; keep the distinct occupied/owned slot rules intact.
            var hasUnoccupiedSlot = false;
            foreach (var bed in pawn.MapHeld.GetGuestBeds(pawn.GetGuestArea()))
            {
                if (bed.RentalFee <= money && bed.AnyUnownedSleepingSlot) return ThoughtState.Inactive;
                if (!hasUnoccupiedSlot && bed.AnyUnoccupiedSleepingSlot) hasUnoccupiedSlot = true;
            }

            return hasUnoccupiedSlot ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
        }
    }
}
