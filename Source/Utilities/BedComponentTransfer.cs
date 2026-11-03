using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Hospitality.Utilities;

// A bed conversion creates a new Thing, but selected optional-mod components own state that
// must survive it. Transfer only the requested component, keeping every other new comp intact.
internal sealed class BedComponentTransfer : IDisposable
{
    private static readonly FieldInfo componentCache = AccessTools.Field(typeof(ThingWithComps), "compsByType");
    private readonly ThingWithComps source;
    private readonly ThingWithComps destination;
    private readonly ThingComp component;
    private readonly ThingComp replacement;
    private readonly CompProperties originalProperties;
    private readonly int sourceIndex;
    private readonly int destinationIndex;
    private bool committed;

    private BedComponentTransfer(ThingWithComps source, ThingWithComps destination,
        ThingComp component, ThingComp replacement, int sourceIndex, int destinationIndex)
    {
        this.source = source;
        this.destination = destination;
        this.component = component;
        this.replacement = replacement;
        this.sourceIndex = sourceIndex;
        this.destinationIndex = destinationIndex;
        originalProperties = component.props;
        source.AllComps.RemoveAt(sourceIndex);
        destination.AllComps[destinationIndex] = component;
        component.parent = destination;
        component.props = replacement.props;
        RefreshComponentCache(source);
        RefreshComponentCache(destination);
    }

    private static void RefreshComponentCache(ThingWithComps thing)
    {
        // This game build's GetComp<T> reads a type index built by InitializeComps; changing
        // AllComps alone leaves lookups pointing to the removed component. Rebuild that index
        // after transfers/rollback so making, inspecting and drawing use the same component.
        // Older builds without this field continue to use their list-based lookup.
        if (componentCache == null) return;
        componentCache.SetValue(thing, thing.AllComps.GroupBy(comp => comp.GetType())
            .ToDictionary(group => group.Key, group => group.ToArray()));
    }

    internal static BedComponentTransfer Begin(ThingWithComps source, ThingWithComps destination, string componentTypeName)
    {
        var sourceIndex = source.AllComps.FindIndex(comp => comp.GetType().FullName == componentTypeName);
        if (sourceIndex < 0) return null; // Optional integration is absent on this bed.
        var destinationIndex = destination.AllComps.FindIndex(comp => comp.GetType().FullName == componentTypeName);
        if (destinationIndex < 0)
            throw new InvalidOperationException($"[Hospitality] Cannot preserve {componentTypeName}: the converted bed has no matching component.");
        return new BedComponentTransfer(source, destination, source.AllComps[sourceIndex],
            destination.AllComps[destinationIndex], sourceIndex, destinationIndex);
    }

    internal void Commit() => committed = true;

    public void Dispose()
    {
        // Roll back an unsuccessful pre-spawn conversion. If spawning already destroyed the
        // old bed, keep the state on the new bed instead of reattaching it to a destroyed Thing.
        if (committed || source.Destroyed) return;
        destination.AllComps[destinationIndex] = replacement;
        source.AllComps.Insert(sourceIndex, component);
        component.parent = source;
        component.props = originalProperties;
        RefreshComponentCache(source);
        RefreshComponentCache(destination);
        committed = true;
    }
}
