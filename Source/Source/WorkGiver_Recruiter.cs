using Hospitality.Utilities;
using RimWorld;
using Verse;
using Verse.AI;

namespace Hospitality
{
    public class WorkGiver_Recruiter : WorkGiver_Scanner
    {
        private readonly JobDef jobDefMakeFriends = DefDatabase<JobDef>.GetNamed("CharmGuest");

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var guest = t as Pawn;
            return pawn.ShouldMakeFriends(guest);
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn?.WorkTagIsDisabled(def.workType.workTags) ?? true) return true;
            // ViableGuestTarget requires membership in this same map's PresentGuests set.
            // Avoid scanning all pawns when none can qualify, without caching a result that
            // could hide newly arrived guests. Missing components retain the old behavior.
            return pawn.GetMapComponent()?.PresentGuests.Count == 0;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return new Job(jobDefMakeFriends, t);
        }

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.OnCell;
    }
}
