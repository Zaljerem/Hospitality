using System;
using System.Reflection;
using RimWorld;
using Verse;
using HarmonyLib;

namespace Hospitality.Utilities;

internal static class TDPackAreas
{
    // TD Enhancement Pack mod adds various area improvements, including increasing the maximum number of areas,
    // and marking some as not for colonists/animals/mechs, in order to make the number of areas shown manageable.

    private delegate bool IsForDelegate(Area area);
    private static IsForDelegate IsForColonists;
    static TDPackAreas()
    {
        MethodInfo func = AccessTools.Method("TD_Enhancement_Pack.MapComponent_AreaOrder:IsForColonists");
        if(func != null)
            IsForColonists = (IsForDelegate) Delegate.CreateDelegate(typeof(IsForDelegate), null, func, true);
    }

    public static bool IsAllowed(Area area)
    {
        if(IsForColonists == null)
            return true; // Always.
        if(IsForColonists(area))
            return true;
        // As a special case, allow also all areas ending with "Hospitality", which allows disabling showing
        // them for colonists/animals/mechs, so that they do not show up anywhere else. End of the name rather
        // than the start of the name is so that several of them can be differentiated with the little space
        // that is available in the UI.
        if(area.Label.EndsWith("Hospitality"))
            return true;
        return false;
    }
}
