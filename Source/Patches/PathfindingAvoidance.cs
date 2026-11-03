using HarmonyLib;
using Hospitality.Utilities;
using Verse;
using System.Reflection;

// Patch for the Pathfinding Avoidance mod.
// While guests are staying at the colony (not arriving/leaving), they should act more like colonists
// (rather than neutrals, which the mod tries to make avoid colony's rooms).
namespace Hospitality.Patches
{
	[HarmonyPatch]
	public class PathfindingAvoidance_Patch
	{
		private static MethodInfo method = AccessTools.Method("PathfindingAvoidance.Utility:ShouldAlsoTreatAsColonist");

		[HarmonyPrepare]
		static bool Prepare()
		{
			return method != null;
		}

		[HarmonyTargetMethod]
		private static MethodBase TargetMethod()
		{
			return method;
		}

		[HarmonyPostfix]
		public static bool ShouldAlsoTreatAsColonist(bool result, Pawn pawn)
		{
			return result || GuestUtility.IsArrivedGuest(pawn, out _);
		}
	}
}
