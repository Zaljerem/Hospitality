using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hospitality;
using Hospitality.Utilities;
using Verse;

namespace Hospitality.Patches
{
    [HarmonyPatch(typeof(Game))]
    [HarmonyPatch("FinalizeInit")]
    [HarmonyPatch(new Type[]
    {

    })]
    internal static class Game_FinalizeInit_Patch
    {
        [HarmonyPostfix]
        private static void WorldLoadedHook()
        {
            foreach (var map in Find.Maps)
            {
                if (map == null) continue;
                var comp = map.GetMapComponent();
                if (comp == null)
                {
                    Log.Warning($"[Hospitality] MapComponent was null during WorldLoadedHook for map: {map}");
                    continue;
                }

                try
                {
                    comp.OnWorldLoaded();
                }
                catch (System.Exception e)
                {
                    Log.Error($"[Hospitality] Exception in OnWorldLoaded for map {map}: {e}");
                }
            }

            try
            {
                GuestUtility.Initialize();
            }
            catch (System.Exception e)
            {
                Log.Error($"[Hospitality] Exception in GuestUtility.Initialize: {e}");
            }
        }
    }
}
