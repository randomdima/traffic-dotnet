using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>A town laid off its map or its brief</b> (GEN-58, GEN-60), every town the same way once its streets stand: a traced
/// map's own roads (GEN-57), or a brief's streets laid off what was authored from it (<see cref="TownGen"/>) — then every
/// lane and connection, the walk, the lights, the zebras, the buildings (<see cref="ZoneBuildings"/>), the props
/// (<see cref="TownProps"/>) and who stands in the town (<see cref="SpawnStage"/>).
/// </summary>
/// <remarks>
/// <b>Content and not code</b>: a map or a brief is a file in <c>towns/</c> that a build may ship any number of, and from
/// the plan onward a town is a town like any other whatever it was laid off (GEN-1a).
/// </remarks>
internal static class TownPlan
{
    /// <summary>A map read off its file and laid, the reading of it the plan's first stage.</summary>
    public static CityPlan Read(string path, SimConfig config, ArtSizes sizes) => Reading(path, config, sizes).Through();

    /// <summary><see cref="Read"/> a stage at a time: each of the plan's own stages one it may pause after.</summary>
    public static Staged<CityPlan> Reading(string path, SimConfig config, ArtSizes sizes) =>
        new(made => Reading(path, config, sizes, made));

    static IEnumerable<string> Reading(string path, SimConfig config, ArtSizes sizes, Action<CityPlan> made)
    {
        var clock = Stopwatch.StartNew();
        var survey = Survey.Of(TownMap.Read(path), config);
        foreach (var stage in Laying(survey, null, StageSeeds.All(survey.Seed), config, sizes, [("map", clock.Elapsed.TotalMilliseconds)], made))
        {
            yield return stage;
        }
    }

    /// <summary>
    /// <b>A brief's town</b> (GEN-60), a stage at a time: its terrain, connectors and districts authored first, as the plan's
    /// first stage, and the rest laid off them.
    /// </summary>
    public static Staged<CityPlan> Laying(TownBrief brief, SimConfig config, ArtSizes sizes) =>
        new(made => Laying(brief, config, sizes, made));

    public static CityPlan Lay(TownBrief brief, SimConfig config, ArtSizes sizes) => Laying(brief, config, sizes).Through();

    static IEnumerable<string> Laying(TownBrief brief, SimConfig config, ArtSizes sizes, Action<CityPlan> made)
    {
        var clock = Stopwatch.StartNew();
        var town = TownGen.Author(brief, config);
        var survey = Survey.Of(town.Map, config);
        foreach (var stage in Laying(survey, town, town.Seeds, config, sizes, [("authored", clock.Elapsed.TotalMilliseconds)], made))
        {
            yield return stage;
        }
    }

    /// <param name="sizes">
    /// The prefabs its buildings are stood as, the roofs its services wear and the looks its props are laid as, handed
    /// down from the catalogues that read them (<see cref="ArtSizes"/>). A town handed no roofs stands no building, and
    /// one handed no looks lays no prop.
    /// </param>
    public static CityPlan Lay(TownMap map, SimConfig config, ArtSizes sizes) => Lay(Survey.Of(map, config), config, sizes);

    public static CityPlan Lay(Survey survey, SimConfig config, ArtSizes sizes) =>
        new Staged<CityPlan>(made => Laying(survey, null, StageSeeds.All(survey.Seed), config, sizes, [], made)).Through();

