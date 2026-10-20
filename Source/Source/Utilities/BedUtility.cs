using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Waylith.Math;
using Verse;
using Verse.AI;

namespace Hospitality.Utilities;

internal static class BedUtility
{
    public const float ScoreFactor = 0.5f; // factor for displayed value
    private static HashSet<Pawn> _tmpOwners = new();

    public static Building_GuestBed FindBedFor(this Pawn guest)
    {
        var silver = guest.inventory.innerContainer.FirstOrDefault(i => i.def == ThingDefOf.Silver);
        var money = silver?.stackCount ?? 0;

        Building_GuestBed bestBed = null;
        var bestValue = float.NegativeInfinity;
        // Search once: Any() followed by MaxBy() restarted availability checks, including
        // reachability/bed validity and owner opinions for the prefix through the first match.
        foreach (var bed in guest.MapHeld.GetGuestBeds(guest.GetGuestArea()))
        {
            if (!IsAvailableBed(bed, guest, money, out var sharingScore)) continue;
            var value = CalculateBedValue(bed, guest, money, sharingScore);
            // Enumerable.MaxBy kept the first bed on equal scores; retain that tie order.
            if (bestBed != null && value <= bestValue) continue;
            bestBed = bed;
            bestValue = value;
        }
        return bestBed;
    }

    [NotNull]
    public static IReadOnlyList<Pawn> Owners([CanBeNull] this Building_Bed bed)
    {
        var comp = bed?.CompAssignableToPawn;
        if (comp == null)
        {
            return Array.Empty<Pawn>();
        }
        if (comp.AssignedPawnsForReading == null)
        {
            return Array.Empty<Pawn>();
        }
        comp.AssignedPawnsForReading.RemoveAll(p => p == null);
        return bed.CompAssignableToPawn.AssignedPawnsForReading;
    }

    private static bool IsAvailableBed(Building_GuestBed bed, Pawn guest, int money, out int sharingScore)
    {
        sharingScore = 0;
        if (bed.RentalFee > money) return false;
        if (bed.IsForbidden(guest)) return false;
        if (bed.IsBurning()) return false;
        if (!RestUtility.CanUseBedEver(guest, bed.def)) return false;
        //Log.Message($"{guest.LabelShort} is checking {bed.Label} at {bed.Position}. SleepingSlots = {bed.SleepingSlotsCount}, Owners = {bed.Owners().Count}, Ideology forbids = {bed.CompAssignableToPawn.IdeoligionForbids(guest)}, AnyUnowned = {bed.AnyUnownedSleepingSlot}, CanReserve (with slots) = {guest.CanReserveAndReach(bed, PathEndMode.OnCell, Danger.Some, bed.SleepingSlotsCount)} CanReserve (without) = {guest.CanReserveAndReach(bed, PathEndMode.OnCell, Danger.Some)}");
        if (!bed.AnyUnownedSleepingSlot) return false;
        if (bed.CompAssignableToPawn.IdeoligionForbids(guest)) return false;
        // Align candidate selection with the bed usability checks used by the LayDown driver.
        // A free slot and a reachable bed can still be unusable due to current pawn/bed state.
        if (!RestUtility.IsValidBedFor(bed, guest, guest, checkSocialProperness: false)) return false;
        sharingScore = OtherOwnerScore(bed, guest);
        if (sharingScore < 0) return false;
        return true;
    }

    public static float CalculateBedValue(this Building_GuestBed bed, Pawn guest, int money)
    {
        return CalculateBedValue(bed, guest, money, OtherOwnerScore(bed, guest));
    }

