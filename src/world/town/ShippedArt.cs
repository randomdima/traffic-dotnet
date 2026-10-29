using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The art this build ships, as the sizes a town is laid at</b> (GEN-54, GEN-6e): the roofs' footprints
/// (<see cref="BuildingCatalog.Roofs"/>), the props' looks (<see cref="PropCatalog.Sizes"/>) and how many person looks
/// there are (<see cref="PersonCatalog.SheetCount"/>), measured once.
/// </summary>
/// <remarks>
/// Every caller that opens a map hands the generator this same reading, so what a town is built of is the art the
/// renderer draws it in, and the plan below never learns that a catalogue exists. <b>It is the composition's</b>
/// because the person looks are an agent's catalogue, which nothing in <c>world/</c> below the town may read.
/// </remarks>
internal static class ShippedArt
{
    public static ArtSizes Sizes { get; } =
        new(BuildingCatalog.Roofs, PropCatalog.Shared.Sizes(), PersonCatalog.Shared.SheetCount);
}
