using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A real place read into this engine's terms</b> (GEN-57): every road way of a <see cref="TracedMap"/>, each as its
/// points, its <c>highway</c> value and its lanes as the rules make them of OSM's, and the sea its coastline closes
/// against the map's edge — in the map's own frame (<see cref="OsmFrame"/>). Axes are the engine's: x east and y
/// south, from the map's north-west corner.
/// </summary>
/// <remarks>
/// <para>
/// <b>No tag of a lane is read here</b>: a road's lanes are its carriageway as the map holds it
/// (<see cref="OsmCarriageway"/>), taken as they are — as wide as the place was measured, and as many as that width
/// holds where OSM only assumes a count (<see cref="Read"/>). What is decided here is that a way whose every lane runs
/// against it is turned round, so <see cref="SurveyWay.Oneway"/> always means the way its points run, and that a way
/// running on past the map is cut where it crosses the map's own edge, each run inside a way of its own and each cut
/// one of the places a road leaves the map (<see cref="LeavesTheMap"/>).
/// </para>
/// <para>
/// <b>It carries what else is known of the place</b>: the control at each junction's point, the pedestrian crossings,
/// the buildings' footprints and the trees, each in this frame already.
/// </para>
/// </remarks>
internal sealed class Survey
{
    public required string Name { get; init; }

    /// <summary>The OSM boundary relation whose roads drew the map, which is the map's own number.</summary>
    public required long Relation { get; init; }

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

    /// <summary>The sea, as closed rings flat as x, y pairs, already cut to the map's edge.</summary>
    public required float[][] Sea { get; init; }

    /// <summary>
    /// What OSM forbids a car at a node and which lanes it joins there, as the scanner read them
    /// (<see cref="OsmTurns"/>): a way by its OSM id (<see cref="SurveyWay.OsmId"/>), and a node by its index, which
    /// is its point's.
    /// </summary>
    public OsmTurns Turns { get; init; } = OsmTurns.None;

    /// <summary>
    /// <b>What controls the junction at each point</b>, by the point's index, as the map has it
    /// (<see cref="TracedMap.Controls"/>) — <see cref="SurveyControl.Unsigned"/> where nothing does, or past the end.
    /// </summary>
    public PointControl[] Controls { get; init; } = [];

    /// <summary>Every pedestrian crossing the map holds over its roads (<see cref="TracedMap.Crossings"/>).</summary>
    public SurveyCrossing[] Crossings { get; init; } = [];

    /// <summary>Every building's footprint the map holds (<see cref="TracedMap.Footprints"/>).</summary>
    public CityPlan.FootprintArrays Footprints { get; init; } = CityPlan.FootprintArrays.None;

    /// <summary>Every tree the map holds (<see cref="TracedMap.TreeM"/>).</summary>
    public Vector2[] TreeM { get; init; } = [];

    public int PointCount => PointsM.Length / 2;

    public Vector2 PointM(int point) => new(PointsM[2 * point], PointsM[(2 * point) + 1]);

    public SurveyControl ControlAt(int point) => (uint)point < (uint)Controls.Length ? Controls[point].Control : SurveyControl.Unsigned;

    /// <remarks>
    /// A crossing is <b>painted where its tags say, and where they say nothing, if it has lights for its walkers</b>,
    /// which in Ukraine are marked as a rule.
    /// </remarks>
    public static Survey Of(TracedMap map, SimConfig config)
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
        var flatSea = new float[sea.Count][];
        for (var ring = 0; ring < flatSea.Length; ring++)
        {
            flatSea[ring] = new float[2 * sea[ring].Count];
            for (var point = 0; point < sea[ring].Count; point++)
            {
                flatSea[ring][2 * point] = (float)sea[ring][point].X;
                flatSea[ring][(2 * point) + 1] = (float)(heightM - sea[ring][point].Y);
            }
        }

        var controls = new PointControl[placedM.Count];
        for (var at = 0; at < map.Controls.Count; at++) controls[map.Controls.Point[at]] = new PointControl(map.Controls.Control[at], map.Controls.Cluster[at]);

        var crossings = new SurveyCrossing[map.Crossings.Count];
        for (var at = 0; at < crossings.Length; at++)
        {
            var kind = map.Crossings.Kind[at];
            var junction = map.Crossings.Junction[at];
            crossings[at] = new SurveyCrossing(
                map.Crossings.Way[at], map.Crossings.AtM[at], kind, map.Crossings.Painted[at] ?? kind == SurveyCrossingKind.Signals,
                junction == TracedMap.NoJunction ? CityPlan.NoRecord : junction);
        }

        var footprints = map.Footprints;
        return new Survey
        {
            Name = map.Name,
            Relation = map.Relation,
            WidthM = (float)widthM,
            HeightM = (float)heightM,
            PointsM = pointsM,
            Ways = [.. ways],
            LeavesTheMap = leavesTheMap,
            Sea = flatSea,
            Turns = map.Turns,
            Controls = controls,
            Crossings = crossings,
            Footprints = new CityPlan.FootprintArrays
            {
                RingOffsets = footprints.RingOffsets,
                Rings = new CityPlan.RingArrays { Offsets = footprints.PointOffsets, PointM = footprints.PointM },
                Traced = footprints.Traced,
                HeightM = footprints.HeightM,
                Use = footprints.Use,
            },
            TreeM = map.TreeM,
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
            var carriageway = road.Carriageway;
            if (carriageway.LanesFrom != OsmLanesFrom.Tagged) continue;

            var oneWay = Math.Max(carriageway.Count(OsmLaneWay.Forward), carriageway.Count(OsmLaneWay.Backward));
            most[road.Highway] = Math.Max(most.GetValueOrDefault(road.Highway), oneWay);
        }

