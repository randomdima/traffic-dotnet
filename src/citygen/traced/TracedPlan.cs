using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A real place, laid from its survey</b> (GEN-57): the roads OSM holds for it as the town's roads, its sea
/// as the town's water, at one metre to the metre — and, where its map says, each road as wide as it was
/// measured, lights where it is signalled, zebras where they are painted, its bridges, roundabouts, trees, and its
/// buildings as the footprints it surveyed. No car park, no building stood on them, and nobody standing in it but a
/// few cars at its bridges over its roads (<see cref="TracedBridgeCars"/>).
/// </summary>
/// <remarks>
/// <b>Content and not code</b>, the same as a brief: a traced map is a file in <c>towns/traced/</c> that a build
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
        var crosswalks = TracedCrossings.Lay(survey, streets, config);
        var crossingsMs = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        var trees = TracedTrees.Lay(survey, streets.Roads, streets.Junctions, config);
        var treesMs = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        var water = TracedSea.Lay(survey, config);
        var seaMs = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        var cars = TracedBridgeCars.Lay(streets.Roads, config);
        var carsMs = clock.Elapsed.TotalMilliseconds;

        // The pavement is laid here and handed over rather than laid again when the town is opened.
        clock.Restart();
        var corners = new CityPlan.JunctionCornerArrays { CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [] };
        var lots = new CityPlan.ParkingLotArrays
        {
            CentreM = [], Axis = [], HalfExtentM = [], SpaceOffsets = [0], SpacePositionM = [], SpaceHeadingRad = [],
        };
        var paving = Paving.Lay(
            new GroundPieces(
                (ulong)survey.Relation, new Vector2(survey.WidthM, survey.HeightM), config.PavementWidthM, streets.Roads,
                streets.Bridges, streets.Junctions, corners, streets.Roundabouts, lots, CityPlan.PavedAreaArrays.None, crosswalks,
                water),
            config);
        var pavingMs = clock.Elapsed.TotalMilliseconds;

        return new CityPlan
        {
            // The relation the survey was taken inside is the map's own number, and a seed is what a plan
            // keys every draw made off it on; a traced town draws nothing of its own.
            Seed = (ulong)survey.Relation,
            Name = survey.Name,
            WorldSizeM = new Vector2(survey.WidthM, survey.HeightM),
            PavementWidthM = config.PavementWidthM,
            Junctions = streets.Junctions,
            JunctionCorners = corners,
            Roads = streets.Roads,
            Bridges = streets.Bridges,
            Roundabouts = streets.Roundabouts,
            PavedAreas = CityPlan.PavedAreaArrays.None,
            Crosswalks = crosswalks,
            ZebraAtEveryStation = false,
            ParkingLots = lots,
            Buildings = CityPlan.BuildingArrays.None,
            Footprints = survey.Footprints,
            Props = trees,
            Spawns = cars,
            Water = water,
            PavingLaidWithIt = paving,
            LaidMs =
            [
                ("streets", streetsMs), ("crossings", crossingsMs), ("trees", treesMs), ("sea", seaMs), ("cars", carsMs),
                ("paving", pavingMs),
            ],
        };
    }
}
