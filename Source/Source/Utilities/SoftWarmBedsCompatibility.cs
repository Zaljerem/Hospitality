using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Hospitality.Utilities;

internal static class SoftWarmBedsCompatibility
{
    internal const string ComponentTypeName = "SoftWarmBeds.CompMakeableBed";
    private delegate void InspectAppender(object bed, ref string text);
    private static readonly MethodInfo inspectMethod = FindMethod("SoftWarmBeds.Building_Bed_GetInspectString", "Postfix");
    private static readonly MethodInfo guestInspectMethod = AccessTools.Method(typeof(Building_GuestBed), nameof(Building_GuestBed.GetInspectString));
    private static readonly InspectAppender appendInspect = CreateInspectAppender();
    private static readonly Type componentType = AccessTools.TypeByName(ComponentTypeName);

    internal static void TickBed(Building_Bed bed)
    {
        if (componentType == null) return;
        // Guest beds use TickLong, but SWB implements its material-filter check only in CompTick.
        // Service that one optional comp at the guest-bed interval instead of reticking every comp
        // or changing all guest beds to normal ticking. Manual unmake rules remain SWB's own.
        foreach (var comp in bed.AllComps)
        {
            if (!componentType.IsInstanceOfType(comp)) continue;
            comp.CompTick();
            break;
        }
    }

    internal static MethodInfo FindMethod(string typeName, string methodName)
    {
        var type = AccessTools.TypeByName(typeName);
        return type == null ? null : AccessTools.Method(type, methodName);
    }

    private static InspectAppender CreateInspectAppender()
    {
        if (inspectMethod == null) return null;
        var parameters = inspectMethod.GetParameters();
        // Bind only the inspected API; optional-mod updates with a different signature are skipped.
        if (inspectMethod.ReturnType != typeof(void) || !inspectMethod.IsStatic || parameters.Length != 2
            || parameters[0].ParameterType != typeof(object) || parameters[1].ParameterType != typeof(string).MakeByRefType()) return null;
        return (InspectAppender)Delegate.CreateDelegate(typeof(InspectAppender), inspectMethod);
    }

    internal static string AppendInspectString(Building_Bed bed, string text)
    {
        if (appendInspect == null) return text;
        // SWB already patches guest inspection in some versions/load configurations. Do not
        // append a duplicate line when that existing postfix will run after this method returns.
        var patches = Harmony.GetPatchInfo(guestInspectMethod);
        if (patches?.Postfixes.Any(patch => patch.PatchMethod == inspectMethod) == true) return text;
        appendInspect(bed, ref text);
        return text;
    }
}
