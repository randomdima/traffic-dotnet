using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A real place, laid from its survey</b> (GEN-57): the roads OSM holds for it as the town's roads, its sea
/// as the town's water, at one metre to the metre — and, where its map says, each road as wide as it was
/// measured, lights where it is signalled, its bridges, roundabouts, and its buildings as the prefabs their
/// footprints are worn as (<see cref="TracedBuildings"/>). <b>Its zebras and props are the town's own</b>: a zebra at
/// every station its kerb ends cut, as a generated town paints, and its props as a generated town lays them — those
/// beyond its verges as scenery, drawn and standing nothing (<see cref="TracedProps"/>). No car park; a few cars at its
/// bridges over its roads (<see cref="TracedBridgeCars"/>), and the people and cars its map asks for, one a door and
/// one a lane (<see cref="TracedSpawns"/>).
/// </summary>
/// <remarks>
/// <b>Content and not code</b>, the same as a brief: a traced map is a file in <c>towns/traced/</c> that a build
/// may ship any number of, and from the plan onward a traced town is a town like any other (GEN-1a). What
/// differs is only where its streets came from.
/// </remarks>
internal static class TracedPlan
{
    /// <param name="sizes">
    /// The prefabs its footprints are fitted from, handed down from the catalogue that read them
    /// (<see cref="BuildingSizes"/>). A town handed none stands each rectangle of a footprint as itself.
    /// </param>
    public static CityPlan Lay(Survey survey, SimConfig config, BuildingSizes sizes)
    {
        var clock = Stopwatch.StartNew();
        var streets = TracedStreets.Lay(survey, config);
        var streetsMs = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        var water = TracedSea.Lay(survey, config);
        var seaMs = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        var cars = TracedBridgeCars.Lay(streets.Roads, config);
        var carsMs = clock.Elapsed.TotalMilliseconds;

        // <b>The buildings stand against the walk the finished town is laid with</b> (GEN-54), so the pavement is laid
        // here and handed over rather than laid again when the town is opened.
        clock.Restart();
        var corners = new CityPlan.JunctionCornerArrays { CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [] };
        var lots = new CityPlan.ParkingLotArrays
        {
            CentreM = [], Axis = [], HalfExtentM = [], SpaceOffsets = [0], SpacePositionM = [], SpaceHeadingRad = [],
        };
        var crosswalks = new CityPlan.CrosswalkArrays { CentreM = [], Axis = [], DepthM = [], Road = [], Junction = [] };
        var paving = Paving.Lay(
            new GroundPieces(
                (ulong)survey.Relation, new Vector2(survey.WidthM, survey.HeightM), config.PavementWidthM, streets.Roads,
                streets.Bridges, streets.Junctions, corners, streets.Roundabouts, lots, CityPlan.PavedAreaArrays.None, crosswalks,
                water),
            config);
        var pavingMs = clock.Elapsed.TotalMilliseconds;

        // The walk's outer face and the ground answered off it, which the town draws and stands on once opened.
        clock.Restart();
        paving.Rings(config).NewKerbScan();
        var ground = new GroundShapes(paving, config);
        var groundMs = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        var buildings = TracedBuildings.Lay(survey.Footprints, paving, ground, sizes, config);
        var buildingsMs = clock.Elapsed.TotalMilliseconds;

        // <b>The props stand on what is left beside what is built</b> (GEN-6b), so they are laid last.
        clock.Restart();
        var (props, scenery) = TracedProps.Lay(survey, streets.Roads, streets.Junctions, paving, ground, buildings, config);
        var propsMs = clock.Elapsed.TotalMilliseconds;

        // <b>The roster stands at the doors and on the lanes</b>, so it is laid once both are.
        clock.Restart();
        var spawns = TracedSpawns.Lay(survey.Population, cars, paving.Lanes, buildings, config);
        var rosterMs = clock.Elapsed.TotalMilliseconds;

        return new CityPlan
        {
            // The relation the survey was taken inside is the map's own number, and a seed is what a plan
            // keys every draw made off it on: a traced town draws its props and nothing else.
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
            ParkingLots = lots,
            // The footprints are what the buildings were fitted off and not part of the town: it carries the buildings.
            Buildings = buildings,
            Props = props,
            Scenery = scenery,
            Spawns = spawns,
            Water = water,
            PavingLaidWithIt = paving,
            LaidMs =
            [
                ("streets", streetsMs), ("sea", seaMs), ("cars", carsMs), ("paving", pavingMs), ("ground", groundMs),
                ("buildings", buildingsMs), ("props", propsMs), ("roster", rosterMs),
            ],
        };
    }
}
