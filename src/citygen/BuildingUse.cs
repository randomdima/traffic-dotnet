namespace TrafficSimulation.CityGen;

/// <summary>
/// What a building is for (AMB-1, SRV-1). <b>It is the map's own answer and not a run's</b>: which building is the hospital is
/// authored beside where that building stands, so it is the same every time the map is opened and moves
/// only when the map does.
/// </summary>
/// <remarks>
/// <b>It is the plan's vocabulary and lives with the plan</b>, as <see cref="Ground"/> does. What each use
/// <em>stands</em> — an apron of ambulances, a yard of wrecks — is <c>World.Statics.BuildingUses</c>'s and
/// the agents' above that.
/// </remarks>
internal enum BuildingUse : byte
{
    /// <summary>Somewhere to walk to and dwell in, which is what nearly every building is.</summary>
    Ordinary = 0,

    /// <summary>A hospital: where a casualty is delivered and where its ambulances stand (AMB-1).</summary>
    Hospital = 1,

    /// <summary>A police station: where its patrol cars stand between beats (SRV-1).</summary>
    PoliceStation = 2,

    /// <summary>A repair shop: where the evacuator waits, and the yard it brings a wreck back to (SRV-1, EVA-2).</summary>
    Depot = 3,
}
