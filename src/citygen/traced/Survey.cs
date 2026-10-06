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
