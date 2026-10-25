using TrafficSimulation.CityGen;

namespace TrafficSimulation.World.Statics;

/// <summary>
/// <b>The art this build ships, as the sizes a town is laid at</b> (GEN-54, GEN-6e): the roofs' footprints
/// (<see cref="BuildingCatalog.Roofs"/>) and the props' looks (<see cref="PropCatalog.Sizes"/>), measured once.
/// </summary>
/// <remarks>
/// Every caller that opens a map hands the generator this same reading, so what a town is built of is the art the
/// renderer draws it in, and the plan below never learns that a catalogue exists.
/// </remarks>
internal static class ShippedArt
{
    public static ArtSizes Sizes { get; } = new(BuildingCatalog.Roofs, PropCatalog.Shared.Sizes());
}
