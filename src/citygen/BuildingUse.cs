using System.Numerics;
using TrafficSimulation.Core.Config;

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

    /// <summary>A repair shop: where its evacuators wait, and the yard they bring a wreck back to (SRV-1, EVA-2).</summary>
    Depot = 3,
}

/// <summary>
/// <b>The service buildings a town is laid with</b> (GEN-56): each use serves a catchment of the districts its streets stand
/// in (<see cref="DistrictMap.Built"/>), so a town has as many of each as its catchments — a hospital to many districts, a
/// police station to a few.
/// </summary>
/// <remarks>
/// <b>Which districts stand one is spread over the town</b>: the one nearest the middle of the built districts first, then
/// each time the built district furthest from every one already chosen — so the catchments are about one size and every
/// district is near one, without the districts being grouped first.
/// </remarks>
internal static class ServiceBuildings
{
    /// <summary>The uses a town stands, in the order their yards are cut.</summary>
    public static ReadOnlySpan<BuildingUse> Uses => [BuildingUse.Hospital, BuildingUse.PoliceStation, BuildingUse.Depot];

    /// <summary>How many districts one building of a use serves.</summary>
    public static int CatchmentOf(BuildingUse use, CityGenFigures figures) => use switch
    {
        BuildingUse.Hospital => figures.DistrictsPerHospital,
        BuildingUse.PoliceStation => figures.DistrictsPerPoliceStation,
        _ => figures.DistrictsPerDepot,
    };

    /// <summary>
    /// <b>The districts that stand a building of one use</b>, in the order chosen: one for each catchment of the built
    /// districts, and none where the town plans no building.
    /// </summary>
    public static List<int> Serving(BuildingUse use, int buildings, DistrictMap districts, CityGenFigures figures)
    {
        var chosen = new List<int>();
        if (buildings <= 0 || districts.BuiltCount == 0) return chosen;

        var built = Enumerable.Range(0, districts.Count).Where(districts.Built).ToList();
        var middleM = Vector2.Zero;
        foreach (var district in built) middleM += districts.CentreM[district];
        middleM /= built.Count;

        var wanted = (int)MathF.Ceiling(built.Count / (float)Math.Max(1, CatchmentOf(use, figures)));
        var nearestM = new float[districts.Count];
        Array.Fill(nearestM, float.PositiveInfinity);
        var next = built.MinBy(district => Vector2.DistanceSquared(districts.CentreM[district], middleM));
        while (chosen.Count < wanted)
        {
            chosen.Add(next);
            foreach (var district in built) nearestM[district] = MathF.Min(nearestM[district], Vector2.Distance(districts.CentreM[district], districts.CentreM[next]));
            next = built.MaxBy(district => nearestM[district]);
        }

        return chosen;
    }

    public static List<int> Serving(BuildingUse use, CityPlan plan, CityGenFigures figures) =>
        Serving(use, plan.Buildings.Count, plan.Districts, figures);
}
