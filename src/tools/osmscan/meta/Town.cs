using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>The survey's roads as a network every layer is laid against</b>: each node in metres, each road way's line
/// and length, where every way meets a node, and a grid of road segments to find the road beside a place.
/// </summary>
/// <remarks>
/// A road is a way the scanner gave lanes (<see cref="OsmWay.Carriageway"/>), as the engine reads it; a road
/// drawn as an area and the coastline are not. <b>A node's arms are the ways leaving it</b>: a way passing through
/// is two, one ending there is one, so a junction is a node of three or more and a dead end a node of one.
/// </remarks>
internal sealed class Town
{
    /// <summary>The grid's cell: about the widest a carriageway and its pavements are, so a road beside a place
    /// is in the cells round it.</summary>
    const double CellM = 40;

    public Town(OsmExtract extract, Plane plane, Fetched fetched, Sources sources)
    {
        (Extract, Plane, Fetched, Sources) = (extract, plane, fetched, sources);
        var nodes = extract.Nodes;
        NodeM = new Pt[nodes.Id.Length];
        NodeIndex = new Dictionary<long, int>(nodes.Id.Length);
        for (var node = 0; node < NodeM.Length; node++)
        {
            NodeM[node] = plane.At(nodes.Lat[node], nodes.Lon[node]);
            NodeIndex[nodes.Id[node]] = node;
        }

        Roads = [.. extract.Ways.Where(way => way.Carriageway is not null)];
        RoadIndex = new Dictionary<long, int>(Roads.Length);
        RoadLineM = new Pt[Roads.Length][];
        RoadLengthM = new double[Roads.Length];
        Uses = new List<(int Road, int At)>?[NodeM.Length];
        Segments = [];
        var segments = new List<(int Road, int At)>();
        RoadGrid = new Grid(CellM);
        for (var road = 0; road < Roads.Length; road++)
        {
            RoadIndex[Roads[road].Id] = road;
            var way = Roads[road].Nodes;
            RoadLineM[road] = [.. way.Select(node => NodeM[node])];
            RoadLengthM[road] = Shape.Length(RoadLineM[road]);
            for (var at = 0; at < way.Length; at++)
            {
                (Uses[way[at]] ??= []).Add((road, at));
                if (at + 1 == way.Length) continue;

                RoadGrid.Add(segments.Count, Box.Empty.With(NodeM[way[at]]).With(NodeM[way[at + 1]]));
                segments.Add((road, at));
            }
        }

        Segments = [.. segments];
        foreach (var tagged in extract.NodeTags) NodeTags[tagged.Node] = tagged.Tags;
    }

    public OsmExtract Extract { get; }

    public Plane Plane { get; }

    public Fetched Fetched { get; }

    public Sources Sources { get; }

    /// <summary>Every survey node's place, by its index into <see cref="OsmExtract.Nodes"/>.</summary>
    public Pt[] NodeM { get; }

    public Dictionary<long, int> NodeIndex { get; }

    public OsmWay[] Roads { get; }

    public Dictionary<long, int> RoadIndex { get; }

    public Pt[][] RoadLineM { get; }

    public double[] RoadLengthM { get; }

    /// <summary>Every road a node is on, and where on it; null for a node no road passes.</summary>
    public List<(int Road, int At)>?[] Uses { get; }

    /// <summary>Every road segment, as its road and its first node's place on it, filed in <see cref="RoadGrid"/>.</summary>
    public (int Road, int At)[] Segments { get; }

    public Grid RoadGrid { get; }

    public Dictionary<int, Dictionary<string, string>> NodeTags { get; } = [];

    public long NodeId(int node) => Extract.Nodes.Id[node];

    /// <summary>
    /// A family's way's places with each node the survey holds where the survey has it — the survey's corrections may
    /// move one a tram track or a footway shares with a road (<see cref="Corrections"/>) — and the rest as OSM's.
    /// </summary>
    public int[] Surveyed(Element way)
    {
        if (way.Nodes.Length * 2 != way.Geometry.Length) return way.Geometry;

        int[]? placed = null;
        for (var at = 0; at < way.Nodes.Length; at++)
        {
            if (!NodeIndex.TryGetValue(way.Nodes[at], out var node)) continue;

            var (lat, lon) = (Extract.Nodes.Lat[node], Extract.Nodes.Lon[node]);
            if (lat == way.Geometry[2 * at] && lon == way.Geometry[(2 * at) + 1]) continue;

            placed ??= (int[])way.Geometry.Clone();
            (placed[2 * at], placed[(2 * at) + 1]) = (lat, lon);
        }

        return placed ?? way.Geometry;
    }

    public string Highway(int road) => Roads[road].Tags["highway"];

    /// <summary>Every arm leaving a node: a road, the node's place on it, and +1 along the way as drawn or −1 against it.</summary>
    public List<Arm> Arms(int node)
    {
        var arms = new List<Arm>();
        if (Uses[node] is not { } uses) return arms;

        foreach (var (road, at) in uses)
        {
            if (at > 0) arms.Add(new Arm(road, at, -1));
            if (at + 1 < Roads[road].Nodes.Length) arms.Add(new Arm(road, at, +1));
        }

        return arms;
    }

