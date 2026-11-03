using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Hospitality.Utilities;

internal static class ShoppingSearchUtility
{
    internal static List<Thing> CollectCandidates(Pawn pawn, Area area,
        Func<IntVec3, bool> hasRecentlyLookedAt, Func<Thing, bool> qualifies)
    {
        var map = pawn.MapHeld;
        var candidates = new List<Thing>();
        var storage = new List<Building_Storage>();
        // Grid contents and multi-cell shelves can expose the same thing repeatedly. Reuse its
        // reservation/inventory/price/trade checks during this synchronous read-only search.
        // Keep this local: another pawn, inventory change or later search needs fresh results.
        // Stockpile-only shops usually expose each item once. Allocate a memo only when a shelf
        // is encountered, avoiding a dictionary proportional to all stock in the common simple case.
        Dictionary<Thing, bool> buyability = null;

        foreach (var cell in area.ActiveCells)
        {
            if (hasRecentlyLookedAt(cell)) continue;
            if (map.edificeGrid[cell] is Building_Storage shelf)
            {
                storage.Add(shelf);
                if (buyability == null)
                {
                    buyability = new Dictionary<Thing, bool>(ReferenceEqualityComparer.Instance);
                    // Grid candidates before the first shelf have already passed buyability.
                    // Seed those successes; earlier rejected items are simply rechecked if needed.
                    foreach (var candidate in candidates) buyability.TryAdd(candidate, true);
                }
            }
            foreach (var thing in map.thingGrid.ThingsListAtFast(cell)) AddCandidate(thing);
        }

        // Append storage contents after all grid contents, as before. A shelf occurring at
        // multiple cells is intentionally kept multiple times: removing entries would alter
        // item selection weights even though rechecking their buyability is unnecessary.
        foreach (var shelf in storage)
        foreach (var thing in shelf.slotGroup.HeldThings)
            AddCandidate(thing);

        return candidates;

        void AddCandidate(Thing thing)
        {
            // Keep the virtual qualification filter before expensive checks. Walls, filth etc.
            // never enter the dictionary, and subclass rules are still applied at each occurrence.
            if (thing == null || !qualifies(thing)) return;
            if (buyability == null)
            {
                if (ItemUtility.IsBuyableAtAll(pawn, thing)) candidates.Add(thing);
                return;
            }
            if (!buyability.TryGetValue(thing, out var canBuy))
            {
                canBuy = ItemUtility.IsBuyableAtAll(pawn, thing);
                buyability.Add(thing, canBuy);
            }
            if (canBuy) candidates.Add(thing);
        }
    }
}
