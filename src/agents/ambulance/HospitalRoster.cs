using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Agents.Ambulance;

/// <summary>
/// <b>How many of a town's buildings are hospitals</b> (AMB-1) — the places a casualty is delivered to
/// and the places an ambulance waits near. Which ones they are is the map's own answer, read back
/// through <see cref="BuildingRoster"/>; what is here is the share this slice's figures ask for when a
/// map is authored.
/// </summary>
/// <remarks>
/// <b>Hospitals are placed first</b>, so a use added beside them cannot move which buildings they are.
/// </remarks>
internal static class HospitalRoster
{
    public static int CountIn(CityPlan plan, SimConfig config) =>
        BuildingRoster.CountIn(plan, config.Ambulance.HospitalsPerBuilding, config.Ambulance.MostHospitals);

    /// <summary>What a map's author is asked to place: this many, each with an apron's worth of bays near it.</summary>
    public static ServiceApron Apron(CityPlan plan, SimConfig config) =>
        new(BuildingUse.Hospital, CountIn(plan, config), config.AmbulanceHomeM);
}
