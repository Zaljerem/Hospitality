using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Verse;

namespace Hospitality.Utilities
{
    public static class ThingCache
    {
        [NotNull]
        // A cached machine points back to its map. Weak keys handle that cycle without keeping
        // discarded maps, their buildings or an old save alive through this static registry.
        private static readonly ConditionalWeakTable<Map, ThingCacheSet> allCacheSets = new();

        [NotNull]
        public static ThingCacheSet GetSetFor(Map map)
        {
            if (map == null)
            {
                Log.Error($"Hospitality ThingCache: Called with map == null.");
                return new ThingCacheSet();
            }

            return allCacheSets.GetValue(map, _ => new ThingCacheSet());
        }

        public static void TryRegisterNewThing(Thing thing)
        {
            // SpawnSetup is patched for every Thing, including ordinary stockpiles and terrain
            // clutter. Do not create/query a map registry unless this thing can be a machine.
            if (thing is not ThingWithComps withComps || withComps.Map == null
                || withComps.TryGetComp<CompVendingMachine>() == null) return;
            GetSetFor(withComps.Map).RegisterVendingMachine(withComps);
        }

        public static void TryDeregister(Thing thing, Map oldMap)
        {
            if (thing is not ThingWithComps || oldMap == null) return;
            // Use the supplied pre-despawn map. Do not require the comp to still be attached,
            // since an integration can remove it before the despawn notification arrives.
            if (allCacheSets.TryGetValue(oldMap, out var set))
            {
                set.Deregister(thing);
            }
        }

        public class ThingCacheSet
        {
            private readonly List<Thing> vendingMachines = new();
            public IEnumerable<Thing> AllVendingMachines => vendingMachines;

            public void TryRegister(Thing newThing)
            {
                if (newThing is ThingWithComps thingWithComps)
                {
                    if (thingWithComps.TryGetComp<CompVendingMachine>() != null)
                    {
                        RegisterVendingMachine(newThing);
                    }
                }
            }

            internal void RegisterVendingMachine(Thing thing)
            {
                // Keep first-registration order for work scans, but do not enqueue the same
                // machine twice when a spawn notification is repeated.
                if (!vendingMachines.Contains(thing)) vendingMachines.Add(thing);
            }

            public void Deregister(Thing thing)
            {
                vendingMachines.Remove(thing);
            }
        }
    }
}