    /// <param name="town">What a brief's town was authored as, which lays its streets; none for a map's own.</param>
    /// <param name="seeds">The seed each stage after the streets draws on (GEN-60): a map's own for a traced map.</param>
    static IEnumerable<string> Laying(
        Survey survey, TownGen.Authored? town, StageSeeds seeds, SimConfig config, ArtSizes sizes, (string Stage, double Ms)[] before,
        Action<CityPlan> made)
    {
        var laidMs = new List<(string Stage, double Ms)>(before);
        var clock = Stopwatch.StartNew();
        string Took(string stage)
        {
            laidMs.Add((stage, clock.Elapsed.TotalMilliseconds));
            clock.Restart();
            return OpenWatch.Passed($"plan: {stage}");
        }

        var worldSizeM = new Vector2(survey.WidthM, survey.HeightM);
        var zoned = survey.ZonesOrWhole;
        var zones = new ZoneTree(zoned, config.Zones);
        var generated = town is not null;
        yield return Took("zones");

        var water = Joined(TracedSea.Lay(survey, config), TerrainStage.Rings(survey.Courses, worldSizeM, config));

        // <b>The railway stands with the water, before a node is placed</b> (GEN-10, RAIL-5), and is laid after it: what
        // crosses the water crosses on a bridge.
        var wet = new GroundShapes(GroundPieces.None(survey.Seed, worldSizeM, config.PavementWidthM).With(water), config);
        var rails = RailLines.Lay(survey.Rails, worldSizeM, config, wet);
        yield return Took("water");

        // A brief's streets say their own stages, and are not paused in.
        var streets = town is not null
            ? Laid(TownGen.Lay(town, config, sizes.Buildings, stage => Took(stage)))
            : Laid(TracedCarParks.Bays(survey, TracedStreets.Lay(survey, config), config), config);
        if (!generated) yield return Took("streets");

        var lots = Lots(survey.Lots);
        var paving = Paving.Lay(
            new GroundPieces(
                survey.Seed, worldSizeM, config.PavementWidthM, streets.Roads, streets.Bridges, streets.Junctions, streets.Corners,
                streets.Roundabouts, lots, CityPlan.PavedAreaArrays.None, streets.Crosswalks, water)
            {
                Rails = rails,
            },
            config);
        yield return Took("paving");

        // <b>The walk's outer face and the ground answered off it</b>, which everything stood from here on is cleared
        // against — the boundary the finished town answers with, and nothing below adds driven ground.
        paving.Rings(config).NewKerbScan();
        var ground = new GroundShapes(paving, config);
        yield return Took("ground");

        // <b>The buildings stand against the walk the finished town is laid with</b> (GEN-54): what the map sets down,
        // then the services on their yards (GEN-55), then whatever the zones build.
        var yards = generated && streets.CarParks.Count > 0
            ? BuildingStage.Lay(streets.CarParks, streets.Roads, paving, ground, worldSizeM, sizes.Buildings, config)
            : BuildingStage.Yards.None(config);
        var buildings = ZoneBuildings.Lay(zones, survey with { Seed = seeds.Buildings }, yards, paving, ground, sizes.Buildings, config);
        yield return Took("buildings");

        // <b>The props stand on what is left beside what is built</b> (GEN-6b), so they are laid last.
        var (props, scenery) = TownProps.Lay(
            survey with { Seed = seeds.Props }, zones, streets.Roads, streets.Junctions, paving, ground, buildings, sizes.Props, config);
        yield return Took("props");

        // <b>The roster stands at the doors and on the lanes</b>, so it is laid once both are.
        var spawn = new Rng(seeds.Roster, Streams.Spawn);
        var root = TownMap.ZoneArrays.Root;
        var spawns = SpawnStage.Lay(
            (int)(zoned.Own(root, ZoneParam.People) ?? 0f), (int)(zoned.Own(root, ZoneParam.Cars) ?? 0f), !generated, streets.BridgeCars,
            paving, streets.CarParks, buildings, config, ref spawn);
        yield return Took("roster");

        made(new CityPlan
        {
            Seed = survey.Seed,
            Name = survey.Name,
            WorldSizeM = worldSizeM,
            PavementWidthM = config.PavementWidthM,
            Districts = streets.Districts,
            Junctions = streets.Junctions,
            JunctionCorners = streets.Corners,
            Roads = streets.Roads,
            Bridges = streets.Bridges,
            Roundabouts = streets.Roundabouts,
            CarParks = new CityPlan.CarParkArrays
            {
                Street = streets.CarParks.Street, AtM = streets.CarParks.AtM, BayOffsets = streets.CarParks.BayOffsets,
                Road = streets.CarParks.Road, Right = streets.CarParks.Right,
            },
            PavedAreas = CityPlan.PavedAreaArrays.None,
            Crosswalks = streets.Crosswalks,
            ParkingLots = lots,
            Buildings = buildings,
            Props = props,
            Scenery = scenery,
            Spawns = spawns,
            Water = water,
            Rails = rails,
            Zones = zoned,
            PavingLaidWithIt = paving,
            LaidMs = [.. laidMs],
        });
    }

