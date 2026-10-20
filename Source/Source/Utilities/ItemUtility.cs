using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using RimWorld;
using Waylith.Math;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace Hospitality.Utilities;

public static class ItemUtility
{
    private static readonly Dictionary<string, MethodInfo> alienFrameworkMethods = new();

    public static float priceFactor = 0.55f;


    public static void PocketHeadgear(this Pawn pawn)
    {
        if (pawn?.apparel?.WornApparel == null || pawn.inventory?.innerContainer == null) return;
        // Don't do it when the pawn wants to stay anonymous
        if (ModsConfig.IdeologyActive && pawn.ideo?.Ideo?.HasPrecept(InternalDefOf.VME_Anonymity_Required) == true) return;

        var headgear = pawn.apparel.WornApparel.Where(CoversHead).ToArray();
        foreach (var apparel in headgear)
        {
            if (apparel == null) continue;
            // Don't drop space headgear
            if (apparel.def.defName == "Apparel_SpaceSuitHelmet") continue;
            if (apparel.def.defName == "Apparel_VacsuitHelmet") continue;
            // Don't drop headgear that is required by title
            if (IsRequiredByRoyalty(pawn, apparel.def)) continue;

            if (pawn.GetInventorySpaceFor(apparel) < 1) continue;

            if (pawn.apparel.TryDrop(apparel, out var droppedApp))
            {
                var item = droppedApp?.SplitOff(1);
                if (item != null)
                {
                    pawn.inventory.TryAddItemNotForSale(item);
                    var success = pawn.inventory.innerContainer.Contains(item);
                    if (!success) pawn.apparel.Wear(droppedApp);
                }
            }
        }
    }

    public static bool IsRequiredByRoyalty(Pawn pawn, ThingDef apparelDef)
    {
        if (pawn.royalty == null) return false;
        try
        {
            return pawn.royalty.AllTitlesForReading.Any(title => title.def.requiredApparel != null && title.def.requiredApparel.Exists(req => req.ApparelMeetsRequirement(apparelDef)));
        }
        catch (Exception e)
        {
            Log.Error($"Failed to read royalty titles or their required apparel. This means you are using a mod that changes these and broke them.\n{e}");
            return false;
        }
    }

    public static bool CoversHead(this Apparel a)
    {
        return a.def.apparel.bodyPartGroups.Any(g => g == BodyPartGroupDefOf.UpperHead || g == BodyPartGroupDefOf.FullHead);
    }

    public static void WearHeadgear(this Pawn pawn)
    {
        if (pawn?.apparel?.WornApparel == null || pawn.inventory?.innerContainer == null) return;

        var container = pawn.inventory.innerContainer;
        var headgear = container.OfType<Apparel>().Where(CoversHead).InRandomOrder().ToArray();
        foreach (var apparel in headgear)
        {
            if (pawn.apparel.CanWearWithoutDroppingAnything(apparel.def))
            {
                container.Remove(apparel);
                pawn.apparel.Wear(apparel);
            }
        }
    }

    public static void TryGiveBackpack(this Pawn p)
    {
        var def = InternalDefOf.CE_Apparel_Backpack;
        if (def == null) return;

        var item = p.inventory.innerContainer.OfType<Apparel>().FirstOrDefault(i => i.def == def);
        if (item == null)
        {
            var stuff = GenStuff.RandomStuffFor(def);
            item = (Apparel)ThingMaker.MakeThing(def, stuff);
            item.stackCount = 1;
        }

        if (!p.apparel.Wearing(item))
        {
            p.apparel.Wear(item, false);
        }
    }

    public static int GetMoney(this Pawn pawn)
    {
        var money = pawn.inventory.innerContainer.FirstOrDefault(i => i.def == ThingDefOf.Silver);
        return money?.stackCount ?? 0;
    }

    public static bool IsIngestible(this Thing thing)
    {
        return thing.def.IsIngestible && thing.def.ingestible.preferability != FoodPreferability.RawBad && thing.def.ingestible.preferability != FoodPreferability.MealAwful;
    }

    public static bool IsFood(this Thing thing)
    {
        return thing.def.ingestible != null && thing.def.ingestible.preferability != FoodPreferability.NeverForNutrition && thing.def.ingestible.preferability != FoodPreferability.DesperateOnlyForHumanlikes
               && thing.def.ingestible.preferability != FoodPreferability.DesperateOnly;
    }

    /// <summary>
    ///     methodName = "CanWear", "CanEat" or "CanEquip"
    /// </summary>
    public static bool AlienFrameworkAllowsIt(ThingDef raceDef, ThingDef thingDef, [NotNull] string methodName)
    {
        if (!alienFrameworkMethods.TryGetValue(methodName, out var method))
        {
            var type = GenTypes.GetTypeInAnyAssembly("AlienRace.RaceRestrictionSettings");
            if (type != null)
            {
                method = type.GetMethod(methodName, [typeof(ThingDef), typeof(ThingDef)]);
                if (method == null) Log.Error($"Alien Framework does not have a method '{methodName}'.");
            }

            alienFrameworkMethods.Add(methodName, method); // we add it as null if not found, so it will return true
        }

        return method == null || (bool)method.Invoke(null, [thingDef, raceDef]);
    }

