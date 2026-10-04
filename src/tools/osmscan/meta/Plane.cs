using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>A place on the map in metres, x east and y south of its north-west corner (<see cref="OsmFrame"/>).</summary>
internal readonly record struct Pt(double X, double Y)
{
    public static Pt operator +(Pt a, Pt b) => new(a.X + b.X, a.Y + b.Y);

    public static Pt operator -(Pt a, Pt b) => new(a.X - b.X, a.Y - b.Y);

    public static Pt operator *(Pt a, double k) => new(a.X * k, a.Y * k);

    public double Length => Math.Sqrt((X * X) + (Y * Y));

    public static double Dot(Pt a, Pt b) => (a.X * b.X) + (a.Y * b.Y);

    /// <summary>Positive where <paramref name="b"/> turns clockwise off <paramref name="a"/> on the map, which is
    /// to the right as a traveller sees it, the frame's y running south.</summary>
    public static double Cross(Pt a, Pt b) => (a.X * b.Y) - (a.Y * b.X);
}

internal readonly record struct Box(double MinX, double MinY, double MaxX, double MaxY)
{
    public static readonly Box Empty = new(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);

    public Box With(Pt at) => new(Math.Min(MinX, at.X), Math.Min(MinY, at.Y), Math.Max(MaxX, at.X), Math.Max(MaxY, at.Y));

    public Box Grown(double byM) => new(MinX - byM, MinY - byM, MaxX + byM, MaxY + byM);

    public bool Holds(Pt at) => at.X >= MinX && at.X <= MaxX && at.Y >= MinY && at.Y <= MaxY;

    public bool Meets(Box other) => other.MinX <= MaxX && other.MaxX >= MinX && other.MinY <= MaxY && other.MaxY >= MinY;

    public static Box Of(ReadOnlySpan<Pt> points)
    {
        var box = Empty;
        foreach (var at in points) box = box.With(at);
        return box;
    }
}

/// <summary>
/// <b>The extract's own frame</b>: an OSM place in 1e-7° read into the map's metres exactly as the engine reads a
/// node (<see cref="OsmFrame.Place"/>), so a layer and the roads it is laid against stand in one frame.
/// </summary>
internal sealed class Plane(OsmFrame frame)
{
    readonly TransverseMercator _projection = frame.Projection();

    public OsmFrame Frame => frame;

    public Pt At(int lat, int lon) => AtDeg(lat / OsmNodes.UnitsPerDegree, lon / OsmNodes.UnitsPerDegree);

    Pt AtDeg(double latDeg, double lonDeg)
    {
        var (eastM, northM) = _projection.Project(latDeg, lonDeg);
        return new Pt(eastM - frame.WestM, frame.HeightM - (northM - frame.SouthM));
    }

    /// <summary>
    /// A place worked out on the map — a crossing of two ways, a hull's corner — back in 1e-7° as lat, lon, by
    /// Newton's method on the projection itself, so it reads back to the same place to well under a millimetre.
    /// </summary>
    public int[] Degrees(Pt at)
    {
        const double StepDeg = 1e-6;
        var (lat, lon) = (frame.Lat0Deg, frame.Lon0Deg);
        for (var round = 0; round < 8; round++)
        {
            var placed = AtDeg(lat, lon);
            var miss = at - placed;
            if (miss.Length < 1e-5) break;

            var (alongLat, alongLon) = (AtDeg(lat + StepDeg, lon) - placed, AtDeg(lat, lon + StepDeg) - placed);
            var determinant = Pt.Cross(alongLat, alongLon);
            lat += Pt.Cross(miss, alongLon) / determinant * StepDeg;
            lon += Pt.Cross(alongLat, miss) / determinant * StepDeg;
        }

        return [(int)Math.Round(lat * OsmNodes.UnitsPerDegree), (int)Math.Round(lon * OsmNodes.UnitsPerDegree)];
    }

