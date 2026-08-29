using TrafficSimulation.CityGen;

namespace TrafficSimulation.World.Statics;

/// <summary>
/// <b>The three rosters read as one question</b> — what is this building for. The answer is the map's
/// own (<see cref="BuildingUse"/>); what is here is the roster of each use, which is what stands a
/// vehicle at one and what a call is delivered to.
/// </summary>
/// <remarks>
/// Laid once when a map is opened and read at the two places a use is visible: standing the vehicles,
/// and which roof the building wears (AMB-1a, SRV-1a). <b>A building serves one use at most</b> (SRV-1),
/// which is a fact about the file rather than an order the draws are taken in: one byte a building
/// cannot say two things.
/// </remarks>
internal sealed class BuildingUses
{
    readonly BuildingUse[] _of;

    BuildingUses(BuildingUse[] of, BuildingRoster hospitals, BuildingRoster policeStations, BuildingRoster depots)
    {
        _of = of;
        Hospitals = hospitals;
        PoliceStations = policeStations;
        Depots = depots;
    }

    /// <summary>What this map declares: one pass over the buildings, at the moment the plan is stood up.</summary>
    public static BuildingUses Of(CityPlan plan) => new(
        plan.Buildings.Use,
        BuildingRoster.Of(plan, BuildingUse.Hospital),
        BuildingRoster.Of(plan, BuildingUse.PoliceStation),
        BuildingRoster.Of(plan, BuildingUse.Depot));

    public BuildingRoster Hospitals { get; }

    public BuildingRoster PoliceStations { get; }

    public BuildingRoster Depots { get; }

    public BuildingUse Of(int building) => _of[building];
}
