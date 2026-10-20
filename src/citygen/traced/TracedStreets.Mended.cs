using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

internal static partial class TracedStreets
{
    /// <summary>
    /// <b>A survey's ways mended into the streets the town holds</b> (GEN-57, <c>qq osm --mend</c>): what OSM left unjoined
    /// joined (<see cref="Survey.Noded"/>), its ways walked into roads junction to junction, every street held even along
    /// its length (<see cref="Evened"/>), and a road that then differs from the one it runs on into by nothing that road
    /// carried on (<see cref="JoinedThrough"/>) — each road handed back as a way of its own, its lanes, width, roadsides and
    /// middle as they came out and its lanes running along its points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The map is changed and not the town</b>: a map is mended once, in place, and written (<see cref="Map.TownMap"/>,
    /// <see cref="TracedRoad.Mended"/>), so laying a town off it reads its lanes as they are and evens nothing. What is
    /// mended is the map's own roads, not anything drawn off them.
    /// </para>
    /// <para>
    /// <b>Every point the survey held is kept where it was</b>, a way's and the coast's alike, and a point the mending put
    /// in comes after them: the coast and the water hold their indices.
    /// </para>
    /// </remarks>
    internal static Survey Mended(Survey survey, SimConfig config)
    {
        // Again over what it came out with until that comes out the same: holding one street even can leave the next a
        // road to carry on into, and parting two carriageways a sliver left to part.
        var mended = MendedOnce(survey, config);
        for (var round = 1; round < MendRounds; round++)
        {
            var again = MendedOnce(mended, config);
            if (Same(again, mended)) return again;

            mended = again;
        }

        return mended;

        static bool Same(Survey one, Survey other)
        {
            if (one.Ways.Length != other.Ways.Length || one.PointCount != other.PointCount) return false;

            for (var way = 0; way < one.Ways.Length; way++)
            {
                var (a, b) = (one.Ways[way], other.Ways[way]);
                if (a.LanesForward != b.LanesForward || a.LanesBackward != b.LanesBackward || !a.Points.AsSpan().SequenceEqual(b.Points)
                    || MathF.Abs(a.CarriagewayM - b.CarriagewayM) > LineTolerance.OnePlaceM
                    || MathF.Abs(a.CentreOffsetM - b.CentreOffsetM) > LineTolerance.OnePlaceM
                    || a.RoadsideAlongM != b.RoadsideAlongM || a.RoadsideAgainstM != b.RoadsideAgainstM)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>How many times at most the mending is run over its own result before it is taken as it stands.</summary>
    const int MendRounds = 4;

    /// <summary>One pass of <see cref="Mended(Survey, SimConfig)"/>.</summary>
    static Survey MendedOnce(Survey survey, SimConfig config)
    {
        var placedM = new List<Vector2D>(survey.PointCount);
        for (var point = 0; point < survey.PointCount; point++) placedM.Add(new Vector2D(survey.PointsM[2 * point], survey.PointsM[(2 * point) + 1]));

        var leaving = new List<int>();
        for (var point = 0; point < survey.LeavesTheMap.Length; point++)
        {
            if (survey.LeavesTheMap[point]) leaving.Add(point);
        }

        var ways = new List<SurveyWay>(survey.Ways);
        Survey.Noded(ways, placedM, leaving, float.DegreesToRadians(config.Road.TurnStraightToleranceDeg));
        var noded = Rebuilt(survey, [.. ways], placedM, survey.LeavesTheMap);

        var edges = Edges(noded);
        var ends = EndsAt(noded.PointCount, edges);
        var place = Places(noded, edges, ends);
        var (junctionOf, centreM, _) = Junctions(noded, place);
        var roads = Walked(noded, edges, ends, place, junctionOf);
        roads = Unlooped(roads, centreM);
        OneWayShared(roads, centreM.Count);
        Evened(roads, centreM, config);
        JoinedThrough(roads, centreM.Count);

        // Each road's points as the survey's own where it stands on one, else a point of its own after them.
        var index = new Dictionary<Vector2, int>(noded.PointCount);
        for (var point = noded.PointCount - 1; point >= 0; point--) index[noded.PointM(point)] = point;

        var mended = new List<SurveyWay>(roads.Count);
        foreach (var road in roads)
        {
            if (road.Gone) continue;

            // Its lanes along its points: a road whose traffic all runs against them is turned round.
            var (pointsM, carriage) = road.Carriage.Lanes.With == 0
                ? (Enumerable.Reverse(road.PointsM).ToList(), road.Carriage.Turned)
                : (road.PointsM, road.Carriage);

            var points = new int[pointsM.Count];
            for (var at = 0; at < points.Length; at++)
            {
                if (!index.TryGetValue(pointsM[at], out points[at]))
                {
                    points[at] = index[pointsM[at]] = placedM.Count;
                    placedM.Add(new Vector2D(pointsM[at].X, pointsM[at].Y));
                }
            }

            mended.Add(new SurveyWay
            {
                OsmId = noded.Ways[road.Ways[0]].OsmId,
                Highway = road.Highway,
                Bridge = carriage.Level != CityPlan.RoadArrays.Ground,
                Roundabout = carriage.Circulates,
                LanesForward = carriage.Lanes.With,
                LanesBackward = carriage.Lanes.Against,
                LanesShared = 0,
                CarriagewayM = carriage.WidthM,
                RoadsideAlongM = carriage.RoadsideWithM,
                RoadsideAgainstM = carriage.RoadsideAgainstM,
                CentreOffsetM = carriage.CentreOffsetM,
                Aisle = noded.Ways[road.Ways[0]].Aisle,
                Points = points,
            });
        }

        var leaves = new bool[placedM.Count];
        noded.LeavesTheMap.AsSpan().CopyTo(leaves);
        return Rebuilt(survey, [.. mended], placedM, leaves);
    }

    /// <summary>
    /// <b>A traced map with its roads mended</b> (<see cref="Mended(Survey, SimConfig)"/>): every road the mending came out
    /// with written as a road of the map's (<see cref="TracedRoad.Mended"/>), named after the first OSM way it runs along,
    /// its points the map's own where it stands on them — each written as the map held it — and new ones after them.
    /// Everything else the map holds is kept.
    /// </summary>
    /// <remarks>
    /// <b>Mended again off the map as it reads back until it reads back the same</b> (<see cref="Map.TownMap.Held"/>): a map
    /// holds a width and a middle to its own steps, and two roads a hair apart in memory are one road once written. A
    /// mended map mended again is the same bytes.
    /// </remarks>
    internal static Map.TownMap Mended(Map.TownMap map, SimConfig config)
    {
        var current = map;
        var bytes = Bytes(current);
        for (var round = 0; round < MendRounds; round++)
        {
            var next = MendedOnce(current, config).Held();
            var nextBytes = Bytes(next);
            if (nextBytes.AsSpan().SequenceEqual(bytes)) return next;

            (current, bytes) = (next, nextBytes);
        }

        return current;

        static byte[] Bytes(Map.TownMap map)
        {
            using var stream = new MemoryStream();
            map.Write(stream);
            return stream.ToArray();
        }
    }

    /// <summary>One pass of <see cref="Mended(Map.TownMap, SimConfig)"/>.</summary>
    static Map.TownMap MendedOnce(Map.TownMap map, SimConfig config)
    {
        var mended = Mended(Survey.Of(map, config), config);

        var pointM = new Vector2D[mended.PointCount];
        map.PointM.AsSpan().CopyTo(pointM);
        for (var point = map.PointM.Length; point < pointM.Length; point++)
        {
            pointM[point] = new Vector2D(mended.PointsM[2 * point], mended.PointsM[(2 * point) + 1]);
        }

        var roads = new TracedRoad[mended.Ways.Length];
        for (var road = 0; road < roads.Length; road++)
        {
            var way = mended.Ways[road];
            var lanesM = way.CarriagewayM - way.RoadsideAlongM - way.RoadsideAgainstM;
            roads[road] = new TracedRoad
            {
                OsmId = way.OsmId,
                Highway = way.Highway,
                Bridge = way.Bridge,
                Roundabout = way.Roundabout,
                LanesForward = way.LanesForward,
                LanesBackward = way.LanesBackward,
                LanesShared = way.LanesShared,
                LanesTagged = true,
                CentreOffsetShare = lanesM > 0f ? way.CentreOffsetM / lanesM : 0f,
                WidthM = way.CarriagewayM,
                Mended = true,
                RoadsideAlong = way.RoadsideAlongM > 0f,
                RoadsideAgainst = way.RoadsideAgainstM > 0f,
                Aisle = way.Aisle,
                Points = way.Points,
            };
        }

        return Compacted(map with { PointM = pointM, Roads = Thinned(Chained(roads, pointM, config), pointM) });
    }

    /// <summary>
    /// <b>Mended roads run on through the junctions they carry straight on through, as OSM's ways do</b>: a road ending at a
    /// point where one alike in everything a road holds sets off — each the other's nearest straight on, within
    /// <see cref="RoadFigures.TurnStraightToleranceDeg"/> — is one road with it. Laying a town cuts every way at every point
    /// another meets it (<see cref="Edges"/>), so the roads laid are the same and the map holds fewer.
    /// </summary>
    static TracedRoad[] Chained(TracedRoad[] roads, Vector2D[] pointM, SimConfig config)
    {
        var leastCos = Trig.Cos(float.DegreesToRadians(config.Road.TurnStraightToleranceDeg));
        var (starting, ending) = (new Dictionary<int, List<int>>(), new Dictionary<int, List<int>>());
        for (var road = 0; road < roads.Length; road++)
        {
            Add(starting, roads[road].Points[0], road);
            Add(ending, roads[road].Points[^1], road);
        }

        // Each road's straightest alike successor at its end, and each road's straightest alike predecessor at its start.
        var after = new int[roads.Length];
        var before = new int[roads.Length];
        Array.Fill(after, -1);
        Array.Fill(before, -1);
        for (var road = 0; road < roads.Length; road++)
        {
            after[road] = Straightest(road, starting.GetValueOrDefault(roads[road].Points[^1]), outOf: true);
            before[road] = Straightest(road, ending.GetValueOrDefault(roads[road].Points[0]), outOf: false);
        }

        var chained = new List<TracedRoad>();
        var taken = new bool[roads.Length];
        for (var pass = 0; pass < 2; pass++)
        {
            for (var road = 0; road < roads.Length; road++)
            {
                // Heads first, and on the second pass whatever is left: a ring of roads alike all round, cut where it was met.
                if (taken[road] || (pass == 0 && Linked(before[road], road))) continue;

                var points = new List<int>(roads[road].Points);
                taken[road] = true;
                for (var current = road; after[current] is var next and >= 0 && !taken[next] && Linked(current, next); current = next)
                {
                    taken[next] = true;
                    points.AddRange(roads[next].Points.AsSpan(1));
                }

                chained.Add(roads[road] with { Points = [.. points] });
            }
        }

        return [.. chained];

        bool Linked(int road, int next) => road >= 0 && after[road] == next && before[next] == road;

        int Straightest(int road, List<int>? candidates, bool outOf)
        {
            if (candidates is null) return -1;

            var points = roads[road].Points;
            var along = outOf ? Direction(points[^2], points[^1]) : Direction(points[0], points[1]);
            var (best, mostCos) = (-1, leastCos);
            foreach (var other in candidates)
            {
                if (other == road || !Alike(roads[road], roads[other])) continue;

                var otherPoints = roads[other].Points;
                var on = outOf ? Direction(otherPoints[0], otherPoints[1]) : Direction(otherPoints[^2], otherPoints[^1]);
                var cos = Vector2.Dot(along, on);
                if (cos >= mostCos) (best, mostCos) = (other, cos);
            }

            return best;
        }

        Vector2 Direction(int from, int to) =>
            Vector2.Normalize(new Vector2((float)(pointM[to].X - pointM[from].X), (float)(pointM[to].Y - pointM[from].Y)));

        static bool Alike(TracedRoad one, TracedRoad other) =>
            one.Highway == other.Highway && one.Bridge == other.Bridge && one.Roundabout == other.Roundabout
            && one.LanesForward == other.LanesForward && one.LanesBackward == other.LanesBackward && one.LanesShared == other.LanesShared
            && one.WidthM == other.WidthM && one.CentreOffsetShare == other.CentreOffsetShare
            && one.RoadsideAlong == other.RoadsideAlong && one.RoadsideAgainst == other.RoadsideAgainst;

        static void Add(Dictionary<int, List<int>> at, int point, int road)
        {
            if (!at.TryGetValue(point, out var list)) at[point] = list = [];
            list.Add(road);
        }
    }

    /// <summary>
    /// <b>A road's points less every one that adds nothing to its line</b> (Douglas–Peucker): its two ends, every point
    /// another road shares, and every point standing further than <see cref="LineTolerance.OnePlaceM"/> off the straight
    /// between the points kept either side of it — two places are one place nearer than that, and the corners a town lays
    /// a road round are found a tolerance further off (<see cref="TracedAlignment"/>).
    /// </summary>
    static TracedRoad[] Thinned(TracedRoad[] roads, Vector2D[] pointM)
    {
        var uses = new Dictionary<int, int>();
        foreach (var road in roads)
        {
            foreach (var point in road.Points) uses[point] = uses.GetValueOrDefault(point) + 1;
        }

        var thinned = new TracedRoad[roads.Length];
        var runs = new Stack<(int First, int Last)>();
        for (var road = 0; road < roads.Length; road++)
        {
            var points = roads[road].Points;
            var kept = new bool[points.Length];
            (kept[0], kept[^1]) = (true, true);
            for (var at = 1; at < points.Length - 1; at++) kept[at] = uses[points[at]] > 1;

            // Between every two points kept already, the furthest off the straight between them while one stands that far.
            var fixedAt = new List<int>();
            for (var at = 0; at < points.Length; at++)
            {
                if (kept[at]) fixedAt.Add(at);
            }

            for (var run = 1; run < fixedAt.Count; run++) runs.Push((fixedAt[run - 1], fixedAt[run]));
            while (runs.Count > 0)
            {
                var (first, last) = runs.Pop();
                var (furthest, furthestM) = (-1, (double)LineTolerance.OnePlaceM);
                for (var at = first + 1; at < last; at++)
                {
                    var offM = OffM(pointM[points[at]], pointM[points[first]], pointM[points[last]]);
                    if (offM > furthestM) (furthest, furthestM) = (at, offM);
                }

                if (furthest < 0) continue;

                kept[furthest] = true;
                runs.Push((first, furthest));
                runs.Push((furthest, last));
            }

            var left = new List<int>(points.Length);
            for (var at = 0; at < points.Length; at++)
            {
                if (kept[at]) left.Add(points[at]);
            }

            thinned[road] = roads[road] with { Points = [.. left] };
        }

        return thinned;

        static double OffM(Vector2D atM, Vector2D fromM, Vector2D toM)
        {
            var (runX, runY) = (toM.X - fromM.X, toM.Y - fromM.Y);
            var lengthSq = (runX * runX) + (runY * runY);
            var along = lengthSq <= 0.0 ? 0.0 : Math.Clamp((((atM.X - fromM.X) * runX) + ((atM.Y - fromM.Y) * runY)) / lengthSq, 0.0, 1.0);
            var (offX, offY) = (fromM.X + (runX * along) - atM.X, fromM.Y + (runY * along) - atM.Y);
            return Math.Sqrt((offX * offX) + (offY * offY));
        }
    }

    /// <summary>A map holding only the points its roads and its coast run through, in the order they stood.</summary>
    static Map.TownMap Compacted(Map.TownMap map)
    {
        var used = new bool[map.PointM.Length];
        foreach (var road in map.Roads)
        {
            foreach (var point in road.Points) used[point] = true;
        }

        foreach (var line in map.Coast)
        {
            foreach (var point in line) used[point] = true;
        }

        var into = new int[used.Length];
        var pointM = new List<Vector2D>(used.Length);
        for (var point = 0; point < used.Length; point++)
        {
            if (!used[point]) continue;

            into[point] = pointM.Count;
            pointM.Add(map.PointM[point]);
        }

        int[] Moved(int[] points)
        {
            var moved = new int[points.Length];
            for (var at = 0; at < points.Length; at++) moved[at] = into[points[at]];
            return moved;
        }

        return map with
        {
            PointM = [.. pointM],
            Roads = [.. map.Roads.Select(road => road with { Points = Moved(road.Points) })],
            Coast = [.. map.Coast.Select(Moved)],
        };
    }

    /// <summary>A survey with these ways over these points, the rest of it the survey given.</summary>
    static Survey Rebuilt(Survey survey, SurveyWay[] ways, List<Vector2D> placedM, bool[] leavesTheMap)
    {
        var pointsM = new float[2 * placedM.Count];
        for (var point = 0; point < placedM.Count; point++) (pointsM[2 * point], pointsM[(2 * point) + 1]) = ((float)placedM[point].X, (float)placedM[point].Y);

        var leaves = new bool[placedM.Count];
        leavesTheMap.AsSpan(0, Math.Min(leavesTheMap.Length, leaves.Length)).CopyTo(leaves);
        return new Survey
        {
            Name = survey.Name, Seed = survey.Seed, WidthM = survey.WidthM, HeightM = survey.HeightM, PointsM = pointsM, Ways = ways,
            LeavesTheMap = leaves, Sea = survey.Sea, Courses = survey.Courses, Bridgeable = survey.Bridgeable, Zones = survey.Zones,
            Buildings = survey.Buildings, Props = survey.Props, Lots = survey.Lots,
        };
    }
}
