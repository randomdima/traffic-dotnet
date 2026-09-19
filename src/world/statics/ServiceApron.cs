using TrafficSimulation.CityGen;

namespace TrafficSimulation.World.Statics;

/// <summary>How many buildings one service wants, and how far from its own door its vehicles may stand.</summary>
/// <remarks>
/// <b>It is what a roster asks for and not what a map has</b> (GEN-4k, GEN-8): the count is the share the
/// engine keeps of the buildings a map plans (<see cref="BuildingRoster.CountIn"/>), and a town whose ground
/// could not carry a yard for every one of them stands fewer.
/// </remarks>
internal readonly record struct ServiceApron(BuildingUse Use, int Wanted, float WithinM);