    public int ArmCount(int node)
    {
        if (Uses[node] is not { } uses) return 0;

        var count = 0;
        foreach (var (road, at) in uses) count += (at > 0 ? 1 : 0) + (at + 1 < Roads[road].Nodes.Length ? 1 : 0);
        return count;
    }

    public bool IsJunction(int node) => ArmCount(node) >= 3;

    /// <summary>How far along its road a node's place is, in metres from the way's first node.</summary>
    public double AlongM(int road, int at)
    {
        var line = RoadLineM[road];
        var alongM = 0.0;
        for (var k = 1; k <= at; k++) alongM += (line[k] - line[k - 1]).Length;
        return alongM;
    }

    /// <summary>
    /// The road nearest a place within a reach, among those a filter lets through: which, how far off, how far
    /// along it, and on which segment.
    /// </summary>
    public (int Road, double OffM, double AlongM, int Segment)? NearestRoad(Pt at, double withinM, Func<int, bool>? which = null)
    {
        var near = new HashSet<int>();
        RoadGrid.Near(Box.Empty.With(at).Grown(withinM), near);
        (int Road, double OffM, double AlongM, int Segment)? best = null;
        foreach (var segment in near)
        {
            var (road, first) = Segments[segment];
            if (which is not null && !which(road)) continue;

            var line = RoadLineM[road];
            var (offM, alongM, _, _) = Shape.Nearest(at, line.AsSpan(first, 2));
            if (offM > withinM || (best is { } held && offM >= held.OffM)) continue;

            best = (road, offM, AlongM(road, first) + alongM, first);
        }

        return best;
    }

    /// <summary>
    /// Whether a road may be driven by a car, its most specific motor vehicle access tag read over the general one;
    /// <c>private</c> and <c>destination</c> roads may, a bus or bus guideway may not.
    /// </summary>
    public bool CarsMay(int road)
    {
        var tags = Roads[road].Tags;
        if (tags["highway"] is "busway" or "bus_guideway") return false;

        foreach (var key in (string[])["motorcar", "motor_vehicle", "vehicle", "access"])
        {
            if (tags.TryGetValue(key, out var value)) return value is not ("no" or "agricultural" or "forestry" or "emergency" or "psv" or "bus");
        }

        return true;
    }

    /// <summary>Whether a car may drive a road along the way as drawn (+1) or against it (−1), by its lanes.</summary>
    public bool Driven(int road, int direction)
    {
        foreach (var lane in Roads[road].Carriageway!.Lanes)
        {
            if (lane.Way == OsmLaneWay.Both || lane.Way == (direction > 0 ? OsmLaneWay.Forward : OsmLaneWay.Backward)) return true;
        }

        return false;
    }
}

/// <summary>
/// <b>One way leaving a node</b>: a road, the node's place on it, and the way it leaves — +1 along the way as drawn,
/// −1 against it.
/// </summary>
internal readonly record struct Arm(int Road, int At, int Direction)
{
    /// <summary>Whether a car may come into the node along this arm: against the arm's own direction.</summary>
    public bool In(Town town) => town.CarsMay(Road) && town.Driven(Road, -Direction);

    public bool Out(Town town) => town.CarsMay(Road) && town.Driven(Road, Direction);

    /// <summary>Whether the way ends at the node, which is where its <c>turn:lanes</c> arrows are for.</summary>
    public bool Ends(Town town) => Direction < 0 ? At == town.Roads[Road].Nodes.Length - 1 : At == 0;

    /// <summary>The bearing the arm leaves on, read at a place a reach along it so a kink at the node is passed over.</summary>
    public double BearingDeg(Town town, double reachM)
    {
        var line = town.RoadLineM[Road];
        var from = line[At];
        var gone = 0.0;
        var to = line[At + Direction];
        for (var k = At; k + Direction >= 0 && k + Direction < line.Length; k += Direction)
        {
            var step = (line[k + Direction] - line[k]).Length;
            to = line[k + Direction];
            if (gone + step >= reachM)
            {
                to = line[k] + ((line[k + Direction] - line[k]) * ((reachM - gone) / step));
                break;
            }

            gone += step;
        }

        return Shape.BearingDeg(from, to);
    }

    /// <summary>
    /// Every node met walking the arm from its node, nearest first, with how far each is and the road, place on it
    /// and direction it was met walking: the way's own, then on across any node where it runs straight on into one
    /// other way, up to a reach. A junction or a dead end is the last met.
    /// </summary>
    public IEnumerable<(int Node, double AtM, Arm Along)> Walk(Town town, double reachM)
    {
        var (road, at, direction) = (Road, At, Direction);
        var gone = 0.0;
        var seen = new HashSet<int>();
        while (true)
        {
            var nodes = town.Roads[road].Nodes;
            var line = town.RoadLineM[road];
            for (var k = at + direction; k >= 0 && k < nodes.Length; k += direction)
            {
                gone += (line[k] - line[k - direction]).Length;
                if (gone > reachM || !seen.Add(nodes[k])) yield break;

                yield return (nodes[k], gone, new Arm(road, k, direction));
                if (town.ArmCount(nodes[k]) != 2) yield break;
            }

            var end = direction > 0 ? nodes[^1] : nodes[0];
            var next = town.Arms(end).Where(arm => arm.Road != road).ToArray();
            if (next.Length != 1) yield break;

            (road, at, direction) = next[0];
        }
    }
}
