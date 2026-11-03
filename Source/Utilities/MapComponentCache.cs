using System;
using JetBrains.Annotations;
using Verse;

namespace Hospitality.Utilities;

public static class MapComponentCache
{
    [NotNull] private static Hospitality_MapComponent[] cachedComponents = new Hospitality_MapComponent[12];
    private static Game cachedGame;

    private static void EnsureCurrentGame()
    {
        // Static fields survive loading another save; discard components from the previous game.
        if (ReferenceEquals(cachedGame, Current.Game)) return;
        Array.Clear(cachedComponents, 0, cachedComponents.Length);
        cachedGame = Current.Game;
    }

    [CanBeNull]
    public static Hospitality_MapComponent GetMapComponent([CanBeNull] this Map map)
    {
        if (map == null) return null;
        return GetMapComponent(map, map.Index);
    }

    private static Hospitality_MapComponent GetMapComponent(Map map, int index)
    {
        if (map == null) return null;
        EnsureCurrentGame();
        if (index < 0) return null;
        if (index >= cachedComponents.Length) Array.Resize(ref cachedComponents, index + 6);

        var component = cachedComponents[index];
        // Removing a map can shift indices. Validate identity before reusing an indexed entry.
        if (component?.map != map)
        {
            component = map.GetComponent<Hospitality_MapComponent>();
            cachedComponents[index] = component;
        }
        return component;
    }

    [CanBeNull]
    public static Hospitality_MapComponent GetMapComponent([CanBeNull] this Thing thing)
    {
        if (thing == null || thing.mapIndexOrState < 0) return null;
        // Thing already stores its map index. Map.Index searches Find.Maps, so avoid that extra
        // search on the IsGuest/pathing/food hot path while retaining the shared identity checks.
        return GetMapComponent(thing.Map, thing.mapIndexOrState);
    }

    public static void Register([NotNull] Hospitality_MapComponent component)
    {
        EnsureCurrentGame();
        // Size for the actual slot being written, and do not index a map that is no longer active.
        var index = component.map.Index;
        if (index < 0) return;
        if (index >= cachedComponents.Length) Array.Resize(ref cachedComponents, index + 6);
        cachedComponents[index] = component;
    }
}