        return most;
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
    /// to</b>. None is laid on a bridge or round a roundabout, which nothing parks on. A width is taken where it was
    /// measured on the way itself — its tag, or the surface OSM outlines it with — or read off imagery under a
    /// street, and where OSM gives no width lane by lane nor places the way off the middle. Imagery is not taken under
    /// a service road, a track or a link: a yard's paving or a slip road's merge reads as their width. <b>A width that
    /// would leave a lane narrower than <see cref="CityGenFigures.TracedNarrowestLaneM"/> or wider than
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
        var carriageway = road.Carriageway;
        var highway = road.Highway;
        var points = (int[])road.Points.Clone();
        var forward = carriageway.Count(OsmLaneWay.Forward);
        var backward = carriageway.Count(OsmLaneWay.Backward);
        var shared = carriageway.Count(OsmLaneWay.Both);
        var laneM = config.CityGen.TracedLaneWidthM;
        var roadsideM = config.CityGen.TracedRoadsideWidthM;
        var edged = !road.Bridge && !road.Roundabout;
        var roadsides = 0;
        MeasuredFrom? widthFrom = null;
        var lanesFromWidth = false;
        if (road.Measured is { } read && Measurable(carriageway, highway, read.From))
        {
            var (f, b, s) = carriageway.LanesFrom == OsmLanesFrom.Assumed && !road.Marked
                ? Held(forward, backward, shared, read.WidthM - (edged ? 2f * roadsideM : 0f), laneM, mostTagged.GetValueOrDefault(highway))
                : (forward, backward, shared);
            var shareM = read.WidthM / (f + b + s);
            if (shareM >= config.CityGen.TracedNarrowestLaneM && shareM <= config.CityGen.TracedWidestLaneM)
            {
                lanesFromWidth = (f, b, s) != (forward, backward, shared);
                (forward, backward, shared, widthFrom) = (f, b, s, read.From);
                if (edged) roadsides = Math.Clamp((int)MathF.Round((read.WidthM - ((f + b + s) * laneM)) / roadsideM), 0, 2);
            }
        }

        var lanesM = (forward + backward + shared) * laneM;
        var centreOffsetM = carriageway.WidthM > 0f ? carriageway.CentreOffsetM * lanesM / carriageway.WidthM : 0f;
        var turned = forward == 0 && shared == 0;
        var arrows = carriageway.Lanes.Any(lane => lane.Arrows != OsmArrows.None)
            ? carriageway.Lanes.Select(lane => lane.Arrows).ToArray()
            : [];
        if (turned)
        {
            Array.Reverse(points);
            Array.Reverse(arrows);
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
            Highway = highway,
            Bridge = road.Bridge,
            Tunnel = road.Tunnel,
            Roundabout = road.Roundabout,
            LanesForward = forward,
            LanesBackward = backward,
            LanesShared = shared,
            CarriagewayM = lanesM + alongM + againstM,
            RoadsideAlongM = alongM,
            RoadsideAgainstM = againstM,
            WidthFrom = widthFrom,
            LanesFromWidth = lanesFromWidth,
            CentreOffsetM = centreOffsetM,
            Arrows = arrows,
            Points = points,
        };
    }

    /// <summary>Whether a width measured so is taken for this carriageway (<see cref="Read"/>).</summary>
    static bool Measurable(OsmCarriageway carriageway, string highway, MeasuredFrom from) =>
        carriageway.WidthFrom != OsmWidthFrom.Lanes && carriageway.CentreOffsetM == 0f
        && (from != MeasuredFrom.Imagery || TracedStreets.Rank(highway) > 0);

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

/// <summary>What controls the junction at one point, and the junctions it is controlled with as one (<see cref="TracedMap.Controls"/>).</summary>
internal readonly record struct PointControl(SurveyControl Control, long Cluster);

/// <summary>
/// One pedestrian crossing a survey maps (<see cref="TracedMap.Crossings"/>): the road way it crosses by its OSM id,
/// where it stands, its kind, whether it is painted, and the point of the junction whose arm it is on, or
/// <see cref="CityPlan.NoRecord"/> where it is struck mid-block.
/// </summary>
internal readonly record struct SurveyCrossing(long Way, Vector2 AtM, SurveyCrossingKind Kind, bool Painted, int Junction);

/// <summary>
/// One road way as this engine reads it, or the run of one inside the map: its lanes as OSM means them
/// (<see cref="OsmCarriageway"/>), turned where every lane runs against the way OSM draws it.
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

    public bool Tunnel { get; init; }

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

    /// <summary>Where the carriageway was measured, or null where it is OSM's lanes alone.</summary>
    public MeasuredFrom? WidthFrom { get; init; }

    /// <summary>Whether its lanes are as many as its measured width holds rather than as OSM assumes.</summary>
    public bool LanesFromWidth { get; init; }

    /// <summary>How far the carriageway's middle stands off the way's points, to the right of them as they run.</summary>
    public required float CentreOffsetM { get; init; }

    /// <summary>
    /// The arrows on every lane (<see cref="OsmLane.Arrows"/>), left to right looking along the way's points — the
    /// lanes against them first, then any driven both ways, then those with them — and empty where none has any.
    /// </summary>
    public OsmArrows[] Arrows { get; init; } = [];

    /// <summary>Indices into <see cref="Survey.PointsM"/>, in the way's own order.</summary>
    public required int[] Points { get; init; }
}
