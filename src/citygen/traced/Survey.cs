using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A real place read into this engine's metres</b> (GEN-57): every road way of an <see cref="OsmExtract"/>,
/// each as its points, its <c>highway</c> value and its lanes as OSM means them, and the sea its coastline closes
/// against the map's edge — placed 1:1 in the extract's own frame (<see cref="OsmFrame"/>). Axes are the
/// engine's: x east and y south, from the map's north-west corner.
/// </summary>
/// <remarks>
/// <b>No tag is read here</b>: a road is a way the scanner gave lanes (<see cref="OsmCarriageway"/>), of whatever
/// class, and its lanes are taken as they are. What is decided here is that a way whose every lane runs against
/// it is turned round, so <see cref="SurveyWay.Oneway"/> always means the way its points run, and that a way
/// running on past the map is cut where it crosses the rectangle inside the frame's margin
/// (<see cref="OsmFrame.MarginM"/>), each run inside a way of its own.
/// </remarks>
internal sealed class Survey
{
    public required string Name { get; init; }

    /// <summary>The OSM boundary relation whose roads drew the map, which is the map's own number.</summary>
    public required long Relation { get; init; }

    public required float WidthM { get; init; }

    public required float HeightM { get; init; }

    /// <summary>
    /// Every node of the extract and every point a way is cut at the map's edge, flat as x, y pairs. <b>Two ways
    /// sharing a point share an index</b>, which is how OSM says they meet; two that merely cross are not joined.
    /// </summary>
    public required float[] PointsM { get; init; }

    public required SurveyWay[] Ways { get; init; }

    /// <summary>The sea, as closed rings flat as x, y pairs, already cut to the map's edge.</summary>
    public required float[][] Sea { get; init; }

    /// <summary>
    /// What OSM forbids a car at a node and which lanes it joins there, as the scanner read them
    /// (<see cref="OsmTurns"/>): a way by its OSM id (<see cref="SurveyWay.OsmId"/>), and a node by its index, which
    /// is its point's.
    /// </summary>
    public OsmTurns Turns { get; init; } = OsmTurns.None;

    public int PointCount => PointsM.Length / 2;

    public Vector2 PointM(int point) => new(PointsM[2 * point], PointsM[(2 * point) + 1]);

    public static Survey Of(OsmExtract extract, SimConfig config)
    {
        var nodes = extract.Nodes;
        var roads = new List<OsmWay>();
        foreach (var way in extract.Ways)
        {
            if (way.Carriageway is not null) roads.Add(way);
        }

        var frame = extract.Frame;
        var (widthM, heightM) = (frame.WidthM, frame.HeightM);
        var projection = frame.Projection();

        // North-up off the south-west corner, which is the hand the coastline is closed in.
        var upM = new Vector2D[nodes.Id.Length];
        var placedM = new List<Vector2D>(upM.Length);
        for (var node = 0; node < upM.Length; node++)
        {
            var (eastM, northM) = projection.Project(nodes.LatDeg(node), nodes.LonDeg(node));
            upM[node] = new Vector2D(eastM - frame.WestM, northM - frame.SouthM);
            placedM.Add(new Vector2D(upM[node].X, heightM - upM[node].Y));
        }

        // A way OSM draws on past the map is laid as far as the rectangle inside its margin, and cut there.
        var cut = new Rectangle(frame.MarginM, frame.MarginM, widthM - frame.MarginM, heightM - frame.MarginM);
        var ways = new List<SurveyWay>(roads.Count);
        foreach (var road in roads)
        {
            var read = Read(road);
            foreach (var run in Inside(read.Points, placedM, upM.Length, cut, config.CityGen.TracedShortestRoadM))
            {
                ways.Add(read with { Points = run });
            }
        }

        var pointsM = new float[2 * placedM.Count];
        for (var point = 0; point < placedM.Count; point++)
        {
            pointsM[2 * point] = (float)placedM[point].X;
            pointsM[(2 * point) + 1] = (float)placedM[point].Y;
        }

        var sea = Coastline.SeaRings(extract, upM, widthM, heightM);
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

        return new Survey
        {
            Name = extract.Name,
            Relation = extract.Source.Relation,
            WidthM = (float)widthM,
            HeightM = (float)heightM,
            PointsM = pointsM,
            Ways = [.. ways],
            Sea = flatSea,
            Turns = extract.Turns,
        };
    }

