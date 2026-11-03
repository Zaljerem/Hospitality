using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using GuestUtility = Hospitality.Utilities.GuestUtility;

namespace Hospitality.Patches
{
    /// <summary>
    /// Allow colonists to talk to guests randomly
    /// </summary>
    internal static class Pawn_InteractionsTracker_Patch
    {
        [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractRandomly))]
        public class TryInteractRandomly
        {
            [HarmonyPrefix]
            public static bool Replacement(Pawn_InteractionsTracker __instance, ref bool __result, Pawn ___pawn, List<Pawn> ___workingList, int ___lastInteractionTime)
            {
                // Added
                if (!IsInteractable(___pawn))
                {
                    __result = false;
                    return false;
                }

                if (InteractedTooRecentlyToInteract(___lastInteractionTime)) // Changed to own
                {
                    __result = false;
                    return false;
                }
                // BASE
                if (!SocialInteractionUtility.CanInitiateRandomInteraction(___pawn))
                {
                    __result = false;
                    return false;
                }
                ___workingList.Clear();
                // Reuse the tracker list without allocating a LINQ iterator on each social search.
                // Keep map order and the same shuffle so recipient selection is unchanged.
                foreach (var candidate in ___pawn.MapHeld.mapPawns.AllPawnsSpawned)
                {
                    if (IsInteractable(candidate)) ___workingList.Add(candidate);
                }
                ___workingList.Shuffle();
                List<InteractionDef> allDefsListForReading = DefDatabase<InteractionDef>.AllDefsListForReading;
                foreach (var p in ___workingList)
                {
                    if (p != ___pawn && CanInteractNowWith(___pawn, p) && SocialInteractionUtility.CanReceiveRandomInteraction(p)
                        && !___pawn.HostileTo(p))
                    {
                        // The full recipient check above already established position, reservation
                        // and visibility for this synchronous search. Only interaction-specific
                        // permission varies while weighting definitions; do not repeat those scans.
                        if (
                            allDefsListForReading.TryRandomElementByWeight(
                                x => !CanInteractForDefinition(___pawn, p, x) ? 0f : x.Worker.RandomSelectionWeight(___pawn, p), out var result))
                        {
                            // Weight workers can be supplied by other mods. Recheck the selected
                            // interaction once before committing in case one changed pawn/map state.
                            if (!CanInteractNowWith(___pawn, p, result)) continue;
                            if (__instance.TryInteractWith(p, result))
                            {
                                ___workingList.Clear();
                                __result = true;
                                return false;
                            }
                            Log.Warning($"{___pawn} failed to interact with {p}.");
                        }
                    }
                }
                ___workingList.Clear();
                __result = false;
                return false;
            }

            private static bool IsInteractable(Pawn pawn) // Added
            {
                return pawn?.Downed == false && pawn.RaceProps.Humanlike && pawn.relations != null && pawn.story?.traits != null;
            }

            private static bool CanInteractNowWith(Pawn pawn, Pawn recipient,  InteractionDef interactionDef = null) 
            {
                if (!recipient.Spawned)
                {
                    return false;
                }
                if (!SocialInteractionUtility.IsGoodPositionForInteraction(pawn, recipient))
                {
                    return false;
                }
                if (!CanInteractForDefinition(pawn, recipient, interactionDef))
                {
                    return false;
                }
                // Added
                if (!pawn.CanReserve(recipient) || !recipient.CanReserve(pawn))
                {
                    return false;
                }
                if (!GenSight.LineOfSight(pawn.Position, recipient.Position, pawn.MapHeld, true))
                {
                    return false;
                }
                return true;
            }

            private static bool CanInteractForDefinition(Pawn pawn, Pawn recipient, InteractionDef interactionDef)
            {
                // Evaluate these for every definition: mods can impose different permissions
                // for different interactions even when the two pawns can generally socialize.
                return SocialInteractionUtility.CanInitiateInteraction(pawn, interactionDef)
                    && SocialInteractionUtility.CanReceiveInteraction(recipient, interactionDef);
            }

            // Added to change InteractIntervalAbsoluteMin
            public static bool InteractedTooRecentlyToInteract(int ___lastInteractionTime)
            {
                return Find.TickManager.TicksGame < ___lastInteractionTime + GuestUtility.InteractIntervalAbsoluteMin;
            }
        }
    }
}
