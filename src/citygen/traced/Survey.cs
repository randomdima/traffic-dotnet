using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A map read into this engine's terms</b> (GEN-58): every road of a <see cref="TownMap"/>, each as its points, its
/// <c>highway</c> value and its lanes (GEN-57), its water — the sea its coastline closes against the map's edge and every
/// water it holds whole — what is set down on it, and what it is zoned for, in the map's own frame
/// (<see cref="OsmFrame"/>). Axes are the engine's: x east and y south, from the map's north-west corner.
/// </summary>
/// <remarks>
/// <b>A road's lanes are the map's counts</b>, each <see cref="CityGenFigures.TracedLaneWidthM"/> wide — as many as its
/// measured width holds where OSM only assumes a count, and the roadside that width holds beside them (<see cref="Read"/>).
/// What is decided here too is that a way whose every lane runs against it is turned round, so
/// <see cref="SurveyWay.Oneway"/> always means the way its points run, and that a way running on past the map is cut
/// where it crosses the map's own edge, each run inside a way of its own and each cut one of the places a road leaves
/// the map (<see cref="LeavesTheMap"/>).
/// </remarks>
internal sealed class Survey
{
    public required string Name { get; init; }

    /// <summary>What every draw laying the town is keyed on (<see cref="TownMap.Seed"/>).</summary>
    public required ulong Seed { get; init; }

    public required float WidthM { get; init; }

    public required float HeightM { get; init; }

    /// <summary>
    /// Every point of the map and every point a way is cut at the map's edge, flat as x, y pairs. <b>Two ways
    /// sharing a point share an index</b>, which is how OSM says they meet; two that merely cross are not joined.
    /// </summary>
    public required float[] PointsM { get; init; }

    public required SurveyWay[] Ways { get; init; }

    /// <summary>
    /// <b>Where a way runs on off the map</b>, by point: the place it crosses the map's edge, or the point it was
    /// surveyed through a shortest road short of that, which then stands for it (<see cref="Inside"/>). Empty for a
    /// survey laid by hand with none.
    /// </summary>
    public bool[] LeavesTheMap { get; init; } = [];

    public bool Leaves(int point) => (uint)point < (uint)LeavesTheMap.Length && LeavesTheMap[point];

    /// <summary>
    /// The water, as closed rings flat as x, y pairs: the sea already cut to the map's edge, then every water the map
    /// holds whole.
    /// </summary>
    public required float[][] Sea { get; init; }

    /// <summary>The waters the map draws along a course (<see cref="TownMap.Courses"/>), drawn as their banks stand off them.</summary>
    public TownMap.CourseArrays Courses { get; init; } = TownMap.CourseArrays.None;

    /// <summary>Whether any of the water is a river, which a road laid by a zone may span (GEN-14b).</summary>
    public bool Bridgeable { get; init; }

    /// <summary>
    /// What the map is zoned for (<see cref="TownMap.Zones"/>), which everything not fixed is laid by — none for a survey laid
    /// by hand, which is laid as the whole map and nothing else (<see cref="ZonesOrWhole"/>).
    /// </summary>
    public TownMap.ZoneArrays Zones { get; init; } = TownMap.ZoneArrays.None;

    /// <summary>Its zones, or the whole map alone where it holds none.</summary>
    public TownMap.ZoneArrays ZonesOrWhole =>
        Zones.Count > 0 ? Zones : TownMap.ZoneArrays.Whole(new Vector2(WidthM, HeightM), ZoneKind.Town, []);

    /// <summary>The buildings the map sets down (<see cref="TownMap.Buildings"/>).</summary>
    public TownMap.StoodArrays Buildings { get; init; } = TownMap.StoodArrays.None;

    /// <summary>The props the map sets down (<see cref="TownMap.Props"/>).</summary>
    public TownMap.PropArrays Props { get; init; } = TownMap.PropArrays.None;

    /// <summary>The car parks the map sets down (<see cref="TownMap.Lots"/>).</summary>
    public TownMap.LotArrays Lots { get; init; } = TownMap.LotArrays.None;

    public int PointCount => PointsM.Length / 2;

    public Vector2 PointM(int point) => new(PointsM[2 * point], PointsM[(2 * point) + 1]);

