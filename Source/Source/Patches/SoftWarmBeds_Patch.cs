using System.Reflection;
using HarmonyLib;
using Hospitality.Utilities;
using RimWorld;

namespace Hospitality.Patches;

[HarmonyPatch]
internal static class SoftWarmBeds_Patch
{
    private static readonly MethodInfo legacySwap = SoftWarmBedsCompatibility.FindMethod("SoftWarmBeds.Hospitality_Patch", "SwapPatch");

    [HarmonyPrepare]
    private static bool Prepare()
    {
        // Only override the inspected legacy API; leave unrelated future signatures alone.
        if (legacySwap == null || !legacySwap.IsStatic || legacySwap.ReturnType != typeof(bool)) return false;
        var parameters = legacySwap.GetParameters();
        return parameters.Length == 2 && parameters[0].ParameterType == typeof(object)
            && parameters[1].ParameterType == typeof(Building_Bed);
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod() => legacySwap;

    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        // SWB's old prefix replaces the entire Hospitality swap and transplants every comp.
        // Return "run Hospitality's original" from that prefix; our swap now preserves just
        // SWB's bedding comp before the old bed despawns, with no hard assembly dependency.
        __result = true;
        return false;
    }
}
