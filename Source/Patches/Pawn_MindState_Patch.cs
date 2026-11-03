using HarmonyLib;
using Hospitality.Utilities;
using Verse.AI;

namespace Hospitality.Patches
{
    /// <summary>
    /// So downed guests stay traders.
    /// </summary>
    public class Pawn_MindState_Patch
    {
        [HarmonyPatch(typeof(Pawn_MindState), "Reset", new[] { typeof(bool), typeof(bool), typeof(bool) })]
        public static class Pawn_MindState_Reset_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn_MindState __instance)
            {
                if (!__instance.pawn.IsGuest()) return;
                __instance.pawn.ConvertToTrader(false);
            }
        }
    }
}