    public static Survey Of(TownMap map, SimConfig config)
    {
        var mostTagged = MostTagged(map.Roads);

        var frame = map.Frame;
        var (widthM, heightM) = (frame.WidthM, frame.HeightM);
        var points = map.PointM.Length;
        var placedM = new List<Vector2D>(map.PointM);

        // A way OSM draws on past the map is laid up to the map's own edge, and cut there.
        var cut = new Rectangle(0, 0, widthM, heightM);
        var ways = new List<SurveyWay>(map.Roads.Length);
        var leaving = new List<int>();
        foreach (var road in map.Roads)
        {
            var read = Read(road, mostTagged, config);
            foreach (var run in Inside(read.Points, placedM, points, cut, config.CityGen.TracedShortestRoadM, leaving))
            {
                ways.Add(read with { Points = run });
            }
        }

        Noded(ways, placedM, leaving);

        var leavesTheMap = new bool[placedM.Count];
        foreach (var point in leaving) leavesTheMap[point] = true;

        var pointsM = new float[2 * placedM.Count];
        for (var point = 0; point < placedM.Count; point++)
        {
            pointsM[2 * point] = (float)placedM[point].X;
            pointsM[(2 * point) + 1] = (float)placedM[point].Y;
        }

        // North-up off the south-west corner, which is the hand the coastline is closed in.
        var upM = new Vector2D[points];
        for (var point = 0; point < points; point++) upM[point] = new Vector2D(map.PointM[point].X, heightM - map.PointM[point].Y);

        var sea = Coastline.SeaRings(map.Coast, upM, widthM, heightM);
        var flatSea = new float[sea.Count + map.Waters.Count][];
        for (var ring = 0; ring < sea.Count; ring++)
        {
            flatSea[ring] = new float[2 * sea[ring].Count];
            for (var point = 0; point < sea[ring].Count; point++)
            {
                flatSea[ring][2 * point] = (float)sea[ring][point].X;
                flatSea[ring][(2 * point) + 1] = (float)(heightM - sea[ring][point].Y);
            }
        }

        for (var water = 0; water < map.Waters.Count; water++)
        {
            var outline = map.Waters.OutlineOf(water);
            var flat = flatSea[sea.Count + water] = new float[2 * outline.Length];
            for (var point = 0; point < outline.Length; point++) (flat[2 * point], flat[(2 * point) + 1]) = (outline[point].X, outline[point].Y);
        }

        return new Survey
        {
            Name = map.Name,
            Seed = map.Seed,
            WidthM = (float)widthM,
            HeightM = (float)heightM,
            PointsM = pointsM,
            Ways = [.. ways],
            LeavesTheMap = leavesTheMap,
            Sea = flatSea,
            Courses = map.Courses,
            Bridgeable = Array.IndexOf(map.Waters.Kind, TownMap.WaterBody.River) >= 0 || Array.IndexOf(map.Courses.Kind, TownMap.WaterBody.River) >= 0,
            Zones = map.Zones,
            Buildings = map.Buildings,
            Props = map.Props,
            Lots = map.Lots,
        };
    }

    /// <summary>How finely the ways' legs are filed when their crossings are looked for (<see cref="Noded"/>).</summary>
    const double NodedCellM = 50.0;