    public static bool IsBuyableAtAll(Pawn pawn, Thing thing)
    {
        if (thing.def.isUnfinishedThing) return false;

        if (thing.def == ThingDefOf.Silver) return false;

        if (!pawn.MayPurchaseThing(thing)) return false;

        if (thing.def.thingSetMakerTags != null && thing.def.thingSetMakerTags.Contains("NotForGuests")) return false;

        if (!IsBuyableNow(pawn, thing)) return false;
        //if (!thing.IsSociallyProper(pawn))
        //{
        //    Log.Message(thing.Label + ": is not proper for " + pawn.NameStringShort);
        //    return false;
        //}
        var cost = Mathf.CeilToInt(GetPurchasingCost(thing));

        if (cost > GetMoney(pawn))
        {
            return false;
        }

        if (BoughtByPlayer(pawn, thing))
        {
            return false;
        }

        //if (thing.IsInValidStorage()) Log.Message(thing.Label + " in storage ");
        return true;
    }

    public static float GetPurchasingCost([NotNull] this Thing thing)
    {
        if (IsFood(thing) && thing.GetMapComponent().guestsCanTakeFoodForFree) return 0;
        return thing.MarketValue * priceFactor;
    }

    private static bool BoughtByPlayer(Pawn pawn, Thing thing)
    {
        var lord = pawn.GetLord();
        return lord?.CurLordToil is not LordToil_VisitPoint toil || toil.BoughtOrSoldByPlayer(thing);
    }

    public static bool IsBuyableNow(Pawn pawn, Thing thing)
    {
        if (!thing.SpawnedOrAnyParentSpawned)
        {
            return false;
        }

        if (thing.ParentHolder is Pawn)
        {
            //Log.Message(thing.Label+": is inside pawn "+pawn.NameStringShort);
            return false;
        }

        if (thing.IsForbidden(Faction.OfPlayer))
        {
            //Log.Message(thing.Label+": is forbidden for "+pawn.NameStringShort);
            return false;
        }

        if (!pawn.HasReserved(thing) && !pawn.CanReserve(thing))
        {
            //Log.Message(thing.Label+": can't be reserved or reached by "+pawn.NameStringShort);
            return false;
        }

        if (pawn.GetInventorySpaceFor(thing) < 1)
        {
            return false;
        }

        return true;
    }

    public static bool MayPurchaseThing(this ITrader guestTrader, Thing thing)
    {
        if (thing == null || guestTrader == null) return false;
        if (thing.def.tradeability.PlayerCanSell()) return true;
        return guestTrader.IsGuestTrader() && thing.def.thingCategories?.Contains(DefDatabase<ThingCategoryDef>.GetNamed("FoodMeals")) == true;
    }

    public static Toil TakeFromPawn(Thing item, ThingOwner holder, int count = -1, TargetIndex indexToSet = TargetIndex.None)
    {
        var toil = ToilMaker.MakeToil();
        toil.initAction = delegate
        {
            if (!holder.Contains(item))
            {
                toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable);
            }
            else
            {
                count = count < 0 ? toil.actor.jobs.curJob.count : count;
                // PR #12: the holder may have eaten some while we approached. Cap to what
                // remains and what fits in the hands, without dropping/merging food on the map.
                count = Mathf.Min(count, Mathf.Min(item.stackCount, toil.actor.carryTracker.AvailableStackSpace(item.def)));
                // A whole-stack move would also move any default-layer eating reservation.
                // Partial takes leave that reserved original stack with its owner.
                if (count < 1 || (count >= item.stackCount && !toil.actor.CanReserve(item)))
                {
                    toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                holder.TryTransferToContainer(item, toil.actor.carryTracker.innerContainer, count, out var taken, false);
                if (taken == null)
                {
                    Log.Warning($"Taker {toil.actor.Label} unable to take count {count} of thing {item.Label} from holder's inventory");
                    toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
                else if (indexToSet != TargetIndex.None) // Respect the helper's optional target parameter.
                {
                    toil.actor.jobs.curJob.SetTarget(indexToSet, taken);
                }
            }
        };
        return toil;
    }

    public static Toil TakeToInventory(TargetIndex indItem)
    {
        var takeThing = ToilMaker.MakeToil();
        takeThing.initAction = delegate
        {
            var actor = takeThing.actor;
            var carried = actor.carryTracker.CarriedThing;
            // PR #12: hands -> inventory directly, so nearby ground stacks cannot merge into
            // the share. Check acceptance because TryAddItemNotForSale has no return value.
            if (carried != null && actor.inventory != null && actor.carryTracker.innerContainer.Remove(carried))
            {
                actor.inventory.TryAddItemNotForSale(carried);
                if (!actor.inventory.innerContainer.Contains(carried))
                {
                    // Preserve the food in the hands if another mod rejects inventory insertion.
                    actor.carryTracker.innerContainer.TryAdd(carried, false);
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                carried.def.soundPickup.PlayOneShot(new TargetInfo(actor.Position, actor.Map));
            }
            else
            {
                actor.jobs.EndCurrentJob(JobCondition.Errored);
            }
        };
        return takeThing;
    }

    #region Combat Extended integration

    public static int GetInventorySpaceFor(this Pawn pawn, Thing current)
    {
        if (pawn == null || current == null) return 0;

        var inventory = pawn.GetInventory();
        if (inventory == null) return current.stackCount;

        // Bind the optional API once per component; repeated item checks use a typed delegate.
        return InventoryCapacityCompatibility.GetSpace(inventory, current);
    }

    private static ThingComp GetInventory(this Pawn pawn)
    {
        // Avoid the predicate/enumerator path for this frequently repeated optional-comp probe.
        var comps = pawn.AllComps;
        for (var i = 0; i < comps.Count; i++)
            if (comps[i].GetType().Name == "CompInventory") return comps[i];
        return null;
    }

    #endregion
}
