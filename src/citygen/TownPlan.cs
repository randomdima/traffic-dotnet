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
/// <b>A town laid off its map</b> (GEN-58), every map the same way: what the map fixes stands as it is — its water, its
/// roads (GEN-57), and what it sets down — and its zones lay the rest round it, off the map's own seed: a wheel's
/// streets where its whole map is one (<see cref="WheelStreets"/>), every lane and connection, the walk, the lights,
/// the zebras, the buildings (<see cref="ZoneBuildings"/>), the props (<see cref="TownProps"/>) and who stands in the
/// town (<see cref="SpawnStage"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Content and not code</b>: a map is a file in <c>towns/</c> that a build may ship any number of, and from the plan
/// onward a town is a town like any other whatever authored its map (GEN-1a).
/// </para>
/// <para>
/// <b>A map's roads are its own or its zones', and never both</b>: a whole map that is a wheel lays its streets, and one
/// that is not lays the roads it holds. A map that holds roads and is a wheel is refused.
/// </para>
/// </remarks>
internal static class TownPlan
{
    /// <summary>A map read off its file and laid, the reading of it the plan's first stage.</summary>
    public static CityPlan Read(string path, SimConfig config, BuildingSizes sizes)
    {
        var clock = Stopwatch.StartNew();
        var survey = Survey.Of(TownMap.Read(path), config);
        return Lay(survey, config, sizes, [("map", clock.Elapsed.TotalMilliseconds)]);
    }

    /// <param name="sizes">
    /// The prefabs its buildings are stood as and the roofs its services wear, handed down from the catalogue that read
    /// them (<see cref="BuildingSizes"/>). A town handed none stands no building.
    /// </param>
    public static CityPlan Lay(TownMap map, SimConfig config, BuildingSizes sizes) => Lay(Survey.Of(map, config), config, sizes, []);

    public static CityPlan Lay(Survey survey, SimConfig config, BuildingSizes sizes) => Lay(survey, config, sizes, []);

