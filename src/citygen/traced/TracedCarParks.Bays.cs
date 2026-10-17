using System.Numerics;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

internal static partial class TracedCarParks
{
    /// <summary>
    /// <b>A rank of bays down both kerbs of every aisle a traced town holds</b> (GEN-53a, <see cref="TracedRoad.Aisle"/>):
    /// the streets with the bays put after their roads as the roads they are, and the car parks they make — one an aisle,
    /// its bays on both sides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A bay here is the bay every town lays</b> (GEN-53, <see cref="CarParks"/>): a lane's width square to the kerb, a
    /// hair back over the aisle's own ground and a bay deep past it, driven both ways over one line, a node a lead off each
    /// end, joined to nothing. What is new is only where they stand: <b>end to end down the whole of an aisle</b>, a lane
    /// apart, where a street's car park is a handful of them (GEN-4b).
    /// </para>
    /// <para>
    /// <b>A bay is laid where all of it stands in a car park's outline, its far node on the map, and nothing else stands</b>: clear of every
    /// junction's disc, every other road's carriageway and every bay already laid, on a stretch of aisle straight enough
    /// to square one to (<see cref="SimConfig.CarParkCurvatureMax"/>), and past the edge of the box at each end of the
    /// aisle by a zebra struck across it (<see cref="SimConfig.CarParkRankClearM"/>). So the ranks stop short of every junction, and the
    /// walk between two ranks back to back (<see cref="CityGenFigures.CarParkIslandM"/>) runs out to the zebras there.
    /// </para>
    /// </remarks>
    public static (TracedStreets.Laid Streets, CarParks.Laid CarParks) Bays(Survey survey, TracedStreets.Laid laid, SimConfig config)
    {
        var roads = laid.Roads;
        var aisles = new List<int>();
        for (var road = 0; road < roads.Count; road++)
        {
            var (from, to) = (laid.WayOffsets[road], laid.WayOffsets[road + 1]);
            if (to > from && laid.Ways.AsSpan(from, to - from).ToArray().All(way => survey.Ways[way].Aisle)) aisles.Add(road);
        }

        if (aisles.Count == 0) return (laid, CarParks.Laid.None);

        var lots = new List<Vector2[]>();
        var zones = survey.Zones;
        for (var zone = 0; zone < zones.Count; zone++)
        {
            if (zones.Kind[zone] == ZoneKind.Parking) lots.Add(zones.OutlineOf(zone).ToArray());
        }

        var ground = new Ground(roads, laid.Junctions);
        var bayM = config.LaneWidthM;
        var lengthM = config.CarParkKerbOverlapM + config.CarParkBayDepthM;
        var leadM = config.CityGen.ConnectionStandoffM;
        var clearM = config.CarParkRankClearM + (bayM * 0.5f);
        var curvatureMost = config.CarParkCurvatureMax(1);
        var mapM = new Vector2(survey.WidthM, survey.HeightM);

        var street = new List<int>();
        var atM = new List<Vector2>();
        var bayOffsets = new List<int> { 0 };
        var right = new List<bool>();
        var bayLines = new List<ArcSeg>();
        foreach (var aisle in aisles)
        {
            var line = roads.SegmentsOf(aisle);
            var aisleM = Spline.TotalLengthM(line);
            var kerbM = roads.WidthM[aisle] * 0.5f;
            var before = bayLines.Count;
            // From the edge of the box at each end and not the line's end: the aisle's line runs to the junction, its lanes
            // only from the box's edge, and a rank begun nearer than a zebra past that is one a car turning in off the
            // junction is already past the place to stop for.
            var rankFromM = laid.Junctions.RadiusM[roads.FromJunction[aisle]] + clearM;
            var rankToM = aisleM - laid.Junctions.RadiusM[roads.ToJunction[aisle]] - clearM;
            for (var alongM = rankFromM; alongM <= rankToM; alongM += bayM)
            {
                var at = Spline.SampleAt(line, alongM);
                if (MathF.Abs(at.Curvature) > curvatureMost
                    || MathF.Abs(Spline.SampleAt(line, alongM - (bayM * 0.5f)).Curvature) > curvatureMost
                    || MathF.Abs(Spline.SampleAt(line, alongM + (bayM * 0.5f)).Curvature) > curvatureMost)
                {
                    continue;
                }

                foreach (var onTheRight in (ReadOnlySpan<bool>)[true, false])
                {
                    var outward = onTheRight ? at.Right : -at.Right;
                    var fromM = at.PositionM + (outward * (kerbM - config.CarParkKerbOverlapM));
                    var bay = new OrientedBox(fromM + (outward * (lengthM * 0.5f)), outward, lengthM * 0.5f, bayM * 0.5f);
                    if (!InTheMap(fromM + (outward * (lengthM + leadM)), mapM) || !InALot(bay, lots) || !ground.Clear(bay, aisle, config.CarParkKerbOverlapM))
                    {
                        continue;
                    }

                    ground.Stand(bay);
                    bayLines.Add(new ArcSeg(fromM, MathF.Atan2(outward.Y, outward.X), lengthM, 0f));
                    right.Add(onTheRight);
                }
            }

            if (bayLines.Count == before) continue;

            street.Add(aisle);
            atM.Add(Spline.SampleAt(line, aisleM * 0.5f).PositionM);
            bayOffsets.Add(bayLines.Count);
        }

        if (bayLines.Count == 0) return (laid, CarParks.Laid.None);

        var (streets, bays) = WithBays(laid, bayLines, leadM, config);
        return (streets, new CarParks.Laid(
            [.. street], [.. atM], [.. bayOffsets], bays, [.. right], Enumerable.Repeat(BuildingUse.Ordinary, street.Count).ToArray()));
    }

