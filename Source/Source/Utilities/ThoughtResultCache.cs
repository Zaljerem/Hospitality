using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace Hospitality.Utilities
{
    /// <summary>
    /// This cache utility saves all results of <see cref="ThoughtWorkerCached"/> and manages whether it needs an update or not
    /// </summary>
    public static class ThoughtResultCache
    {
        // A hash alone cannot identify a pawn/worker pair: collisions used to share results.
        // Weak pawn keys avoid retaining discarded pawns, and each worker uses reference identity.
        // Keep state and tick together so they cannot diverge and need one dictionary lookup.
        private static readonly ConditionalWeakTable<Pawn, Dictionary<ThoughtWorkerCached, Entry>> cachedStates = new();
        private static Game cachedGame;

        private readonly struct Entry
        {
            internal readonly ThoughtState State;
            internal readonly int Tick;
            internal Entry(ThoughtState state, int tick) { State = state; Tick = tick; }
        }

        private static void EnsureCurrentGame()
        {
            // Static state also survives starting a new game, not just the LoadGame reset hook.
            if (!ReferenceEquals(cachedGame, Current.Game)) Reset();
        }

        public static void Reset()
        {
            cachedStates.Clear();
            cachedGame = Current.Game;
        }
        public static void CacheThoughtResult(Pawn forPawn, ThoughtWorkerCached worker, ThoughtState result)
        {
            EnsureCurrentGame();
            var states = cachedStates.GetValue(forPawn, _ => new Dictionary<ThoughtWorkerCached, Entry>(ReferenceEqualityComparer.Instance));
            states[worker] = new Entry(result, Find.TickManager.TicksGame);
        }

        //If the time between current tick and last cache tick is greater than the interval defined in the worker, we request a new state
        internal static bool NeedsNewState(Pawn forPawn, ThoughtWorkerCached worker, out ThoughtState existingState)
        {
            EnsureCurrentGame();
            int curTick = Find.TickManager.TicksGame;

            existingState = false;
            if (cachedStates.TryGetValue(forPawn, out var states) && states.TryGetValue(worker, out var entry))
            {
                existingState = entry.State;
                var elapsed = curTick - entry.Tick;
                // Preserve the strict interval boundary, but refresh after a tick rollback.
                return elapsed < 0 || elapsed > worker.ThoughtCacheInterval;
            }
            return true;
        }
    }
}
