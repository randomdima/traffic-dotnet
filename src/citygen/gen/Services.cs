using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Which of a town's buildings serve which use</b> (GEN-9): the hospital, the police stations and the
/// depots, decided while the town is laid and carried on the buildings themselves.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two things decide a place and both are known here.</b> A service has to have somewhere for its
/// vehicles to stand, so only a building with a car park within a block of it is eligible; and the services
/// have to be spread over the town rather than dropped into it, so each one goes as far from every service
/// already placed as the eligible buildings allow.
/// </para>
/// <para>
/// <b>It is a sweep and not a shuffle</b>, which is what the placement could never be while it had to be
/// paid for every time a map was opened: a town is laid once, when it is opened, and a pass over the
/// buildings for each of a handful of services is nothing beside the ground that was just painted.
/// </para>
/// </remarks>
internal static class Services
{
    public static BuildingUse[] Decide(
        List<Vector2> buildingM, List<Vector2> lotM, TownBrief brief, SimConfig config)
    {
        var uses = new BuildingUse[buildingM.Count];
        if (buildingM.Count == 0) return uses;

        var withinM = config.CityGen.BlockSpacingAlongMinM;
        var eligible = new List<int>();
        for (var building = 0; building < buildingM.Count; building++)
        {
            if (Nearest(lotM, buildingM[building]) <= withinM) eligible.Add(building);
        }

        if (eligible.Count == 0) return uses;

        var placedM = new List<Vector2>();
        Place(uses, eligible, placedM, buildingM, BuildingUse.Hospital, brief.Hospitals);
        Place(uses, eligible, placedM, buildingM, BuildingUse.PoliceStation, brief.PoliceStations);
        Place(uses, eligible, placedM, buildingM, BuildingUse.Depot, brief.Depots);
        return uses;
    }

    /// <summary>
    /// Each place in turn, as far from the places already taken as the town allows. <b>The first is the one
    /// nearest the middle</b> — with nothing placed there is no distance to maximise, and a town's first
    /// hospital standing on its edge is a town that drives past it to reach it.
    /// </summary>
    static void Place(
        BuildingUse[] uses, List<int> eligible, List<Vector2> placedM, List<Vector2> buildingM,
        BuildingUse use, int wanted)
    {
        var centreM = Middle(buildingM);
        for (var placing = 0; placing < wanted; placing++)
        {
            var best = -1;
            var bestM = float.NegativeInfinity;
            foreach (var building in eligible)
            {
                if (uses[building] != BuildingUse.Ordinary) continue;

                var awayM = placedM.Count == 0
                    ? -(buildingM[building] - centreM).Length()
                    : Nearest(placedM, buildingM[building]);
                if (awayM <= bestM) continue;

                bestM = awayM;
                best = building;
            }

            if (best < 0) return;

            uses[best] = use;
            placedM.Add(buildingM[best]);
        }
    }

    static Vector2 Middle(List<Vector2> pointsM)
    {
        var sumM = Vector2.Zero;
        foreach (var pointM in pointsM) sumM += pointM;
        return sumM / MathF.Max(1, pointsM.Count);
    }

    static float Nearest(List<Vector2> pointsM, Vector2 ofM)
    {
        var nearestM = float.PositiveInfinity;
        foreach (var pointM in pointsM) nearestM = MathF.Min(nearestM, (pointM - ofM).Length());
        return nearestM;
    }
}
