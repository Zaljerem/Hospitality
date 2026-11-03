using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace Hospitality.Patches;

/// <summary>
///     Make filters for beds also accept guest beds - DefModExtension version
/// </summary>
public class ThingFilter_Patch
{
    [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Allows), typeof(ThingDef))]
    public static class ThingFilter_Allows_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ref ThingDef def)
        {
            var ext = def?.GetModExtension<GuestBedExtension>();
            if (ext?.originalBed != null)
            {
                def = ext.originalBed;
            }
        }
    }

}