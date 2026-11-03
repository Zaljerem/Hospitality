using Verse;

namespace Hospitality.Utilities;

internal static class SpaceMapUtility
{
    internal static bool IsSpaceMap(Map map)
    {
        // SOS2 uses a named biome; Odyssey orbital sites may use other biomes.
        // Match Spaceports' orbital-layer rule without depending on its assembly.
        return map != null && (map.Biome?.defName is "Space" or "OuterSpaceBiome" ||
            (ModLister.OdysseyInstalled && !map.TileInfo.OnSurface));
    }
}