    private static float CalculateBedValue(Building_GuestBed bed, Pawn guest, int money, int sharingScore)
    {
        StaticBedValue(bed, out var room, out var quality, out var impressiveness, out var roomType, out var comfort, out var facilities);

        var fee = Mathf.RoundToInt(money > 0 ? 250 * (1f * bed.RentalFee / money) : 0); // 0 - 250

        //Ideology
        var ideologyNeeds = Ideology_GetFulfillment(bed, guest, sharingScore); // -150 - 50

        // Royalty requires single bed?
        var royalExpectations = GetRoyalExpectations(bed, guest, room, out var title);

        // Shared
        // Availability already inspected these owners/reservations in this synchronous search.
        // Reuse that result locally; do not cache it across ownership or reservation changes.
        var otherPawnOpinion = sharingScore; // -340 - 340

        // Temperature
        var temperature = GetTemperatureScore(guest, room, bed); // -200 - 0

        // Traits
        if (guest.story.traits.HasTrait(TraitDefOf.Greedy))
        {
            fee *= 3;
            impressiveness -= 50;
        }

        if (guest.story.traits.HasTrait(TraitDefOf.Kind))
            fee /= 2;

        if (guest.story.traits.HasTrait(TraitDefOf.Ascetic))
        {
            impressiveness *= -1;
            roomType = 75; // We don't care, so always max
            comfort = 100; // We don't care, so always max
            facilities = 0; // We don't care, so they're ignored
        }

        if (guest.story.traits.HasTrait(TraitDefOf.Jealous))
        {
            fee /= 4;
            impressiveness -= 50;
            impressiveness *= 4;
        }

        // Tired
        var distance = 0;
        if (guest.IsTired())
        {
            distance = (int)bed.Position.DistanceTo(guest.Position);
            //Log.Message($"{guest.LabelShort} is tired. {bed.LabelCap} is {distance} units far away.");
        }

        var score = impressiveness + quality + comfort + roomType + temperature + otherPawnOpinion + royalExpectations + ideologyNeeds + facilities - distance;
        var value = Mathf.CeilToInt(ScoreFactor * score - fee);
        //Log.Message($"For {guest.LabelShort} {bed.Label} at {bed.Position} has a score of {score} and value of {value}:\n"
        //            + $"fee = {fee}, impressiveness = {impressiveness}, quality = {quality}, comfort = {comfort}, roomType = {roomType}, temperature = {temperature}, opinion = {otherPawnOpinion}, royalExpectations = {royalExpectations}, ideologyNeeds = {ideologyNeeds}, facilities = {facilities}, distance = {distance}");
        return value;
    }

    private static int GetFacilityScore(Building building)
    {
        return building.TryGetComp<CompAffectedByFacilities>()?.LinkedFacilitiesListForReading.Count * 10 ?? 0;
    }

    private static int Ideology_GetFulfillment(Building_Bed bed, Pawn guest, int sharingScore)
    {
        if (!ModsConfig.IdeologyActive) return 0;
        if (guest.ideo == null || bed == null) return 0;

        var score = 0;

        // If there's someone already using this bed, and we're willing to share, affect score accordingly
        // Preserve the existing additional ideology contribution, including its weight.
        score += sharingScore;

        var requiredFacilities = guest.Ideo.PreceptsListForReading.SelectMany(p => p.def.comps.Select(c => (c as PreceptComp_BedThought)?.requireFacility?.def)).ToList();
        if (requiredFacilities.Any())
        {
            var comp = bed.TryGetComp<CompAffectedByFacilities>();
            if (comp?.linkedFacilities != null)
                score += 10 * comp.linkedFacilities.Count(f => requiredFacilities.Contains(f.def));
        }

        var dominance = IdeoUtility.GetStyleDominance(bed, guest.Ideo);
        return score + Mathf.CeilToInt(dominance * 50);
    }

    private static int OtherOwnerScore(Building_Bed bed, Pawn guest)
    {
        var score = 0;
        GetUsersOfBed(bed, guest, _tmpOwners);

        // Take opinion of others into account
        foreach (var otherOwner in _tmpOwners)
        {
            if (!RimWorld.BedUtility.WillingToShareBed(guest, otherOwner) || !RimWorld.BedUtility.WillingToShareBed(otherOwner, guest)) score += -340;
            else
            {
                // Worst opinion counts, we're respectful like that
                var opinions = Mathf.Min(guest.relations.OpinionOf(otherOwner), otherOwner.relations.OpinionOf(guest));
                if (opinions < 15) score += -340;
                else
                {
                    score += (opinions - 15) * 4;
                }
            }
        }

        return score;
    }

