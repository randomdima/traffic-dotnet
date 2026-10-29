using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>What a generated town's blocks are used for besides its district's own kind</b> (GEN-77): a park, a school or a
/// church and its grounds, and shops along the roads between districts — each a zone of a whole block, under its district's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Laid once the walk is</b>: a block is the ground inside one closed ring of the walk's outer face
/// (<see cref="GroundRings.WalkEdge"/>), which only the streets make, so the town's zones are added to after its streets
/// and before its buildings, and the buildings and props read the uses as they read every zone.
/// </para>
/// <para>
/// <b>Each block's draw is keyed on where it stands</b> (GEN-11), so a block retuned or a street moved elsewhere leaves
/// every other block's use as it was.
/// </para>
/// </remarks>
internal static class BlockUses
{
    /// <summary>The stream a block's use is drawn on.</summary>
    const ulong UseStream = 0x626C_6F63_6B75_7365;

    /// <summary>The kinds of district whose blocks are given other uses: what a generated district is built as (GEN-74).</summary>
    static bool Built(ZoneKind kind) => kind is ZoneKind.OldTown or ZoneKind.Residential or ZoneKind.HighRise or ZoneKind.Suburb or ZoneKind.Industrial;

    /// <summary>The town's zones with a zone added for every block given a use of its own, under its district's zone.</summary>
    public static TownMap.ZoneArrays Zoned(
        TownMap.ZoneArrays zones, ZoneTree tree, GroundRings rings, CityPlan.RoadArrays roads, Vector2 sizeM, SimConfig config, ulong seed)
    {
        var figures = config.CityGen;
        var root = TownMap.ZoneArrays.Root;
        var hubM = zones.Own(root, ZoneParam.HubXM) is { } x && zones.Own(root, ZoneParam.HubYM) is { } y ? new Vector2(x, y) : sizeM * 0.5f;
        var mains = MainRoads(roads, config);

        // A face along a road between districts stands within its widest corridor of its middle; one across a street from
        // it stands a street's width further.
        var frontM = TownGen.CorridorM(config, LayoutEdge.MostLanes);

        var blocks = new List<(Vector2[] OutlineM, Vector2 MiddleM, int District, bool Fronts)>();
        foreach (var points in Blocks.Of(rings).Outlines)
        {
            var middleM = Middle(points);
            var district = tree.At(middleM);
            if (!Built(zones.Kind[district])) continue;

            var fronting = 0;
            foreach (var pointM in points) fronting += mains.AnyWithin(pointM, frontM) ? 1 : 0;
            blocks.Add((points, middleM, district, fronting >= points.Length * figures.BlockFrontsAShare));
        }

        var builder = new TownMap.ZoneArrays.Builder();
        for (var zone = 0; zone < zones.Count; zone++) builder.Add(zones, zone, zones.Parent[zone], Vector2.Zero, sizeM);

        var garden = blocks.Count == 0 ? -1 : Enumerable.Range(0, blocks.Count).MinBy(at => Vector2.DistanceSquared(blocks[at].MiddleM, hubM));
        for (var at = 0; at < blocks.Count; at++)
        {
            var (outlineM, middleM, district, fronts) = blocks[at];
            var draw = new Rng(seed, Streams.Keyed(UseStream, ConnectionPoints.Keyed(middleM, middleM)));
            var (shop, share) = (draw.NextFloat(), draw.NextFloat());
            var kind = zones.Kind[district];
            var onTheHighStreet = fronts && kind is ZoneKind.OldTown or ZoneKind.Residential;
            ZoneKind? use = at == garden ? ZoneKind.Park
                : onTheHighStreet && shop < figures.BlockCommercialShare ? ZoneKind.Commercial
                : share < figures.BlockParkShare ? ZoneKind.Park
                : kind != ZoneKind.Industrial && share < figures.BlockParkShare + figures.BlockCivicShare ? ZoneKind.Civic
                : null;
            if (use is ZoneKind.Park) builder.Add(district, ZoneKind.Park, [(ZoneParam.Growth, figures.BlockParkGrowth)], [outlineM]);
            else if (use is { } given) builder.Add(district, given, [], [outlineM]);
        }

        return builder.Arrays();
    }

    /// <summary>Every road between districts, as points a step apart: what a block fronts as a main road.</summary>
    static PointCells MainRoads(CityPlan.RoadArrays roads, SimConfig config)
    {
        var mains = new PointCells(config.Grid.Main);
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads.Class.Length == 0 || roads.Class[road] != RoadClass.Arterial) continue;

            foreach (var pointM in Along(roads.SegmentsOf(road))) mains.Add(pointM);
        }

        return mains;
    }

    static List<Vector2> Along(ReadOnlySpan<ArcSeg> arcs)
    {
        var points = new List<Vector2>();
        var totalM = Spline.TotalLengthM(arcs);
        for (var atM = Blocks.StepM * 0.5f; atM < totalM; atM += Blocks.StepM) points.Add(Spline.SampleAt(arcs, atM).PositionM);
        return points;
    }

    static Vector2 Middle(Vector2[] ring)
    {
        var sum = Vector2.Zero;
        foreach (var pointM in ring) sum += pointM;
        return sum / ring.Length;
    }
}
