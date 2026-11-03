using HarmonyLib;
using Hospitality.Utilities;
using RimWorld;
using Verse;

namespace Hospitality.Patches;

/// <summary>
///     Allows guests to store things in shelves
/// </summary>
public class StoreUtility_Patch
{
    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterNonSlotGroupStorageFor))]
    public class TryFindBestBetterNonSlotGroupStorageFor
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn carrier, ref Faction faction)
        {
            if (carrier == null) return true;
            if (!carrier.IsGuest()) return true;

            // Guests act as if they're host faction for this purpose
            faction = carrier.HostFaction;
            return true;
        }
    }
}