    private static void GetUsersOfBed(Building_Bed bed, Pawn except, HashSet<Pawn> ownersOut)
    {
        ownersOut.Clear();
        ownersOut.AddRange(bed.Owners());
        bed.Map.reservationManager.ReserversOf(bed, ownersOut);
        ownersOut.Remove(except);
        ownersOut.Remove(null);
    }

    private static int GetRoyalExpectations(Building_GuestBed bed, Pawn guest, Room room, out RoyalTitle title)
    {
        title = guest.royalty?.HighestTitleWithBedroomRequirements();
        if (title == null) return 0;

        var royalExpectations = 0;
        if (room == null) royalExpectations -= 75;
        else
        {
            foreach (var roomBeds in room.ContainedBeds)
            {
                if (roomBeds != bed && BedClaimedByStranger(roomBeds, guest))
                {
                    royalExpectations -= 100;
                }
            }
        }

        if (RoyalTitleUtility.BedroomSatisfiesRequirements(room, title)) royalExpectations += 100;

        return royalExpectations;
    }

    private static bool BedClaimedByStranger(Building_Bed bed, Pawn guest)
    {
        GetUsersOfBed(bed, guest, _tmpOwners);
        return _tmpOwners.Any(p => !p.RaceProps.Animal && !LovePartnerRelationUtility.LovePartnerRelationExists(p, guest));
    }

    public static int StaticBedValue(Building_GuestBed bed, [CanBeNull] out Room room, out int quality, out int impressiveness, out int roomTypeScore, out int comfort, out int facilities)
    {
        room = bed.Map != null && bed.Map.regionAndRoomUpdater.Enabled ? bed.GetRoom() : null;

        //Facilities
        facilities = GetFacilityScore(bed); // facilityCount * 10
        quality = GetBedQuality(bed);
        impressiveness = room != null ? GetRoomImpressiveness(room) : 0;
        roomTypeScore = GetRoomTypeScore(room) * 2;
        comfort = Mathf.RoundToInt(100 * GetBedComfort(bed));
        return quality + impressiveness + roomTypeScore + comfort + facilities;
    }

    private static float GetBedComfort(Building_GuestBed bed)
    {
        return bed.GetStatValue(StatDefOf.Comfort);
    }

    private static int GetBedQuality(Building_Bed bed)
    {
        if (!bed.TryGetQuality(out var category)) category = QualityCategory.Normal;
        return ((int)category - 2) * 25;
    }

    private static int GetRoomImpressiveness(Room room)
    {
        return Mathf.RoundToInt(room.GetStat(RoomStatDefOf.Impressiveness));
    }

    private static float GetTemperatureScore(Pawn guest, Room room, Building_Bed bed)
    {
        if (room == null) return 0;
        var optimalTemperature = GenTemperature.ComfortableTemperatureRange(guest.def);
        var pctTemperature = Mathf.Abs(optimalTemperature.InverseLerpThroughRange(room.Temperature) - 0.5f) * 2; // 0-1
        return Mathf.RoundToInt(Mathf.Lerp(0, -200, pctTemperature - 0.75f) * 4); // -200 - 0
    }

    private static int GetRoomTypeScore(Room room)
    {
        if (room == null) return -30;
        if (room.OutdoorsForWork) return -50;

        int roomType;
        if (room.Role == RoomRoleDefOf.Barracks) roomType = 0;
        else if (room.Role == InternalDefOf.GuestRoom) roomType = 30;
        else roomType = -30;
        if (room.OnlyOneBed()) roomType += 60;
        return roomType;
    }

    public static IEnumerable<Building_GuestBed> GetGuestBeds(this Map map, Area area = null)
    {
        if (map == null) return Array.Empty<Building_GuestBed>();
        if (area == null) return map.listerBuildings.AllBuildingsColonistOfClass<Building_GuestBed>();
        return map.listerBuildings.AllBuildingsColonistOfClass<Building_GuestBed>().Where(b => area[b.Position]);
    }

    public static Building_Bed GetGuestBed(Pawn pawn)
    {
        var compGuest = pawn.CompGuest();
        return compGuest?.bed;
    }

    public static bool IsGuestBed(this Building_Bed bed)
    {
        return bed is Building_GuestBed;
    }
}