    /// <summary>
    /// <b>Two ways on the ground that cross between their points share a point where they cross</b>, put into both: the
    /// crossing a mapper drew with no node, which OSM's validators report as one highway intersecting another. Left so,
    /// one road's lanes run across the other's carriageway with nothing joining them, and its traffic drives through the
    /// other's as though it were not there.
    /// </summary>
    /// <remarks>
    /// <b>A bridge is on a level of its own</b> and crosses what is under it by being over it. A crossing within
    /// <see cref="LineTolerance.OnePlaceM"/> of a point either way already has takes that point; two ways that only touch
    /// at an end each, or run along one line, have nothing to cross at. A way ending on another's carriageway joins it too
    /// (<see cref="AlmostJoined"/>).
    /// </remarks>
    internal static void Noded(List<SurveyWay> ways, List<Vector2D> placedM, List<int> leaving)
    {
        var cells = new Dictionary<(long X, long Y), List<(int Way, int Leg)>>();
        for (var way = 0; way < ways.Count; way++)
        {
            if (ways[way].Bridge) continue;

            var points = ways[way].Points;
            for (var leg = 1; leg < points.Length; leg++)
            {
                var (fromM, toM) = (placedM[points[leg - 1]], placedM[points[leg]]);
                for (var x = Cell(Math.Min(fromM.X, toM.X)); x <= Cell(Math.Max(fromM.X, toM.X)); x++)
                {
                    for (var y = Cell(Math.Min(fromM.Y, toM.Y)); y <= Cell(Math.Max(fromM.Y, toM.Y)); y++)
                    {
                        if (!cells.TryGetValue((x, y), out var filed)) cells[(x, y)] = filed = [];
                        filed.Add((way, leg));
                    }
                }
            }
        }

        // Each way's crossings as (leg, share along it, point), put in once every pair has been asked.
        var crossings = new List<(int Leg, double Along, int Point)>[ways.Count];
        var seen = new HashSet<(int, int, int, int)>();
        foreach (var filed in cells.Values)
        {
            for (var one = 0; one < filed.Count; one++)
            {
                for (var other = one + 1; other < filed.Count; other++)
                {
                    var (a, b) = (filed[one], filed[other]);
                    if (a.Way == b.Way || !seen.Add((a.Way, a.Leg, b.Way, b.Leg))) continue;

                    var (pa, pb) = (ways[a.Way].Points, ways[b.Way].Points);
                    var (a0, a1, b0, b1) = (pa[a.Leg - 1], pa[a.Leg], pb[b.Leg - 1], pb[b.Leg]);
                    if (a0 == b0 || a0 == b1 || a1 == b0 || a1 == b1) continue;
                    if (!Crosses(placedM[a0], placedM[a1], placedM[b0], placedM[b1], out var alongA, out var alongB)) continue;

                    var atM = new Vector2D(
                        placedM[a0].X + ((placedM[a1].X - placedM[a0].X) * alongA), placedM[a0].Y + ((placedM[a1].Y - placedM[a0].Y) * alongA));
                    var point = Nearest(atM, a0, a1, b0, b1);
                    if (point < 0)
                    {
                        point = placedM.Count;
                        placedM.Add(atM);
                    }

                    if (point != a0 && point != a1) (crossings[a.Way] ??= []).Add((a.Leg, alongA, point));
                    if (point != b0 && point != b1) (crossings[b.Way] ??= []).Add((b.Leg, alongB, point));
                }
            }
        }

        AlmostJoined(ways, placedM, cells, crossings, leaving);

        for (var way = 0; way < ways.Count; way++)
        {
            if (crossings[way] is not { } added) continue;

            added.Sort((one, other) => one.Leg != other.Leg ? one.Leg.CompareTo(other.Leg) : one.Along.CompareTo(other.Along));
            var points = ways[way].Points;
            var noded = new List<int>(points.Length + added.Count) { points[0] };
            var next = 0;
            for (var leg = 1; leg < points.Length; leg++)
            {
                for (; next < added.Count && added[next].Leg == leg; next++)
                {
                    if (noded[^1] != added[next].Point) noded.Add(added[next].Point);
                }

                if (noded[^1] != points[leg]) noded.Add(points[leg]);
            }

            ways[way] = ways[way] with { Points = [.. noded] };
        }

        int Nearest(Vector2D atM, params ReadOnlySpan<int> ends)
        {
            foreach (var end in ends)
            {
                if (Math.Sqrt(((placedM[end].X - atM.X) * (placedM[end].X - atM.X)) + ((placedM[end].Y - atM.Y) * (placedM[end].Y - atM.Y))) <
                    LineTolerance.OnePlaceM)
                {
                    return end;
                }
            }

            return -1;
        }

        static long Cell(double atM) => (long)Math.Floor(atM / NodedCellM);
    }

