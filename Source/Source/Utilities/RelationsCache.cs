using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Waylith.Math;
using Verse;

namespace Hospitality.Utilities
{
	public class RelationsCache
	{
		private Map Map { get; }
		[NotNull]
		public readonly List<Pawn> colonistsFromBase = new();
		private readonly Dictionary<Pawn, RelationsCacheSet> allCacheSets = new();

		private int lastUpdateTick;

		public int friendsRequired;

		public RelationsCache(Map map)
		{
			Map = map;
		}

		[NotNull]
		public RelationsCacheSet GetSetFor(Pawn pawn)
		{
			// General stats
			 TryUpdate();

			// Specific stats
			if (!allCacheSets.TryGetValue(pawn, out var set)) allCacheSets.Add(pawn, set = new RelationsCacheSet(pawn));

			set.TryUpdate();
			return set;
		}

        public RelationsCacheSet RefreshFor(Pawn pawn)
        {
            // Explicit interaction callers need fresh before/after totals within the same tick.
            // Leave ordinary UI queries throttled and refresh only this guest's relationship set.
            TryUpdate();
            if (!allCacheSets.TryGetValue(pawn, out var set)) allCacheSets.Add(pawn, set = new RelationsCacheSet(pawn));
            set.TryUpdate(true);
            return set;
        }

        public void ForceImmediateUpdate()
        {
            // Bypass the throttle explicitly; tick zero is not an expired cache timestamp.
            TryUpdate(true);

            foreach (var pawn in Map.mapPawns.AllPawns)
            {
                if (pawn.IsGuest() && pawn.CompGuest() != null)
                {
                    if (!allCacheSets.TryGetValue(pawn, out var set)) allCacheSets.Add(pawn, set = new RelationsCacheSet(pawn));
                    set.TryUpdate(true); // Existing per-guest entries must also bypass their throttle.
                }
            }
        }

        private void TryUpdate(bool force = false)
		{
			if (!force && Current.Game.tickManager.TicksGame <= lastUpdateTick + 50) return;
			lastUpdateTick = Current.Game.tickManager.TicksGame;

			// Calculate the requirement from this refresh, rather than the previous colonist list.
			UpdateColonistsFromBase();
			friendsRequired = GetFriendsRequired();
		}      

        private void UpdateColonistsFromBase()
		{
			colonistsFromBase.Clear();
			colonistsFromBase.AddRange(Map.mapPawns.FreeColonists.Where(c => !c.IsSlave));
			colonistsFromBase.AddRange( GetNearbyColonists(Map).Where(c=>!c.IsSlave));
		}
		
		private static IEnumerable<Pawn> GetNearbyColonists(Map mapHeld)
		{
			return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Where(p => IsNearby(mapHeld, p));
		}

		private static bool IsNearby(Map mapHeld, Pawn p)
		{
			if (p.Spawned && p.MapHeld.IsPlayerHome) return false;
			var tile = p.GetRootTile();
			if (tile == -1) return false;

			return Find.WorldGrid.ApproxDistanceInTiles(mapHeld.Tile, tile) < 8; // Within 8 tiles counts; retain the existing gameplay distance.
		}

		private int GetFriendsRequired()
		{
			var x = colonistsFromBase.Count();
			if (x <= 3) return 1;
			// Formula from: https://mycurvefit.com/share/5b359026-5f44-4ac4-88ed-9b364a242f7b
			var a = 0.887f;
			var b = 0.646f;
			var y = a * Mathf.Pow(x, b);
			var required = y;
			return Mathf.RoundToInt(required);

		}

		public class RelationsCacheSet
		{
			private int lastUpdateTick;

			private Pawn Pawn { get; }
			public int friends;
			public int friendsSeniority;
			public int enemies;
			public int enemiesSeniority;
            
			public RelationsCacheSet(Pawn pawn)
			{
				Pawn = pawn;
			}

			public void TryUpdate(bool force = false)
			{
				// Normal updates remain staggered; load-time refreshes can explicitly bypass this delay.
				if (!force && Current.Game.tickManager.TicksGame <= lastUpdateTick + 50 + Pawn.HashOffset() % 20) return;
				lastUpdateTick = Current.Game.tickManager.TicksGame;

				var mapComponent = Pawn.GetMapComponent();
				if (mapComponent == null) return;

				float requiredOpinion = Pawn.GetMinRecruitOpinion();
				friends = friendsSeniority = enemies = enemiesSeniority = 0;
				// One pass replaces four LINQ scans: knowledge and opinion are evaluated once per
				// colonist, and relatedness once for a qualifying colonist. Keep both tests independent
				// in case another mod makes the friend/enemy thresholds overlap.
				foreach (var colonist in mapComponent.RelationsCache.colonistsFromBase)
				{
					if (!RelationsUtility.PawnsKnowEachOther(Pawn, colonist)) continue;
					var opinion = Pawn.relations.OpinionOf(colonist);
					var isEnemy = opinion <= GuestUtility.MaxOpinionForEnemy;
					var isFriend = opinion >= requiredOpinion;
					if (!isEnemy && !isFriend) continue;

					var weight = GetRelationValue(colonist, Pawn);
					var title = colonist.royalty?.MostSeniorTitle;
					var seniority = title != null ? title.def.seniority + 100 : 0; // Seniority can be zero.
					checked // Preserve Enumerable.Sum's overflow checking for accumulated totals.
					{
						if (isEnemy)
						{
							enemies += weight;
							enemiesSeniority += seniority;
						}
						if (isFriend)
						{
							friends += weight;
							friendsSeniority += seniority;
						}
					}
				}
			}         

            private static int GetRelationValue(Pawn pawn, Pawn guest)
			{
				return GuestUtility.IsRelated(pawn, guest) ? 2 : 1;
			}
		}
	}
}
