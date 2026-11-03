using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace Hospitality.Utilities;

// Optional CompInventory integration. Cache the API binding, never the capacity result:
// reservations, item counts and carried mass can change between consecutive shopping checks.
internal static class InventoryCapacityCompatibility
{
    private delegate bool CapacityCheck(Thing thing, out int count, bool flagA, bool flagB);
    private static readonly Dictionary<Type, MethodInfo> methods = new();
    private static readonly HashSet<Type> reportedTypes = new();
    // Closed delegates refer to their component. Weak keys avoid retaining pawns/old games;
    // ConditionalWeakTable also permits the value to refer back to its own key safely.
    private static readonly ConditionalWeakTable<ThingComp, Reader> readers = new();

    internal static int GetSpace(ThingComp inventory, Thing thing)
    {
        if (inventory == null) return thing.stackCount;
        return readers.GetValue(inventory, CreateReader).GetSpace(thing);
    }

    private static Reader CreateReader(ThingComp inventory)
    {
        var type = inventory.GetType();
        try
        {
            if (!methods.TryGetValue(type, out var method))
            {
                // The previous integration searched declared methods. Require the complete
                // known signature rather than selecting an unrelated overload or indexing an
                // empty parameter list. Cache missing APIs too, avoiding repeated discovery.
                foreach (var candidate in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (candidate.Name != "CanFitInInventory" || candidate.ReturnType != typeof(bool) || candidate.ContainsGenericParameters) continue;
                    var parameters = candidate.GetParameters();
                    if (parameters.Length != 4 || parameters[0].ParameterType != typeof(Thing)
                        || parameters[1].ParameterType != typeof(int).MakeByRefType() || !parameters[1].IsOut
                        || parameters[2].ParameterType != typeof(bool) || parameters[3].ParameterType != typeof(bool)) continue;
                    method = candidate;
                    break;
                }
                methods.Add(type, method);
            }
            if (method != null)
                return new Reader(type, (CapacityCheck)method.CreateDelegate(typeof(CapacityCheck), inventory));
            Report(type, "compatible CanFitInInventory(Thing, out int, bool, bool) method not found");
        }
        catch (Exception exception)
        {
            Report(type, exception.ToString());
        }
        return new Reader(type, null);
    }

    private static void Report(Type type, string reason)
    {
        // A broken optional integration can otherwise emit an exception for every shop item.
        // Keep the original stack-count fallback, but report only the first failure per type.
        if (reportedTypes.Add(type)) Log.Error($"Hospitality inventory capacity integration for {type.FullName}: {reason}. Falling back to the item stack count.");
    }

    private sealed class Reader
    {
        private readonly Type type;
        private readonly CapacityCheck check;

        internal Reader(Type type, CapacityCheck check) { this.type = type; this.check = check; }

        internal int GetSpace(Thing thing)
        {
            if (check == null) return thing.stackCount;
            try
            {
                // Preserve the previous CE arguments and its success/out-count interpretation.
                // No argument array, boxed flags/count, or MethodInfo.Invoke on this hot path.
                return check(thing, out var count, false, true) ? count : 0;
            }
            catch (Exception exception)
            {
                Report(type, exception.ToString());
                return thing.stackCount;
            }
        }
    }
}