    /// <summary>A geometry of lat, lon pairs as places.</summary>
    public Pt[] Line(int[] geometry)
    {
        var line = new Pt[geometry.Length / 2];
        for (var at = 0; at < line.Length; at++) line[at] = At(geometry[2 * at], geometry[(2 * at) + 1]);
        return line;
    }

    public bool OnMap(Pt at) => at.X >= 0 && at.Y >= 0 && at.X <= frame.WidthM && at.Y <= frame.HeightM;
}

/// <summary>Plane geometry over polylines and rings of <see cref="Pt"/>.</summary>
internal static class Shape
{
    public static double Length(ReadOnlySpan<Pt> line)
    {
        var lengthM = 0.0;
        for (var at = 1; at < line.Length; at++) lengthM += (line[at] - line[at - 1]).Length;
        return lengthM;
    }

    /// <summary>A ring's area by the shoelace; positive where it winds clockwise on the map.</summary>
    public static double SignedArea(ReadOnlySpan<Pt> ring)
    {
        var twice = 0.0;
        for (var at = 0; at < ring.Length; at++)
        {
            var (a, b) = (ring[at], ring[(at + 1) % ring.Length]);
            twice += (a.X * b.Y) - (b.X * a.Y);
        }

        return twice * 0.5;
    }

    /// <summary>A ring's centre of area, which an average of its corners is not where one side is drawn in more of them.</summary>
    public static Pt Centroid(ReadOnlySpan<Pt> ring)
    {
        var (area, x, y) = (0.0, 0.0, 0.0);
        for (var at = 0; at < ring.Length; at++)
        {
            var (a, b) = (ring[at], ring[(at + 1) % ring.Length]);
            var cross = (a.X * b.Y) - (b.X * a.Y);
            (area, x, y) = (area + cross, x + ((a.X + b.X) * cross), y + ((a.Y + b.Y) * cross));
        }

        return Math.Abs(area) < 1e-9 ? ring[0] : new Pt(x / (3 * area), y / (3 * area));
    }

    /// <summary>The area of outer rings less inner ones, however each winds.</summary>
    public static double Area(Pt[][] outer, Pt[][] inner) =>
        Math.Max(0, outer.Sum(ring => Math.Abs(SignedArea(ring))) - inner.Sum(ring => Math.Abs(SignedArea(ring))));

