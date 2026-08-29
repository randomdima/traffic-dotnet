using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Agents.Service;

/// <summary>
/// <b>How many of a town's buildings are police stations</b> (SRV-1), on the terms a hospital is placed
/// on. The placement itself is <see cref="ServicePlacement"/>; what is here is the share this slice's
/// figures ask for.
/// </summary>
internal static class PoliceStationRoster
{
    public static int CountIn(CityPlan plan, SimConfig config) =>
        BuildingRoster.CountIn(plan, config.Service.StationsPerBuilding, config.Service.MostStations);

    public static ServiceApron Apron(CityPlan plan, SimConfig config) =>
        new(BuildingUse.PoliceStation, CountIn(plan, config), config.ServiceHomeM);
}

/// <summary><b>And how many are depots</b> (SRV-1) — the yard an evacuator waits in.</summary>
internal static class DepotRoster
{
    public static int CountIn(CityPlan plan, SimConfig config) =>
        BuildingRoster.CountIn(plan, config.Service.DepotsPerBuilding, config.Service.MostDepots);

    public static ServiceApron Apron(CityPlan plan, SimConfig config) =>
        new(BuildingUse.Depot, CountIn(plan, config), config.ServiceHomeM);
}