    /// <summary>The rectangle a traced map lays its roads in, in the engine's own axes.</summary>
    readonly record struct Rectangle(double Left, double Top, double Right, double Bottom)
    {
        public bool Holds(Vector2D atM) => atM.X >= Left && atM.X <= Right && atM.Y >= Top && atM.Y <= Bottom;
    }

    /// <summary>
    /// <b>A way's runs inside the rectangle</b>: the way itself where it stays inside, and each stretch of it
    /// between two places it crosses the edge, with a point added at every crossing. A way wholly outside has none.
    /// </summary>
    /// <remarks>
    /// <b>A crossing nearer the node inside it than the shortest road a traced map lays is that node</b>
    /// (<see cref="CityGenFigures.TracedShortestRoadM"/>), and a run shorter than that is none: a cut is a place a
    /// road ends, and one a few centimetres past a junction would be a road too short to lay between the two.
    /// </remarks>
    static List<int[]> Inside(int[] points, List<Vector2D> placedM, int nodes, Rectangle cut, float shortestM)
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
            if (run.Count > 2 && run[0] >= nodes && Apart(run[0], run[1]) < shortestM) run.RemoveAt(0);
            if (run.Count > 2 && run[^1] >= nodes && Apart(run[^1], run[^2]) < shortestM) run.RemoveAt(run.Count - 1);

            var lengthM = 0.0;
            for (var at = 1; at < run.Count; at++) lengthM += Apart(run[at - 1], run[at]);
            if (run.Count > 1 && lengthM >= shortestM) runs.Add([.. run]);
        }

        double Apart(int one, int other) =>
            Math.Sqrt(Math.Pow(placedM[one].X - placedM[other].X, 2) + Math.Pow(placedM[one].Y - placedM[other].Y, 2));

        int Added(Vector2D fromM, Vector2D toM, double share)
        {
            placedM.Add(new Vector2D(fromM.X + ((toM.X - fromM.X) * share), fromM.Y + ((toM.Y - fromM.Y) * share)));
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

    /// <summary>One road way with its lanes as OSM means them, turned round where every lane runs against it.</summary>
    static SurveyWay Read(OsmWay way)
    {
        var carriageway = way.Carriageway!;
        var points = (int[])way.Nodes.Clone();
        var forward = carriageway.Count(OsmLaneWay.Forward);
        var backward = carriageway.Count(OsmLaneWay.Backward);
        var shared = carriageway.Count(OsmLaneWay.Both);
        var centreOffsetM = carriageway.CentreOffsetM;
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

        return new SurveyWay
        {
            Turned = turned,
            OsmId = way.Id,
            Highway = way.Tags["highway"],
            Bridge = (way.Tag("bridge") ?? "no") != "no",
            Tunnel = (way.Tag("tunnel") ?? "no") != "no",
            LanesForward = forward,
            LanesBackward = backward,
            LanesShared = shared,
            CarriagewayM = carriageway.WidthM,
            CentreOffsetM = centreOffsetM,
            Arrows = arrows,
            Points = points,
        };
    }
}

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

    /// <summary>The lanes running the way the points do.</summary>
    public required int LanesForward { get; init; }

    /// <summary>The lanes running against them.</summary>
    public required int LanesBackward { get; init; }

    /// <summary>The lanes driven both ways: a single lane two-way traffic shares, or a centre turning lane.</summary>
    public required int LanesShared { get; init; }

    /// <summary>Every lane's width together.</summary>
    public required float CarriagewayM { get; init; }

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

/// <summary>A place in metres held in doubles, for the arithmetic done before a place is a float.</summary>
internal readonly record struct Vector2D(double X, double Y);