    /// <summary>Whether a place stands inside a set of rings by the even-odd rule, which a multipolygon's holes and
    /// islands read right without knowing which ring is which.</summary>
    public static bool Inside(Pt at, Pt[][] rings)
    {
        var inside = false;
        foreach (var ring in rings)
        {
            for (int a = 0, b = ring.Length - 1; a < ring.Length; b = a++)
            {
                if ((ring[a].Y > at.Y) != (ring[b].Y > at.Y)
                    && at.X < ((ring[b].X - ring[a].X) * (at.Y - ring[a].Y) / (ring[b].Y - ring[a].Y)) + ring[a].X)
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    /// <summary>The nearest place on a polyline: how far off it, how far along it, and on which segment.</summary>
    public static (double OffM, double AlongM, int Segment, Pt Foot) Nearest(Pt at, ReadOnlySpan<Pt> line)
    {
        var best = (OffM: double.MaxValue, AlongM: 0.0, Segment: 0, Foot: line[0]);
        var alongM = 0.0;
        for (var segment = 0; segment + 1 < line.Length; segment++)
        {
            var (a, b) = (line[segment], line[segment + 1]);
            var run = b - a;
            var lengthM = run.Length;
            var t = lengthM > 0 ? Math.Clamp(Pt.Dot(at - a, run) / (lengthM * lengthM), 0, 1) : 0;
            var foot = a + (run * t);
            var offM = (at - foot).Length;
            if (offM < best.OffM) best = (offM, alongM + (t * lengthM), segment, foot);
            alongM += lengthM;
        }

        return best;
    }

    /// <summary>The place a distance along a polyline, held to its ends.</summary>
    public static Pt Along(ReadOnlySpan<Pt> line, double alongM)
    {
        if (alongM <= 0) return line[0];

        for (var at = 1; at < line.Length; at++)
        {
            var lengthM = (line[at] - line[at - 1]).Length;
            if (alongM <= lengthM && lengthM > 0) return line[at - 1] + ((line[at] - line[at - 1]) * (alongM / lengthM));
            alongM -= lengthM;
        }

        return line[^1];
    }

    /// <summary>
    /// Where two segments cross strictly inside both, if they do: the place and how far along each, as a share of
    /// it. Segments that only touch at an end, or run along each other, do not cross.
    /// </summary>
    public static bool Crosses(Pt a, Pt b, Pt c, Pt d, out Pt at, out double alongAb, out double alongCd)
    {
        const double End = 1e-9;
        var (r, s) = (b - a, d - c);
        var denominator = Pt.Cross(r, s);
        (at, alongAb, alongCd) = (default, 0, 0);
        if (Math.Abs(denominator) < 1e-12) return false;

        alongAb = Pt.Cross(c - a, s) / denominator;
        alongCd = Pt.Cross(c - a, r) / denominator;
        if (alongAb <= End || alongAb >= 1 - End || alongCd <= End || alongCd >= 1 - End) return false;

        at = a + (r * alongAb);
        return true;
    }

    /// <summary>The bearing from one place to another in degrees clockwise from north, the frame's y running south.</summary>
    public static double BearingDeg(Pt from, Pt to)
    {
        var bearing = double.RadiansToDegrees(Math.Atan2(to.X - from.X, from.Y - to.Y));
        return bearing < 0 ? bearing + 360 : bearing;
    }

    /// <summary>The turn from one bearing onto another, in degrees: positive to the right, in (−180, 180].</summary>
    public static double TurnDeg(double fromDeg, double toDeg)
    {
        var turn = (toDeg - fromDeg) % 360;
        if (turn > 180) turn -= 360;
        if (turn <= -180) turn += 360;
        return turn;
    }

    /// <summary>The convex hull, counter-clockwise on the map (Andrew's monotone chain).</summary>
    public static Pt[] Hull(IEnumerable<Pt> points)
    {
        var sorted = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
        if (sorted.Length < 3) return sorted;

        var hull = new Pt[2 * sorted.Length];
        var k = 0;
        foreach (var p in sorted)
        {
            while (k >= 2 && Pt.Cross(hull[k - 1] - hull[k - 2], p - hull[k - 2]) <= 0) k--;
            hull[k++] = p;
        }

        for (int at = sorted.Length - 2, lower = k + 1; at >= 0; at--)
        {
            var p = sorted[at];
            while (k >= lower && Pt.Cross(hull[k - 1] - hull[k - 2], p - hull[k - 2]) <= 0) k--;
            hull[k++] = p;
        }

        return hull[..(k - 1)];
    }
}

/// <summary>
/// <b>A bucket grid over the map</b>: an item filed under every cell its box meets, so what stands near a place is
/// found by the cells round it rather than by every item.
/// </summary>
internal sealed class Grid(double cellM)
{
    readonly Dictionary<long, List<int>> _cells = [];

    public void Add(int item, Box box)
    {
        var (x0, y0, x1, y1) = Cells(box);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var key = Key(x, y);
                if (!_cells.TryGetValue(key, out var held)) _cells[key] = held = [];
                held.Add(item);
            }
        }
    }

    /// <summary>Every item filed under a cell the box meets, each once, into a set the caller clears.</summary>
    public void Near(Box box, HashSet<int> into)
    {
        var (x0, y0, x1, y1) = Cells(box);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (_cells.TryGetValue(Key(x, y), out var held)) into.UnionWith(held);
            }
        }
    }

    (int X0, int Y0, int X1, int Y1) Cells(Box box) =>
        ((int)Math.Floor(box.MinX / cellM), (int)Math.Floor(box.MinY / cellM), (int)Math.Floor(box.MaxX / cellM), (int)Math.Floor(box.MaxY / cellM));

    static long Key(int x, int y) => ((long)x << 32) | (uint)y;
}

