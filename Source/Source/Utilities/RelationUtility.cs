using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using RimWorld;
using Verse;

namespace Hospitality.Utilities;

internal static class RelationUtility
{
    // Pawn IDs are reused across saves. Key by the actual pawn without retaining discarded pawns.
    private static readonly ConditionalWeakTable<Pawn, RelationInfo> relationCache = new();
    private static Game cachedGame;

    internal static void Reset()
    {
        relationCache.Clear();
        cachedGame = Current.Game;
    }

    public static RelationInfo GetRelationInfo(Pawn pawn)
    {
        // Static table helpers outlive game instances, including loading an earlier save.
        if (!ReferenceEquals(cachedGame, Current.Game))
        {
            Reset();
        }
        if (!relationCache.TryGetValue(pawn, out var info)) return CreateRelationInfo(pawn);

        // Retain the existing interval boundary; a backward tick movement needs a fresh result.
        var elapsed = GenTicks.TicksGame - info.lastUpdateTick;
        if (elapsed < 0 || elapsed > GenTicks.TickLongInterval) UpdateInfo(info, pawn);

        return info;
    }

    private static RelationInfo CreateRelationInfo(Pawn guest)
    {
        var info = new RelationInfo();
        UpdateInfo(info, guest);
        relationCache.Add(guest, info);
        return info;
    }

    private static void UpdateInfo(RelationInfo info, Pawn guest)
    {
        // Format in source order without per-colonist projection iterators or a temporary array
        // of relation structs. Keep the flag independent of translated tooltip text length.
        var hasRelationship = false;
        var sb = new StringBuilder();
        foreach (var colonist in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
        {
            if (colonist.relations == null) continue;
            foreach (var relationDef in colonist.GetRelations(guest))
            {
                hasRelationship = true;
                sb.AppendLine("Relationship".Translate(relationDef.GetGenderSpecificLabelCap(guest), colonist.Label, colonist));
            }
        }

        info.tooltip = sb.ToString();
        info.hasRelationship = hasRelationship;
        info.lastUpdateTick = GenTicks.TicksGame;
    }

    public class RelationInfo
    {
        public bool hasRelationship;
        public int lastUpdateTick;
        public string tooltip;
    }

}
