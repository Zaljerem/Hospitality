using Hospitality.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

// need Waylith equivalents - UPDATE

namespace Hospitality.MainTab;

internal static class MainTabUtility
{
    public static void DrawLordGroups(Rect rect, PawnTable table, Pawn firstPawn)
    {
        if (table.SortingBy != null) return;
        if (firstPawn == null) return;
        if (!firstPawn.SpawnedOrAnyParentSpawned) return;

        var lords = firstPawn.Map.GetMapComponent().PresentLords;

        var index = -table.PawnsListForReading.IndexOf(firstPawn);
        var yTop = rect.yMin;
        var cellHeight = rect.height;

        foreach (var lord in lords)
        {
            if (lord == null) continue;

            const int verticalMargin = 6;
            const float width = 4;

            var box = rect;
            box.xMin = box.xMax - width;
            box.yMin = yTop + index * cellHeight + verticalMargin;
            box.yMax = box.yMin + lord.ownedPawns.Count * cellHeight - verticalMargin * 2;
            if (lord.faction != null) Widgets.DrawBoxSolid(box, lord.faction.Color);
            index += lord.ownedPawns.Count;
        }
    }
}