/// <summary>
/// <b>A surface OSM outlines</b>: a closed way, or a multipolygon relation's member ways joined into rings — its
/// places in OSM's units and in the map's metres.
/// </summary>
internal sealed class Outline
{
    public required Element Element { get; init; }

    /// <summary>Outer rings as lat, lon pairs, each closed on its first place.</summary>
    public required int[][] Outer { get; init; }

    public required int[][] Inner { get; init; }

    /// <summary>Every ring in metres, outer and inner alike, for the even-odd rule.</summary>
    public required Pt[][] RingsM { get; init; }

    public required Box Bounds { get; init; }

    public required double AreaM2 { get; init; }

    public bool Holds(Pt at) => Bounds.Holds(at) && Shape.Inside(at, RingsM);

    /// <summary>
    /// The outline of a closed way, or of a relation whose members carry geometry; null for an open way or a
    /// relation whose rings do not close.
    /// </summary>
    public static Outline? Of(Element element, Plane plane)
    {
        int[][] outer, inner;
        if (element.Type == 'w')
        {
            if (!element.Closed) return null;
            (outer, inner) = ([element.Geometry], []);
        }
        else if (element.Type == 'r')
        {
            outer = [.. Rings(element.Members.Where(m => m.Type == 'w' && m.Role is "outer" or "" && m.Geometry.Length >= 4).Select(m => m.Geometry))];
            inner = [.. Rings(element.Members.Where(m => m.Type == 'w' && m.Role == "inner" && m.Geometry.Length >= 4).Select(m => m.Geometry))];
            if (outer.Length == 0) return null;
        }
        else
        {
            return null;
        }

        var outerM = outer.Select(plane.Line).ToArray();
        var innerM = inner.Select(plane.Line).ToArray();
        var bounds = Box.Empty;
        foreach (var ring in outerM) bounds = ring.Aggregate(bounds, (box, at) => box.With(at));
        return new Outline
        {
            Element = element,
            Outer = outer,
            Inner = inner,
            RingsM = [.. outerM, .. innerM],
            Bounds = bounds,
            AreaM2 = Shape.Area(outerM, innerM),
        };
    }

    /// <summary>
    /// Ways joined end to end into closed rings, each turned round where it must be to meet the next; a chain that
    /// never closes is left out, as OSM's own renderers leave it.
    /// </summary>
    static List<int[]> Rings(IEnumerable<int[]> ways)
    {
        var rings = new List<int[]>();
        var open = new List<int[]>();
        foreach (var way in ways)
        {
            if (way.Length >= 8 && way[0] == way[^2] && way[1] == way[^1]) rings.Add(way);
            else open.Add(way);
        }

        while (open.Count > 0)
        {
            var chain = new List<int>(open[^1]);
            open.RemoveAt(open.Count - 1);
            var joined = true;
            while (joined && !(chain[0] == chain[^2] && chain[1] == chain[^1]))
            {
                joined = false;
                for (var at = 0; at < open.Count; at++)
                {
                    var next = open[at];
                    if (next[0] == chain[^2] && next[1] == chain[^1]) chain.AddRange(next.Skip(2));
                    else if (next[^2] == chain[^2] && next[^1] == chain[^1]) chain.AddRange(Reversed(next).Skip(2));
                    else continue;

                    open.RemoveAt(at);
                    joined = true;
                    break;
                }
            }

            if (chain.Count >= 8 && chain[0] == chain[^2] && chain[1] == chain[^1]) rings.Add([.. chain]);
        }

        return rings;
    }

    static int[] Reversed(int[] geometry)
    {
        var reversed = new int[geometry.Length];
        for (var at = 0; at < geometry.Length / 2; at++)
        {
            reversed[2 * at] = geometry[geometry.Length - (2 * at) - 2];
            reversed[(2 * at) + 1] = geometry[geometry.Length - (2 * at) - 1];
        }

        return reversed;
    }
}
