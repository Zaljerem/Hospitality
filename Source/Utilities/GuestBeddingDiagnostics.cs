using System;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Hospitality.Utilities;

// Opt-in developer diagnostic: no render hooks or reflection run during ordinary play.
// A short capture distinguishes missing frame draws from retained bedding with a bad graphic.
internal static class GuestBeddingDiagnostics
{
    private static readonly Harmony traceHarmony = new("Hospitality.GuestBeddingDiagnostics");
    private static ThingComp tracedComp;
    private static MethodInfo tracedMethod;
    private static int attempts;
    private static int completions;

    internal static Command_Action ColorCommand(Building_GuestBed bed)
    {
        return new Command_Action
        {
            defaultLabel = bed.debugUseBaseSheetColor ? "DEV: Restore guest color" : "DEV: Test base sheet color",
            defaultDesc = "Temporarily use Building_Bed's sheet-color getter, including Soft Warm Beds' existing patches. Click again to restore purple.",
            action = () =>
            {
                bed.debugUseBaseSheetColor = !bed.debugUseBaseSheetColor;
                // Refresh both graphics so the comparison cannot be masked by cached colors.
                bed.Notify_ColorChanged();
                var comp = bed.AllComps.FirstOrDefault(c => c.GetType().FullName == SoftWarmBedsCompatibility.ComponentTypeName);
                if (comp != null && AccessTools.Field(comp.GetType(), "blanket")?.GetValue(comp) is Thing blanket)
                    blanket.Notify_ColorChanged();
            }
        };
    }

    internal static Command_Action Command(Building_Bed bed)
    {
        return new Command_Action
        {
            defaultLabel = tracedComp == null ? "DEV: Trace bedding" : "DEV: Finish bedding trace",
            defaultDesc = "Start on the affected bed, leave it visible for a few seconds, then finish. The report is also written to the game log.",
            action = () => Capture(bed)
        };
    }

    private static void Capture(Building_Bed bed)
    {
        if (tracedComp != null)
        {
            // Remove only our temporary probe, preserving all other mods' patches.
            traceHarmony.Unpatch(tracedMethod, HarmonyPatchType.All, traceHarmony.Id);
            var report = Describe((Building_Bed)tracedComp.parent)
                         + $"\nPostDraw calls attempted/completed: {attempts}/{completions}";
            tracedComp = null;
            tracedMethod = null;
            Show(report);
            return;
        }

        var comp = bed.AllComps.FirstOrDefault(c => c.GetType().FullName == SoftWarmBedsCompatibility.ComponentTypeName);
        if (comp == null)
        {
            Show(Describe(bed));
            return;
        }

        tracedMethod = AccessTools.Method(comp.GetType(), nameof(ThingComp.PostDraw));
        attempts = completions = 0;
        try
        {
            traceHarmony.Patch(tracedMethod,
                prefix: new HarmonyMethod(typeof(GuestBeddingDiagnostics), nameof(BeforeDraw)),
                postfix: new HarmonyMethod(typeof(GuestBeddingDiagnostics), nameof(AfterDraw)));
            tracedComp = comp;
            Show(Describe(bed) + "\nTrace started. Keep this bed visible for a few seconds, then click Finish bedding trace.");
        }
        catch (Exception exception)
        {
            traceHarmony.Unpatch(tracedMethod, HarmonyPatchType.All, traceHarmony.Id);
            tracedMethod = null;
            Show("Could not start bedding trace: " + exception);
        }
    }

    private static void BeforeDraw(object __instance)
    {
        if (ReferenceEquals(__instance, tracedComp)) attempts++;
    }

    private static void AfterDraw(object __instance)
    {
        if (ReferenceEquals(__instance, tracedComp)) completions++;
    }

    private static string Describe(Building_Bed bed)
    {
        var text = new StringBuilder("Hospitality guest bedding diagnostic\n");
        text.AppendLine($"Bed: {bed.ThingID}; def: {bed.def.defName}; drawer: {bed.def.drawerType}; spawned: {bed.Spawned}");
        text.AppendLine($"Bed colors: primary {bed.DrawColor}; secondary {bed.DrawColorTwo}; base-color test: {(bed as Building_GuestBed)?.debugUseBaseSheetColor}");
        text.AppendLine($"Registered for dynamic drawing: {bed.Map?.dynamicDrawManager.DrawThings.Contains(bed)}");
        var comp = bed.AllComps.FirstOrDefault(c => c.GetType().FullName == SoftWarmBedsCompatibility.ComponentTypeName);
        if (comp == null) return text.AppendLine("Soft Warm Beds component missing.").ToString();
        text.AppendLine($"Component parent matches: {ReferenceEquals(comp.parent, bed)}; assembly: {comp.GetType().Assembly.FullName}");
        // These are the fields in the supplied SWB assembly. Missing fields are reported explicitly
        // so this diagnostic remains useful with a different installed version.
        foreach (var name in new[] { "loaded", "loadedBedding", "blanketDef", "blanketStuff", "blanket", "NotTheBlanket" })
        {
            var field = AccessTools.Field(comp.GetType(), name);
            var value = field?.GetValue(comp);
            text.AppendLine($"{name}: {(field == null ? "<field missing>" : value is Def def ? def.defName : value?.ToString() ?? "<null>")}");
        }
        foreach (var method in new[] {
                     AccessTools.Method(comp.GetType(), nameof(ThingComp.PostDraw)),
                     AccessTools.Method(bed.GetType(), "DrawAt"),
                     AccessTools.Method(bed.GetType(), "DynamicDrawPhase"),
                     AccessTools.Method(bed.GetType(), "DynamicDrawPhaseAt") })
        {
            text.AppendLine($"Draw method: {method?.DeclaringType?.FullName}.{method?.Name}");
            var patches = method == null ? null : Harmony.GetPatchInfo(method);
            if (patches != null)
                foreach (var patch in patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers).Concat(patches.Finalizers))
                    text.AppendLine($"  Patch: {patch.owner}: {patch.PatchMethod.DeclaringType?.FullName}.{patch.PatchMethod.Name}");
        }
        return text.ToString();
    }

    private static void Show(string report)
    {
        Log.Message(report);
        Find.WindowStack.Add(new Dialog_MessageBox(report));
    }
}