    /// <summary>Whether a bay's far node stands on the map, as every node a town lays does.</summary>
    static bool InTheMap(Vector2 nodeM, Vector2 mapM) => nodeM.X >= 0f && nodeM.Y >= 0f && nodeM.X <= mapM.X && nodeM.Y <= mapM.Y;

    /// <summary>Whether every corner of a bay stands inside one car park's outline.</summary>
    static bool InALot(in OrientedBox bay, List<Vector2[]> lots)
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        var (along, across) = (bay.Along * bay.HalfLengthM, new Vector2(-bay.Along.Y, bay.Along.X) * bay.HalfWidthM);
        (corners[0], corners[1], corners[2], corners[3]) = (bay.CentreM - along - across, bay.CentreM - along + across, bay.CentreM + along + across, bay.CentreM + along - across);
        foreach (var lot in lots)
        {
            var inside = true;
            foreach (var corner in corners) inside &= Encloses(lot, corner);
            if (inside) return true;
        }

        return false;
    }

    static bool Encloses(Vector2[] ring, Vector2 atM)
    {
        var inside = false;
        for (int at = 0, before = ring.Length - 1; at < ring.Length; before = at++)
        {
            var (p, q) = (ring[at], ring[before]);
            if ((p.Y > atM.Y) != (q.Y > atM.Y) && atM.X < p.X + ((atM.Y - p.Y) * (q.X - p.X) / (q.Y - p.Y))) inside = !inside;
        }

        return inside;
    }

    /// <summary>
    /// <b>The streets with these bays put after their roads</b>, each a road from a node a lead back over the aisle to one a
    /// lead past its far end (GEN-46) — two junctions of its own at the town's one disc (<see cref="SimConfig.JunctionRadiusM"/>),
    /// unlit — a lane wide, driven both ways over its one line, on the ground, joined to no way of the survey. The bays'
    /// roads, in the order given.
    /// </summary>
    static (TracedStreets.Laid Streets, int[] Bays) WithBays(TracedStreets.Laid laid, List<ArcSeg> bayLines, float leadM, SimConfig config)
    {
        var (roads, junctions) = (laid.Roads, laid.Junctions);
        var (firstRoad, firstJunction, count) = (roads.Count, junctions.Count, bayLines.Count);

        var centreM = new Vector2[firstJunction + (2 * count)];
        junctions.CentreM.AsSpan().CopyTo(centreM);
        var from = new int[firstRoad + count];
        var to = new int[firstRoad + count];
        roads.FromJunction.AsSpan().CopyTo(from);
        roads.ToJunction.AsSpan().CopyTo(to);
        var segmentOffsets = new int[firstRoad + count + 1];
        roads.SegmentOffsets.AsSpan().CopyTo(segmentOffsets);
        var segments = new ArcSeg[roads.Segments.Length + count];
        roads.Segments.AsSpan().CopyTo(segments);
        var bays = new int[count];
        for (var bay = 0; bay < count; bay++)
        {
            var line = bayLines[bay];
            var (near, far) = (firstJunction + (2 * bay), firstJunction + (2 * bay) + 1);
            centreM[near] = line.StartM - (line.StartUnit * leadM);
            centreM[far] = line.EndM + (line.StartUnit * leadM);
            (from[firstRoad + bay], to[firstRoad + bay]) = (near, far);
            segments[roads.Segments.Length + bay] = line;
            segmentOffsets[firstRoad + bay + 1] = roads.Segments.Length + bay + 1;
            bays[bay] = firstRoad + bay;
        }

        var bay2 = new bool[firstRoad + count];
        roads.Bay.AsSpan().CopyTo(bay2);
        bay2.AsSpan(firstRoad).Fill(true);

        var junctionsWith = new CityPlan.JunctionArrays
        {
            CentreM = centreM,
            RadiusM = [.. junctions.RadiusM, .. Enumerable.Repeat(config.JunctionRadiusM, 2 * count)],
            Lit = [.. junctions.Lit, .. new bool[2 * count]],
            PhaseOffsetS = [.. junctions.PhaseOffsetS, .. new float[2 * count]],
            RunsOffTheMap = junctions.RunsOffTheMap.Length > 0 ? [.. junctions.RunsOffTheMap, .. new bool[2 * count]] : [],
        };

        var roadsWith = new CityPlan.RoadArrays
        {
            FromJunction = from, ToJunction = to,
            WidthM = [.. roads.WidthM, .. Enumerable.Repeat(config.LaneWidthM, count)],
            Flow = [.. roads.Flow, .. new RoadFlow[count]],
            Lanes = roads.Lanes.Length > 0 ? [.. roads.Lanes, .. Enumerable.Repeat(new RoadLanes(1, 1), count)] : [],
            Bay = bay2,
            SegmentOffsets = segmentOffsets, Segments = segments,
            ThroughOffsets = roads.ThroughOffsets.Length > 0 ? [.. roads.ThroughOffsets, .. Enumerable.Repeat(roads.ThroughOffsets[^1], count)] : [],
            ThroughM = roads.ThroughM,
            LaidStraight = roads.LaidStraight.Length > 0 ? [.. roads.LaidStraight, .. Enumerable.Repeat(true, count)] : [],
            BannedTurns = roads.BannedTurns,
            Level = roads.Level.Length > 0 ? [.. roads.Level, .. new byte[count]] : [],
            RoadsideWithM = roads.RoadsideWithM.Length > 0 ? [.. roads.RoadsideWithM, .. new float[count]] : [],
            RoadsideAgainstM = roads.RoadsideAgainstM.Length > 0 ? [.. roads.RoadsideAgainstM, .. new float[count]] : [],
        };

        var wayOffsets = new int[firstRoad + count + 1];
        laid.WayOffsets.AsSpan().CopyTo(wayOffsets);
        wayOffsets.AsSpan(firstRoad + 1).Fill(laid.WayOffsets[^1]);

        return (laid with { Junctions = junctionsWith, Roads = roadsWith, WayOffsets = wayOffsets }, bays);
    }

    /// <summary>
    /// <b>What a bay must stand clear of</b>: every junction's disc, every road's carriageway but the aisle it is worked off,
    /// and every bay already stood — each road read as points along its line, filed by where they stand.
    /// </summary>
    sealed class Ground
    {
        const float CellM = 16f;

        /// <summary>How finely a road's line is read for the ground it takes.</summary>
        const float StepM = 1f;

        readonly Dictionary<(int, int), List<(Vector2 AtM, float HalfM, int Road)>> _lines = [];
        readonly Dictionary<(int, int), List<(Vector2 AtM, float RadiusM)>> _discs = [];
        readonly Dictionary<(int, int), List<OrientedBox>> _bays = [];
        readonly float _widestHalfM;
        readonly float _widestDiscM;

        public Ground(CityPlan.RoadArrays roads, CityPlan.JunctionArrays junctions)
        {
            for (var road = 0; road < roads.Count; road++)
            {
                var line = roads.SegmentsOf(road);
                var lengthM = Spline.TotalLengthM(line);
                var halfM = roads.WidthM[road] * 0.5f;
                _widestHalfM = MathF.Max(_widestHalfM, halfM);
                for (var alongM = 0f; alongM <= lengthM; alongM += StepM)
                {
                    var atM = Spline.SampleAt(line, alongM).PositionM;
                    File(_lines, atM, (atM, halfM, road));
                }
            }

            for (var junction = 0; junction < junctions.Count; junction++)
            {
                _widestDiscM = MathF.Max(_widestDiscM, junctions.RadiusM[junction]);
                File(_discs, junctions.CentreM[junction], (junctions.CentreM[junction], junctions.RadiusM[junction]));
            }
        }

        /// <summary>
        /// Whether a bay stands clear of everything but the aisle it is worked off, which its mouth runs back over by
        /// <paramref name="overlapM"/>.
        /// </summary>
        public bool Clear(in OrientedBox bay, int aisle, float overlapM)
        {
            var reachM = bay.HalfLengthM + bay.HalfWidthM;
            foreach (var (atM, halfM, road) in Near(_lines, bay.CentreM, reachM + _widestHalfM))
            {
                if (road != aisle && OffM(bay, atM) < halfM) return false;
            }

            foreach (var (atM, radiusM) in Near(_discs, bay.CentreM, reachM + _widestDiscM))
            {
                if (OffM(bay, atM) < radiusM) return false;
            }

            foreach (var other in Near(_bays, bay.CentreM, 2f * reachM))
            {
                if (bay.Overlaps(other, overlapM)) return false;
            }

            return true;
        }

        public void Stand(in OrientedBox bay) => File(_bays, bay.CentreM, bay);

        /// <summary>How far a place stands outside a bay, nought inside it.</summary>
        static float OffM(in OrientedBox bay, Vector2 atM)
        {
            var offset = atM - bay.CentreM;
            var across = new Vector2(-bay.Along.Y, bay.Along.X);
            var outside = new Vector2(
                MathF.Max(0f, MathF.Abs(Vector2.Dot(offset, bay.Along)) - bay.HalfLengthM),
                MathF.Max(0f, MathF.Abs(Vector2.Dot(offset, across)) - bay.HalfWidthM));
            return outside.Length();
        }

        static void File<T>(Dictionary<(int, int), List<T>> cells, Vector2 atM, T item)
        {
            var cell = ((int)MathF.Floor(atM.X / CellM), (int)MathF.Floor(atM.Y / CellM));
            if (!cells.TryGetValue(cell, out var here)) cells[cell] = here = [];
            here.Add(item);
        }

        /// <summary>Everything filed in the cells within reach of a place.</summary>
        static IEnumerable<T> Near<T>(Dictionary<(int, int), List<T>> cells, Vector2 atM, float reachM)
        {
            for (var x = (int)MathF.Floor((atM.X - reachM) / CellM); x <= (int)MathF.Floor((atM.X + reachM) / CellM); x++)
            {
                for (var y = (int)MathF.Floor((atM.Y - reachM) / CellM); y <= (int)MathF.Floor((atM.Y + reachM) / CellM); y++)
                {
                    if (!cells.TryGetValue((x, y), out var here)) continue;

                    foreach (var item in here) yield return item;
                }
            }
        }
    }
}
