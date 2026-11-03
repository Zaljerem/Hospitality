using Verse;

namespace Hospitality.Utilities;

[StaticConstructorOnStartup]
internal static class ScroungeReservationLayer
{
    internal static readonly ReservationLayerDef Def = FindOrCreate();

    private static ReservationLayerDef FindOrCreate()
    {
        // PR #12 supplies this definition in versioned XML. This workspace ships source/DLLs
        // without the full mod assets, so also register it before saves load when XML is absent.
        // Reuse an installed XML definition to avoid duplicate defs when packaged upstream.
        var existing = DefDatabase<ReservationLayerDef>.GetNamedSilentFail("Scrounge");
        if (existing != null) return existing;
        var layer = new ReservationLayerDef { defName = "Scrounge" };
        DefDatabase<ReservationLayerDef>.Add(layer);
        return layer;
    }
}
