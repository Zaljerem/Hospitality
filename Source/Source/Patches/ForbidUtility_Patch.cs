using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using System.Reflection;
using System.Reflection.Emit;
using Hospitality.Utilities;
using Verse;

namespace Hospitality.Patches
{
    internal static class ForbidUtility_Patch
    {
        /// <summary>
        /// So guests will care
        /// </summary>
        [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.CaresAboutForbidden))]
        public static class CaresAboutForbidden
        {
            static bool Prefix(Pawn pawn, ref bool __result)
            {
                if (pawn != null && pawn.IsGuest())
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Set by JobDriver_Patch and stores who is doing a toil right now, in which case we don't want to forbid things.
        /// </summary>
#pragma warning disable 649 // Its set via reflection.
        public static Pawn currentToilWorker;
#pragma warning restore 649

        /// <summary>
        /// Things dropped by guests are never forbidden
        /// </summary>
        [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.SetForbidden))]
        public class SetForbidden
        {
            [HarmonyPrefix]
            public static bool Prefix(bool value)
            {
                //if (value && currentToilWorker.IsArrivedGuest(out _))
                if (value && currentToilWorker != null && currentToilWorker.IsArrivedGuest(out _))
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Area check for guests trying to access things outside their zone.
        /// Reworked to prefix to try and improve performance
        /// </summary>
        [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.InAllowedArea))]
        public static class InAllowedArea_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(IntVec3 c, Pawn forPawn, ref bool __result)
            {
                if (forPawn == null)
                    return true;

                if (!forPawn.IsArrivedGuest(out var guestComp))
                    return true;

                var area = guestComp.GuestArea;
                if (area == null)
                    return true;

                if (!c.IsValid)
                {
                    __result = false;
                    return false;
                }

                if (!area[c])
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Check if it is politically proper. This only applies in some cases, so turn the flag on/off before and after.
        /// </summary>
        [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), typeof(Thing), typeof(Pawn))]
        public class IsForbidden
        {
            public static bool checkPoliticallyProper;

            [HarmonyPostfix]
            public static void Postfix(Thing t, Pawn pawn, ref bool __result)
            {
                if (!checkPoliticallyProper) return;
                if (__result || !pawn.IsGuest()) return;
                // Not forbidden, but also not proper? Then forbid
                if(!t.IsPoliticallyProper(pawn)) __result = true;
            }
        }

        /// <summary>
        /// Make sure guests don't use the player's drugs
        /// </summary>
        [HarmonyPatch(typeof(JoyGiver_SocialRelax), nameof(JoyGiver_SocialRelax.TryFindIngestibleToNurse))]
        public class TryFindIngestibleToNurse
        {
            [HarmonyPrefix]
            public static void Prefix(Pawn ingester)
            {
                if (ingester.IsGuest())
                {
                    IsForbidden.checkPoliticallyProper = true;
                }
            }
            [HarmonyPostfix]
            public static void Postfix()
            {
                IsForbidden.checkPoliticallyProper = false;
            }
        }
    }
}