    /// <summary>
    /// <b>A way ending on another's carriageway joins it there</b>: an end no other way shares, short of a way on the
    /// ground by no more than the two carriageways' half-widths — so the end's own tarmac already stands on the other's —
    /// is put into that way at the nearest place along it. OSM's validators report it as an almost-junction; left so, a
    /// dead end is laid butting into the road beside it, its lanes ending on the other's and joined to nothing.
    /// </summary>
    /// <remarks>
    /// <b>None at a place the way leaves the map</b>, which is the map's edge and not an end, nor on a bridge, nor onto
    /// the way's own legs.
    /// </remarks>
    static void AlmostJoined(
        List<SurveyWay> ways, List<Vector2D> placedM, Dictionary<(long X, long Y), List<(int Way, int Leg)>> cells,
        List<(int Leg, double Along, int Point)>?[] crossings, List<int> leaving)
    {
        var uses = new Dictionary<int, int>();
        foreach (var way in ways)
        {
            foreach (var point in way.Points) uses[point] = uses.GetValueOrDefault(point) + 1;
        }

        foreach (var added in crossings)
        {
            if (added is null) continue;

            foreach (var (_, _, point) in added) uses[point] = uses.GetValueOrDefault(point) + 1;
        }

        var leaves = new HashSet<int>(leaving);
        for (var way = 0; way < ways.Count; way++)
        {
            if (ways[way].Bridge) continue;

            foreach (var atStart in (ReadOnlySpan<bool>)[true, false])
            {
                var points = ways[way].Points;
                var end = atStart ? points[0] : points[^1];
                if (uses[end] > 1 || leaves.Contains(end)) continue;

                var endM = placedM[end];
                var (best, bestLeg, bestAlong, bestM) = (-1, 0, 0.0, double.MaxValue);
                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (!cells.TryGetValue((Cell(endM.X) + dx, Cell(endM.Y) + dy), out var filed)) continue;

                    foreach (var (other, leg) in filed)
                    {
                        if (other == way) continue;

                        var reachM = (ways[way].CarriagewayM + ways[other].CarriagewayM) * 0.5;
                        var (fromM, toM) = (placedM[ways[other].Points[leg - 1]], placedM[ways[other].Points[leg]]);
                        var (runX, runY) = (toM.X - fromM.X, toM.Y - fromM.Y);
                        var lengthSq = (runX * runX) + (runY * runY);
                        if (lengthSq <= 0.0) continue;

                        var along = Math.Clamp((((endM.X - fromM.X) * runX) + ((endM.Y - fromM.Y) * runY)) / lengthSq, 0.0, 1.0);
                        var (offX, offY) = (fromM.X + (runX * along) - endM.X, fromM.Y + (runY * along) - endM.Y);
                        var apartM = Math.Sqrt((offX * offX) + (offY * offY));
                        if (apartM <= reachM && apartM < bestM) (best, bestLeg, bestAlong, bestM) = (other, leg, along, apartM);
                    }
                }

                if (best < 0) continue;

                // The end moved onto the other's line, a point of the other's where it lands on one, a point of its own
                // put into the other's leg where it does not: the other's line keeps its course and the end comes to it.
                var otherPoints = ways[best].Points;
                int onto;
                if (bestAlong <= 0.0) onto = otherPoints[bestLeg - 1];
                else if (bestAlong >= 1.0) onto = otherPoints[bestLeg];
                else
                {
                    var (fromM, toM) = (placedM[otherPoints[bestLeg - 1]], placedM[otherPoints[bestLeg]]);
                    onto = placedM.Count;
                    placedM.Add(new Vector2D(fromM.X + ((toM.X - fromM.X) * bestAlong), fromM.Y + ((toM.Y - fromM.Y) * bestAlong)));
                    (crossings[best] ??= []).Add((bestLeg, bestAlong, onto));
                }

                var joined = (int[])points.Clone();
                if (atStart) joined[0] = onto;
                else joined[^1] = onto;
                ways[way] = ways[way] with { Points = joined };
                uses[onto] = uses.GetValueOrDefault(onto) + 1;
            }
        }

