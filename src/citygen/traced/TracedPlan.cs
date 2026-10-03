using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A real place, laid from its survey</b> (GEN-57): the roads OSM holds for it as the town's roads, its sea
/// as the town's water, at one metre to the metre — and nothing else. No building, no car park, no prop, no
/// light and nobody standing in it.
/// </summary>
/// <remarks>
/// <b>Content and not code</b>, the same as a brief: a survey is a file in <c>towns/traced/</c> that a build
/// may ship any number of, and from the plan onward a traced town is a town like any other (GEN-1a). What
/// differs is only where its streets came from.
/// </remarks>
internal static class TracedPlan
{
    public static CityPlan Lay(Survey survey, SimConfig config)
    {
        var clock = Stopwatch.StartNew();
        var streets = TracedStreets.Lay(survey, config);
        var streetsMs = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        var water = TracedSea.Lay(survey, config);
        var seaMs = clock.Elapsed.TotalMilliseconds;

        return new CityPlan
        {
            // The relation the survey was taken inside is the map's own number, and a seed is what a plan
            // keys every draw made off it on; a traced town draws nothing of its own.
            Seed = (ulong)survey.Relation,
            Name = survey.Name,
            WorldSizeM = new Vector2(survey.WidthM, survey.HeightM),
            PavementWidthM = config.PavementWidthM,
            Junctions = streets.Junctions,
            JunctionCorners = new CityPlan.JunctionCornerArrays
            {
                CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [],
            },
            Roads = streets.Roads,
            Bridges = new CityPlan.BridgeArrays
            {
                Road = [], FromM = [], ToM = [], DeckWidthM = [], PavementWidthM = [],
            },
            PavedAreas = CityPlan.PavedAreaArrays.None,
            Crosswalks = new CityPlan.CrosswalkArrays { CentreM = [], Axis = [], DepthM = [], Road = [], Junction = [] },
            ParkingLots = new CityPlan.ParkingLotArrays
            {
                CentreM = [], Axis = [], HalfExtentM = [], SpaceOffsets = [0], SpacePositionM = [], SpaceHeadingRad = [],
            },
            Buildings = CityPlan.BuildingArrays.None,
            Props = new CityPlan.PropArrays { CentreM = [], RadiusM = [], BearingRad = [], Kind = [] },
            Spawns = new CityPlan.SpawnArrays { Kind = [], PositionM = [], HeadingRad = [] },
            Water = water,
            LaidMs = [("streets", streetsMs), ("sea", seaMs)],
        };
    }
}