    static CityPlan Lay(Survey survey, SimConfig config, BuildingSizes sizes, (string Stage, double Ms)[] before)
    {
        var laidMs = new List<(string Stage, double Ms)>(before);
        var clock = Stopwatch.StartNew();
        void Took(string stage)
        {
            laidMs.Add((stage, clock.Elapsed.TotalMilliseconds));
            clock.Restart();
        }

        var worldSizeM = new Vector2(survey.WidthM, survey.HeightM);
        var zoned = survey.ZonesOrWhole;
        var zones = new ZoneTree(zoned, config.Zones);
        var wheel = zoned.Kind[TownMap.ZoneArrays.Root] == ZoneKind.Wheel;
        if (wheel && survey.Ways.Length > 0) throw new InvalidDataException($"{survey.Name}: a wheel whose map holds roads of its own.");
        Took("zones");

        var water = TracedSea.Lay(survey, config);
        for (var course = 0; course < survey.Courses.Count; course++)
        {
            water = Joined(water, TerrainStage.Rings(
                survey.Courses.CourseOf(course), survey.Courses.Across[course], survey.Courses.NearM[course], survey.Courses.FarM[course],
                worldSizeM, config));
        }

        Took("water");

        var streets = wheel
            ? Laid(WheelStreets.Lay(zoned, survey.Seed, worldSizeM, water, survey.Bridgeable, config, sizes, Took))
            : Laid(TracedStreets.Lay(survey, config), config);
        if (!wheel) Took("streets");

        var lots = Lots(survey.Lots);
        var paving = Paving.Lay(
            new GroundPieces(
                survey.Seed, worldSizeM, config.PavementWidthM, streets.Roads, streets.Bridges, streets.Junctions, streets.Corners,
                streets.Roundabouts, lots, CityPlan.PavedAreaArrays.None, streets.Crosswalks, water),
            config);
        Took("paving");

        // <b>The walk's outer face and the ground answered off it</b>, which everything stood from here on is cleared
        // against — the boundary the finished town answers with, and nothing below adds driven ground.
        paving.Rings(config).NewKerbScan();
        var ground = new GroundShapes(paving, config);
        Took("ground");

        // <b>The buildings stand against the walk the finished town is laid with</b> (GEN-54): what the map sets down,
        // then the services on their yards (GEN-55), then whatever the zones build.
        var yards = streets.CarParks.Count > 0
            ? BuildingStage.Lay(streets.CarParks, streets.Roads, paving, ground, worldSizeM, sizes, config)
            : BuildingStage.Yards.None(config);
        var buildings = ZoneBuildings.Lay(zones, survey, yards, paving, ground, sizes, config);
        Took("buildings");

        // <b>The props stand on what is left beside what is built</b> (GEN-6b), so they are laid last.
        var (props, scenery) = TownProps.Lay(survey, zones, streets.Roads, streets.Junctions, paving, ground, buildings, config);
        Took("props");

        // <b>The roster stands at the doors and on the lanes</b>, so it is laid once both are.
        var spawn = new Rng(survey.Seed, Streams.Spawn);
        var root = TownMap.ZoneArrays.Root;
        var spawns = SpawnStage.Lay(
            (int)(zoned.Own(root, ZoneParam.People) ?? 0f), (int)(zoned.Own(root, ZoneParam.Cars) ?? 0f), streets.BridgeCars,
            paving, streets.CarParks, buildings, config, ref spawn);
        Took("roster");

        return new CityPlan
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
            Zones = zoned,
            PavingLaidWithIt = paving,
            LaidMs = [.. laidMs],
        };
    }

    /// <summary>The streets a town laid, whichever laid them, as the rest of the town is laid against them.</summary>
    readonly record struct Streets(
        CityPlan.RoadArrays Roads, CityPlan.JunctionArrays Junctions, CityPlan.JunctionCornerArrays Corners, CityPlan.BridgeArrays Bridges,
        CityPlan.RoundaboutArrays Roundabouts, CityPlan.CrosswalkArrays Crosswalks, CarParks.Laid CarParks, DistrictWheel Districts,
        CityPlan.SpawnArrays BridgeCars);

    /// <summary>A wheel's: its own corners, crossings and car parks, laid in its own districts, and no car on a bridge.</summary>
    static Streets Laid(WheelStreets.Laid wheel) => new(
        wheel.Roads.Roads, wheel.Roads.Junctions, wheel.Roads.Corners, wheel.Roads.Bridges, wheel.Roads.Roundabouts, wheel.Roads.Crosswalks,
        wheel.CarParks, wheel.Districts.Wheel, CityPlan.SpawnArrays.None);

    /// <summary>
    /// A map's own roads: no kerb corner, crossing or car park of their own, one district, and a few cars stood by rule
    /// at its bridges over its roads (<see cref="TracedBridgeCars"/>).
    /// </summary>
    static Streets Laid(TracedStreets.Laid traced, SimConfig config) => new(
        traced.Roads, traced.Junctions, new CityPlan.JunctionCornerArrays { CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [] },
        traced.Bridges, traced.Roundabouts, new CityPlan.CrosswalkArrays { CentreM = [], Axis = [], DepthM = [], Road = [], Junction = [] },
        CarParks.Laid.None, DistrictWheel.Whole, TracedBridgeCars.Lay(traced.Roads, config));

    /// <summary>Two waters' rings as one water's, each set of the first's then the second's.</summary>
    static CityPlan.WaterArrays Joined(CityPlan.WaterArrays one, CityPlan.WaterArrays other)
    {
        return new CityPlan.WaterArrays
        {
            Outline = Rings(one.Outline, other.Outline), Shore = Rings(one.Shore, other.Shore), ShoreEdge = Rings(one.ShoreEdge, other.ShoreEdge),
            WaterEdge = Rings(one.WaterEdge, other.WaterEdge),
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
