using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A traced town's zebras, where its survey maps them</b> (GEN-57, TER-6): every painted pedestrian crossing laid
/// across the road its way was laid along, at the place on that road's line nearest where OSM puts it — and none
/// anywhere else.
/// </summary>
/// <remarks>
/// <para>
/// <b>On the road its own way runs along</b>: a crossing names the way it crosses, and of the roads laid along that
/// way the one whose line passes nearest is the one painted. A crossing a few metres off its junction stands where
/// the junction's disc has taken the road's line, so it is laid at that road's end.
/// </para>
/// <para>
/// <b>A whole zebra on its road</b>: its middle stands half its depth inside either end of the line, and a road
/// shorter than a zebra is deep is painted with one at its middle. Two on one road nearer than a zebra's depth are
/// one — a crossing mapped on the road's node and again on a carriageway beside it share their paint.
/// </para>
/// </remarks>
internal static class TracedCrossings
{
    public static CityPlan.CrosswalkArrays Lay(Survey survey, TracedStreets.Laid laid, SimConfig config)
    {
        var roads = laid.Roads;
        var depthM = config.Road.CrossingDepthM;
        var roadsOf = new Dictionary<long, List<int>>();
        for (var road = 0; road < roads.Count; road++)
        {
            for (var at = laid.WayOffsets[road]; at < laid.WayOffsets[road + 1]; at++)
            {
                var way = survey.Ways[laid.Ways[at]].OsmId;
                if (!roadsOf.TryGetValue(way, out var along)) roadsOf[way] = along = [];
                if (along.Count == 0 || along[^1] != road) along.Add(road);
            }
        }

        var junctionAt = new Dictionary<int, int>();
        for (var junction = 0; junction < laid.JunctionPoint.Length; junction++)
        {
            if (laid.JunctionPoint[junction] != CityPlan.NoRecord) junctionAt[laid.JunctionPoint[junction]] = junction;
        }

        var centreM = new List<Vector2>();
        var axis = new List<Vector2>();
        var onRoad = new List<int>();
        var ofJunction = new List<int>();
        var paintedAlong = new Dictionary<int, List<float>>();
        foreach (var crossing in survey.Crossings)
        {
            if (!crossing.Painted || !roadsOf.TryGetValue(crossing.Way, out var candidates)) continue;

            var (road, alongM, offSq) = (CityPlan.NoRecord, 0f, float.PositiveInfinity);
            foreach (var candidate in candidates)
            {
                var arcs = roads.SegmentsOf(candidate);
                var lengthM = Spline.TotalLengthM(arcs);
                var projectedM = Spline.ProjectM(arcs, crossing.AtM, lengthM * 0.5f, lengthM, out var candidateOffSq);
                if (candidateOffSq < offSq) (road, alongM, offSq) = (candidate, projectedM, candidateOffSq);
            }

            if (road == CityPlan.NoRecord) continue;

            var line = roads.SegmentsOf(road);
            var roadM = Spline.TotalLengthM(line);
            alongM = roadM <= depthM ? roadM * 0.5f : Math.Clamp(alongM, depthM * 0.5f, roadM - (depthM * 0.5f));
            if (!paintedAlong.TryGetValue(road, out var painted)) paintedAlong[road] = painted = [];
            if (painted.Exists(otherM => MathF.Abs(otherM - alongM) < depthM)) continue;

            painted.Add(alongM);
            var at = Spline.SampleAt(line, alongM);
            centreM.Add(at.PositionM);
            axis.Add(at.Direction);
            onRoad.Add(road);
            ofJunction.Add(junctionAt.TryGetValue(crossing.Junction, out var junction)
                           && (roads.FromJunction[road] == junction || roads.ToJunction[road] == junction)
                ? junction
                : CityPlan.NoRecord);
        }

        var depth = new float[centreM.Count];
        Array.Fill(depth, depthM);
        return new CityPlan.CrosswalkArrays
        {
            CentreM = [.. centreM], Axis = [.. axis], DepthM = depth, Road = [.. onRoad], Junction = [.. ofJunction],
        };
    }
}