        static long Cell(double atM) => (long)Math.Floor(atM / NodedCellM);
    }

    /// <summary>Where two straights cross, as a share of each along it, where they cross between their ends.</summary>
    static bool Crosses(Vector2D a0, Vector2D a1, Vector2D b0, Vector2D b1, out double alongA, out double alongB)
    {
        var (runA, runB) = (new Vector2D(a1.X - a0.X, a1.Y - a0.Y), new Vector2D(b1.X - b0.X, b1.Y - b0.Y));
        var across = (runA.X * runB.Y) - (runA.Y * runB.X);
        (alongA, alongB) = (0.0, 0.0);
        if (Math.Abs(across) < 1e-12) return false;

        var apart = new Vector2D(b0.X - a0.X, b0.Y - a0.Y);
        alongA = ((apart.X * runB.Y) - (apart.Y * runB.X)) / across;
        alongB = ((apart.X * runA.Y) - (apart.Y * runA.X)) / across;
        return alongA is >= 0.0 and <= 1.0 && alongB is >= 0.0 and <= 1.0;
    }

    /// <summary>The rectangle a traced map lays its roads in, in the engine's own axes.</summary>
    readonly record struct Rectangle(double Left, double Top, double Right, double Bottom)
    {
        public bool Holds(Vector2D atM) => atM.X >= Left && atM.X <= Right && atM.Y >= Top && atM.Y <= Bottom;
    }

    /// <summary>
    /// <b>A way's runs inside the rectangle</b>: the way itself where it stays inside, and each stretch of it
    /// between two places it crosses the edge, with a point added at every crossing, on the edge itself. A way wholly
    /// outside has none. Each run's end at a crossing goes into <paramref name="leaving"/>.
    /// </summary>
    /// <remarks>
    /// <b>A crossing nearer the node inside it than the shortest road a traced map lays is that node</b>
    /// (<see cref="CityGenFigures.TracedShortestRoadM"/>), and a run shorter than that is none: a cut is a place a
    /// road ends, and one a few centimetres past a junction would be a road too short to lay between the two.
    /// </remarks>
    static List<int[]> Inside(int[] points, List<Vector2D> placedM, int nodes, Rectangle cut, float shortestM, List<int> leaving)
    {
        var runs = new List<int[]>();
        List<int>? run = cut.Holds(placedM[points[0]]) ? [points[0]] : null;
        for (var at = 1; at < points.Length; at++)
        {
            var (fromM, toM) = (placedM[points[at - 1]], placedM[points[at]]);
            if (run is not null && cut.Holds(toM))
            {
                run.Add(points[at]);
                continue;
            }

            if (!Crossing(fromM, toM, cut, out var enters, out var leaves)) continue;

            if (run is null)
            {
                run = [];
                Join(run, Added(fromM, toM, enters));
            }

            if (cut.Holds(toM))
            {
                Join(run, points[at]);
                continue;
            }

            Join(run, Added(fromM, toM, leaves));
            Finish(run);
            run = null;
        }

        if (run is not null) Finish(run);
        return runs;

        void Finish(List<int> run)
        {
            var (entered, left) = (run[0] >= nodes, run[^1] >= nodes);
            if (run.Count > 2 && entered && Apart(run[0], run[1]) < shortestM) run.RemoveAt(0);
            if (run.Count > 2 && left && Apart(run[^1], run[^2]) < shortestM) run.RemoveAt(run.Count - 1);

            var lengthM = 0.0;
            for (var at = 1; at < run.Count; at++) lengthM += Apart(run[at - 1], run[at]);
            if (run.Count < 2 || lengthM < shortestM) return;

            runs.Add([.. run]);
            if (entered) leaving.Add(run[0]);
            if (left) leaving.Add(run[^1]);
        }

        double Apart(int one, int other) =>
            Math.Sqrt(Math.Pow(placedM[one].X - placedM[other].X, 2) + Math.Pow(placedM[one].Y - placedM[other].Y, 2));

        // Held on the edge: a rounding's step past it is a road standing off the map (GEN-2b).
        int Added(Vector2D fromM, Vector2D toM, double share)
        {
            placedM.Add(new Vector2D(
                Math.Clamp(fromM.X + ((toM.X - fromM.X) * share), cut.Left, cut.Right),
                Math.Clamp(fromM.Y + ((toM.Y - fromM.Y) * share), cut.Top, cut.Bottom)));
            return placedM.Count - 1;
        }

        // A crossing standing on the point beside it is that point.
        void Join(List<int> run, int point)
        {
            if (run.Count == 0 || placedM[run[^1]] != placedM[point]) run.Add(point);
        }
    }

    /// <summary>
    /// Where a straight crosses into and out of the rectangle, as shares of its length (Liang–Barsky), or false
    /// where it never stands inside it.
    /// </summary>
    static bool Crossing(Vector2D fromM, Vector2D toM, Rectangle cut, out double enters, out double leaves)
    {
        (enters, leaves) = (0.0, 1.0);
        var (acrossM, downM) = (toM.X - fromM.X, toM.Y - fromM.Y);
        ReadOnlySpan<(double Toward, double RoomM)> sides =
        [
            (-acrossM, fromM.X - cut.Left), (acrossM, cut.Right - fromM.X), (-downM, fromM.Y - cut.Top), (downM, cut.Bottom - fromM.Y),
        ];
        foreach (var (toward, roomM) in sides)
        {
            if (toward == 0.0)
            {
                if (roomM < 0.0) return false;

                continue;
            }

            var share = roomM / toward;
            if (toward < 0.0) enters = Math.Max(enters, share);
            else leaves = Math.Min(leaves, share);
        }

        return enters <= leaves;
    }

    /// <summary>
    /// <b>One road way with its lanes as OSM counts them, each <see cref="CityGenFigures.TracedLaneWidthM"/> wide, and
    /// the roadside its measured width holds beside them</b>, turned round where every lane runs against it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every lane is one width</b>, whatever OSM tags or assumes a lane is: what a mapper's widths and a survey's
    /// measurements differ by lane to lane is how far the sources disagree, and a street's lanes are painted alike.
    /// Where OSM places the way off the middle, it stands off the middle of these lanes by as much of them.
    /// </para>
    /// <para>
    /// <b>What a measured width holds past its lanes is roadside</b> — the parked cars and the gutters imagery reads
    /// as road — in strips of <see cref="CityGenFigures.TracedRoadsideWidthM"/>, as many as the rest holds to the
    /// nearest strip and no more than one a kerb, <b>a single one beside the kerb the traffic along the way keeps
    /// to</b>. None is laid on a bridge or round a roundabout, which nothing parks on. <b>A width that would leave a lane
    /// narrower than <see cref="CityGenFigures.TracedNarrowestLaneM"/> or wider than
    /// <see cref="CityGenFigures.TracedWidestLaneM"/> is not taken</b> — it read half a pair of carriageways, or a
    /// square or a yard the way runs through — and the lanes are laid with no roadside.
    /// </para>
    /// <para>
    /// <b>Where OSM assumes the lane count, the measured width says it</b> — as many lanes each way as it holds past a
    /// roadside at either kerb, shared evenly between the ways it is driven, never fewer than OSM assumes and never
    /// more than any way of its class on the map is tagged with. A way with any lane's turn, change or bus entry
    /// tagged keeps OSM's count, its entries being counted lane by lane.
    /// </para>
    /// </remarks>
    static SurveyWay Read(TracedRoad road, Dictionary<string, int> mostTagged, SimConfig config)
    {
        var points = (int[])road.Points.Clone();
        var (forward, backward, shared) = (road.LanesForward, road.LanesBackward, road.LanesShared);
        var laneM = config.CityGen.TracedLaneWidthM;
        var roadsideM = config.CityGen.TracedRoadsideWidthM;
        var edged = !road.Bridge && !road.Roundabout;
        var roadsides = 0;
        if (road.WidthM is { } widthM)
        {
            var (f, b, s) = !road.LanesTagged && !road.Marked
                ? Held(forward, backward, shared, widthM - (edged ? 2f * roadsideM : 0f), laneM, mostTagged.GetValueOrDefault(road.Highway))
                : (forward, backward, shared);
            var shareM = widthM / (f + b + s);
            if (shareM >= config.CityGen.TracedNarrowestLaneM && shareM <= config.CityGen.TracedWidestLaneM)
            {
                (forward, backward, shared) = (f, b, s);
                if (edged) roadsides = Math.Clamp((int)MathF.Round((widthM - ((f + b + s) * laneM)) / roadsideM), 0, 2);
            }
        }

        var lanesM = (forward + backward + shared) * laneM;
        var centreOffsetM = road.CentreOffsetShare * lanesM;
        var turned = forward == 0 && shared == 0;
        if (turned)
        {
            Array.Reverse(points);
            (forward, backward) = (backward, forward);
            centreOffsetM = -centreOffsetM;
        }

        // Turned first: a single roadside is beside the kerb the traffic keeps to, which is the way's points' only
        // once they run with it.
        var (alongM, againstM) = (roadsides >= 1 ? roadsideM : 0f, roadsides == 2 ? roadsideM : 0f);

        return new SurveyWay
        {
            Turned = turned,
            OsmId = road.OsmId,
            Highway = road.Highway,
            Bridge = road.Bridge,
            Roundabout = road.Roundabout,
            LanesForward = forward,
            LanesBackward = backward,
            LanesShared = shared,
            CarriagewayM = lanesM + alongM + againstM,
            RoadsideAlongM = alongM,
            RoadsideAgainstM = againstM,
            CentreOffsetM = centreOffsetM,
            Points = points,
        };
    }

    /// <summary>
    /// The most lanes one way any way of each class is tagged with on the map: what a width read off the ground may
    /// make of an untagged way of that class at most (<see cref="Read"/>).
    /// </summary>
    static Dictionary<string, int> MostTagged(TracedRoad[] roads)
    {
        var most = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var road in roads)
        {
            if (road.LanesTagged) most[road.Highway] = Math.Max(most.GetValueOrDefault(road.Highway), Math.Max(road.LanesForward, road.LanesBackward));
        }

        return most;
    }

    /// <summary>
    /// The lanes a width holds, as (along the way, against it, both ways) — each way it is driven given as many lanes
    /// as its share holds, between what OSM assumes and the most its class is tagged with (<see cref="Read"/>). A
    /// single lane both ways share becomes a lane each way where the width holds two.
    /// </summary>
    static (int Forward, int Backward, int Shared) Held(int forward, int backward, int shared, float widthM, float laneM, int mostTagged)
    {
        var oneWay = (forward == 0 || backward == 0) && shared == 0;

        // A width a float's rounding short of a whole number of lanes holds them: it is a sum of widths read back.
        var each = Math.Min(mostTagged, (int)((MathF.Max(0f, widthM) + LineTolerance.RoundingM) / ((oneWay ? 1 : 2) * laneM)));
        if (oneWay) return forward > 0 ? (Math.Max(forward, each), 0, 0) : (0, Math.Max(backward, each), 0);
        if (forward == 0 && backward == 0) return each >= 1 ? (each, each, 0) : (0, 0, shared);

        return (Math.Max(forward, each), Math.Max(backward, each), shared);
    }
}