    /// <summary>The streets a town laid, whichever laid them, as the rest of the town is laid against them.</summary>
    readonly record struct Streets(
        CityPlan.RoadArrays Roads, CityPlan.JunctionArrays Junctions, CityPlan.JunctionCornerArrays Corners, CityPlan.BridgeArrays Bridges,
        CityPlan.RoundaboutArrays Roundabouts, CityPlan.CrosswalkArrays Crosswalks, CarParks.Laid CarParks, DistrictMap Districts,
        CityPlan.SpawnArrays BridgeCars);

    /// <summary>A brief's: its own corners, crossings and car parks, laid in its own districts, and no car on a bridge.</summary>
    static Streets Laid(TownGen.Laid town) => new(
        town.Roads.Roads, town.Roads.Junctions, town.Roads.Corners, town.Roads.Bridges, town.Roads.Roundabouts, town.Roads.Crosswalks,
        town.CarParks, town.Districts, CityPlan.SpawnArrays.None);

    /// <summary>
    /// A map's own roads: no kerb corner or crossing of their own, the bays ranked along its car parks' aisles
    /// (<see cref="TracedCarParks.Bays"/>), one district, and a few cars stood by rule at its bridges over its roads
    /// (<see cref="TracedBridgeCars"/>).
    /// </summary>
    static Streets Laid((TracedStreets.Laid Streets, CarParks.Laid CarParks) traced, SimConfig config) => new(
        traced.Streets.Roads, traced.Streets.Junctions,
        new CityPlan.JunctionCornerArrays { CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [] },
        traced.Streets.Bridges, traced.Streets.Roundabouts, new CityPlan.CrosswalkArrays { CentreM = [], Axis = [], DepthM = [], Road = [], Junction = [] },
        traced.CarParks, DistrictMap.Whole, TracedBridgeCars.Lay(traced.Streets.Roads, config));

    /// <summary>Two waters' rings as one water's, each set of the first's then the second's.</summary>
    static CityPlan.WaterArrays Joined(CityPlan.WaterArrays one, CityPlan.WaterArrays other)
    {
        return new CityPlan.WaterArrays
        {
            Outline = Rings(one.Outline, other.Outline), Shore = Rings(one.Shore, other.Shore),
        };

        static CityPlan.RingArrays Rings(CityPlan.RingArrays one, CityPlan.RingArrays other) => new()
        {
            Offsets = [.. one.Offsets, .. other.Offsets.Skip(1).Select(offset => offset + one.PointM.Length)],
            PointM = [.. one.PointM, .. other.PointM],
        };
    }

    /// <summary>The car parks the map sets down, each a paved rectangle with no space marked in it.</summary>
    static CityPlan.ParkingLotArrays Lots(TownMap.LotArrays lots)
    {
        var (axis, halfM) = (new Vector2[lots.Count], new Vector2[lots.Count]);
        for (var lot = 0; lot < lots.Count; lot++) (axis[lot], halfM[lot]) = (Core.Geometry.Heading.Unit(lots.HeadingRad[lot]), lots.SizeM[lot] * 0.5f);

        return new CityPlan.ParkingLotArrays
        {
            CentreM = [.. lots.CentreM], Axis = axis, HalfExtentM = halfM, SpaceOffsets = new int[lots.Count + 1], SpacePositionM = [],
            SpaceHeadingRad = [],
        };
    }
}
