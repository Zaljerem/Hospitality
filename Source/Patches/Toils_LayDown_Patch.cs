using HarmonyLib;
using Hospitality.Utilities;
using RimWorld;
using Verse;
using BedUtility = Hospitality.Utilities.BedUtility;

namespace Hospitality.Patches;

public class Toils_LayDown_Patch
{
    /// <summary>
    ///     So guests can think about their bedroom
    /// </summary>
    [HarmonyPatch(typeof(Toils_LayDown), nameof(Toils_LayDown.ApplyBedThoughts))]
    public class ApplyBedThoughts
    {
        [HarmonyPrefix]
        public static bool Replacement(Pawn actor)
        {
            if (actor.needs.mood == null) return false;

            var bed = actor.CurrentBed(); // ADDED to make sure we also get guest's beds
            actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInBedroom);
            actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInBarracks);
            actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptOutside);
            actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptOnGround);
            actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInCold);
            actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInHeat);

            var ambientTemperature = actor.AmbientTemperature;
            var comfyTemperatureMin = actor.def.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin);
            var comfyTemperatureMax = actor.def.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax);
            if (ModsConfig.BiotechActive && actor.genes != null)
            {
                foreach (var item in actor.genes.GenesListForReading)
                {
                    if (!item.Active) continue;
                    comfyTemperatureMin += item.def.statOffsets.GetStatOffsetFromList(StatDefOf.ComfyTemperatureMin);
                    comfyTemperatureMax += item.def.statOffsets.GetStatOffsetFromList(StatDefOf.ComfyTemperatureMax);
                }
            }

            if (ambientTemperature < comfyTemperatureMin)
            {
                actor.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.SleptInCold);
            }

            if (ambientTemperature > comfyTemperatureMax)
            {
                actor.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.SleptInHeat);
            }

            if (!actor.IsWildMan())
            {
                if (actor.GetRoom().PsychologicallyOutdoors)
                {
                    actor.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.SleptOutside);
                }

                if (bed == null || bed.CostListAdjusted().Count == 0)
                {
                    actor.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.SleptOnGround);
                }
            }

            if (bed != null && AddedBedIsOwned(actor, bed) && !bed.ForPrisoners && !actor.story.traits.HasTrait(TraitDefOf.Ascetic))
            {
                ThoughtDef thoughtDef = null;
                // ADDED:
                if (bed.GetRoom().Role == InternalDefOf.GuestRoom)
                {
                    thoughtDef = bed.GetRoom().OnlyOneBed() ? ThoughtDefOf.SleptInBedroom : ThoughtDefOf.SleptInBarracks;
                } ////
                else if (bed.GetRoom().Role == RoomRoleDefOf.Bedroom)
                {
                    thoughtDef = ThoughtDefOf.SleptInBedroom;
                }
                else if (bed.GetRoom().Role == RoomRoleDefOf.Barracks)
                {
                    thoughtDef = ThoughtDefOf.SleptInBarracks;
                }

                if (thoughtDef != null)
                {
                    var scoreStageIndex = RoomStatDefOf.Impressiveness.GetScoreStageIndex(bed.GetRoom().GetStat(RoomStatDefOf.Impressiveness));
                    if (thoughtDef.stages[scoreStageIndex] != null)
                    {
                        actor.needs.mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(thoughtDef, scoreStageIndex));
                    }
                }
            }

            actor.Notify_AddBedThoughts();

            return false;
        }

        // ADDED
        private static bool AddedBedIsOwned(Pawn pawn, Building_Bed buildingBed)
        {
            return pawn.IsArrivedGuest(out _)
                ? BedUtility.GetGuestBed(pawn) == buildingBed
                : buildingBed == pawn.ownership.OwnedBed;
        }
    }
}