/// <summary>
/// One road way as this engine reads it, or the run of one inside the map: its lanes as the map counts them
/// (<see cref="TracedRoad"/>), turned where every lane runs against the way OSM draws it.
/// </summary>
internal sealed record SurveyWay
{
    /// <summary>Whether its points run against the way OSM draws it, every lane running that way.</summary>
    public bool Turned { get; init; }

    /// <summary>The OSM way it was read off, to look it up or fetch it again by.</summary>
    public long OsmId { get; init; }

    /// <summary>OSM's own <c>highway</c> value: <c>primary</c>, <c>residential</c>, <c>trunk_link</c>.</summary>
    public required string Highway { get; init; }

    /// <summary>Whether it is driven one way only, which is then the way its points run.</summary>
    public bool Oneway => LanesBackward == 0 && LanesShared == 0;

    public bool Bridge { get; init; }

    /// <summary>Whether it is a roundabout's circulating carriageway, or a piece of one.</summary>
    public bool Roundabout { get; init; }

    /// <summary>The lanes running the way the points do.</summary>
    public required int LanesForward { get; init; }

    /// <summary>The lanes running against them.</summary>
    public required int LanesBackward { get; init; }

    /// <summary>The lanes driven both ways: a single lane two-way traffic shares, or a centre turning lane.</summary>
    public required int LanesShared { get; init; }

    /// <summary>
    /// Kerb to kerb: every lane at <see cref="CityGenFigures.TracedLaneWidthM"/> and the roadside either side of them
    /// (<see cref="RoadsideAlongM"/>, <see cref="RoadsideAgainstM"/>).
    /// </summary>
    public required float CarriagewayM { get; init; }

    /// <summary>
    /// The roadside between the lanes and the kerb the traffic along the way's points keeps to — the one side a
    /// width holding a single roadside lays it — or nought.
    /// </summary>
    public float RoadsideAlongM { get; init; }

    /// <summary>And between the lanes and the other kerb.</summary>
    public float RoadsideAgainstM { get; init; }

    /// <summary>How far the carriageway's middle stands off the way's points, to the right of them as they run.</summary>
    public required float CentreOffsetM { get; init; }

    /// <summary>Indices into <see cref="Survey.PointsM"/>, in the way's own order.</summary>
    public required int[] Points { get; init; }
}
