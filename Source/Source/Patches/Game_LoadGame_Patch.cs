using HarmonyLib;
using Hospitality.Utilities;
using Verse;

namespace Hospitality.Patches
{
    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    public static class Game_LoadGame_Patch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            ThoughtResultCache.Reset();
            // Relationship tooltips also contain save-specific pawn data, even if a load reuses
            // the current Game object and its tick happens to be inside the refresh interval.
            RelationUtility.Reset();
        }
    }
}
