using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The roads between districts</b> (GEN-67, GEN-69): one along every edge two districts share, links across every
/// barrier two districts face each other over — a bridge over a river, a level crossing over a railway, a crossroads over
/// an intercity road — and a road in from an intercity road, or across a district from one that reaches it, to every
/// district no other road between districts brings to one; each laid as a district road, and as wide as the demand over
/// it says.
/// </summary>
/// <remarks>
/// <para>
/// <b>A district is reached two separate ways</b> (GEN-69): from the intercity network, over the district roads round it
/// that run on to it, and from each of its neighbours, over the road along their shared edge or a link across the barrier
/// between them — never only out onto an intercity road and back. <b>So a link ends on its district's own edge road where
/// the district has one</b>, and the roads between districts are one network the intercity roads meet; a district that
/// network still leaves out is given a road of its own from the intercity road it fronts.
/// </para>
/// <para>
/// <b>An intercity road's junctions are planned before any is placed</b> (GEN-68), so every road meeting one is laid to a
/// junction it may have, and none is laid toward one it may not.
/// </para>
/// <para>
/// <b>A barrier is crossed where its own crossings stand</b> (<see cref="Crossings"/>), each square and short (GEN-70), as
/// often as the edge two districts share along it is long (<see cref="CityGenFigures.DistrictLinksApartM"/>) and at least
/// once — and a river parting built-up districts is crossed often enough that no two of its bridges stand further apart
/// than a town crosses its water (<see cref="CityGenFigures.BridgesApartMostM"/>).
/// </para>
/// </remarks>
internal sealed class TownLinks(TownGen.Authored town, Paths paths, int[] intercity, SimConfig config)
{
    /// <summary>
    /// <b>One end of a planned road</b>: on a road between districts planned before it, at a place along it; on an
    /// intercity road at one of its planned junctions; or neither — inside its district, on the district's own lattice.
    /// </summary>
    readonly record struct End(int Onto = -1, float OntoAlongM = 0f, int Road = -1, float RoadAlongM = 0f)
    {
        /// <summary>An end that meets nothing laid before it: on its district's lattice.</summary>
        public static End None => new(Onto: -1, Road: -1);
    }

    /// <summary>
    /// One road between districts to be laid: the two districts it joins (the one, twice, for a road in to a district from
    /// an intercity road), its line, the crossings it takes, what each of its ends meets, and whether it runs along an edge
    /// two districts share — which is run on to the intercity road it ends against rather than ended short of it.
    /// </summary>
    sealed record Planned(int A, int B, Polyline Line, List<Crossing> Crossed, End First, End Last, bool Seam);

    readonly Partition _partition = town.Partition;
    readonly float _streetM = TownGen.CorridorM(config, 1);

    /// <summary>
    /// How near the way it came in a link holds, as the cosine off it: <b>no further off square to the road it leaves than
    /// that road's own arms let a junction take</b> (GEN-13), with the margin laying it takes off
    /// (<see cref="SimConfig.LinkArmsApartMinRad"/>) — a link leaning nearer is planned and then refused when laid.
    /// </summary>
    readonly float _inwardLeast = MathF.Sin(config.LinkArmsApartMinRad);

    /// <summary>How near two roads leaving one junction lie, as the cosine between them: no nearer than its arms are let (GEN-13).</summary>
    readonly float _armsApartCos = MathF.Cos(config.LinkArmsApartMinRad);

    /// <summary>Each intercity road's junctions as planned, by how far along it each stands.</summary>
    readonly List<float>[] _stations = [.. intercity.Select(path => paths.All[path].Nodes.Select(node => node.AlongM).ToList())];

    /// <summary>
    /// <b>Whether the intercity roads are met only at interchanges</b> (GEN-68): a bypassed town's, which nothing on them
    /// stops for. Every other town's meet the roads between districts at junctions on them.
    /// </summary>
    readonly bool _interchanged = town.Connectors.BypassM > 0f;

    /// <summary>
    /// Each interchange, by the intercity road and the junction planned on it: its terminal on the road's right and its left,
    /// where a road has been laid to one.
    /// </summary>
    readonly Dictionary<(int Road, float StationM), Vector2?[]> _interchanges = [];

    /// <summary>
    /// The junctions planned on each intercity road for a road to meet it, as against the nodes it stood with — its ends
    /// and its bridgeheads — which an interchange keeps only the junctions' own spacing from (GEN-68).
    /// </summary>
    readonly List<float>[] _interchangesAt = [.. intercity.Select(_ => new List<float>())];

    /// <summary>Where on each intercity road a road between districts is carried over it (GEN-68).</summary>
    readonly List<float>[] _overAt = [.. intercity.Select(_ => new List<float>())];

    /// <summary>
    /// <b>The interchanges that cross</b> (GEN-68): a road carried over the intercity road between their terminals and both
    /// ways' slip roads, so what they reach is left and come back to both ways — each the first a piece of the roads between
    /// districts is given (<see cref="RoadsIn"/>), and one on every intercity road (<see cref="Interchanges"/>).
    /// </summary>
    readonly HashSet<(int Road, float StationM)> _crossing = [];

    public void Lay()
    {
        // <b>In the order they can be laid</b>: what a road ends on is laid before it, so the edges first, the roads in from
        // the intercity roads next, and the links over the water and the railway onto those.
        var planned = new List<Planned>();
        Seams(planned);
        var seams = planned.Count;
        OverTheRoads(planned);
        RoadsIn(planned, seams);
        Bridged(planned);
        LevelCrossed(planned);
        RoadsIn(planned, seams);
        FarSides(planned);

        var lanes = Lanes(planned);
        var pathOf = new int[planned.Count];
        for (var at = 0; at < planned.Count; at++) pathOf[at] = Add(planned, at, lanes[at], pathOf);
        Interchanges();
    }

    /// <summary>
    /// <b>A road in on an interchange's far side too</b> (GEN-68, GEN-69): an access serves only the way on its own side, so
    /// one planned from one side is given one on the other wherever a district fronts it, and both ways have a way off and
    /// on there.
    /// </summary>
    void FarSides(List<Planned> planned)
    {
        if (!_interchanged) return;

        var reached = new Dictionary<(int Road, float StationM), bool[]>();
        foreach (var plan in planned)
        {
            foreach (var (end, inwardM) in (ReadOnlySpan<(End, Vector2)>)[
                         (plan.First, plan.Line.At(config.CityGen.InterchangeOffsetM)), (plan.Last, plan.Line.At(plan.Line.LengthM - config.CityGen.InterchangeOffsetM))])
            {
                if (end.Road < 0) continue;

                var key = (end.Road, end.RoadAlongM);
                if (!reached.TryGetValue(key, out var sides)) reached[key] = sides = [false, false];
                sides[SideOf(paths.All[intercity[end.Road]].Line, end.RoadAlongM, inwardM)] = true;
            }
        }

        foreach (var ((road, stationM), sides) in reached)
        {
            var line = paths.All[intercity[road]].Line;
            var atM = line.At(stationM);
            for (var side = 0; side < sides.Length; side++)
            {
                if (sides[side]) continue;

                var outward = Heading.RightOf(line.Heading(stationM)) * (side == 0 ? 1f : -1f);
                var district = _partition.At(atM + (outward * (VergeM(road) + _streetM + (config.CityGen.FrontCells * _partition.Grid.CellM))));
                if (district < 0) continue;

                var (last, inM) = Into(planned, district, atM, outward);
                if (!ArmsClear(planned, road, stationM, atM, Vector2.Normalize(inM - atM))) continue;

                planned.Add(new Planned(district, district, new Polyline([atM, inM]), [], new End(Road: road, RoadAlongM: stationM), last, Seam: false));
            }
        }
    }

    /// <summary>
    /// <b>Every interchange laid</b> (GEN-68): at each terminal, <b>a slip road off the kerb lane of the way on that side and
    /// one back onto it</b> — off it short of the terminal and onto it past — so the town's road there meets that way and
    /// nothing crosses the intercity road; the way on the other side is met at the terminal there (<see cref="FarSides"/>),
    /// and a road between districts crosses the intercity road carried over it, meeting nothing (<see cref="OverTheRoads"/>).
    /// <b>Some interchanges cross</b> (<see cref="_crossing"/>): a road carried over the intercity road joins their two
    /// terminals, one of them the interchange's own where no road reached that side — so what they reach is left, and come
    /// back to, both ways.
    /// </summary>
    /// <remarks>
    /// <b>A slip road is two legs</b>: one leaving the intercity road at <see cref="CityGenFigures.SlipLeavesDeg"/> off its
    /// line where it leaves — found along a curving road rather than read off its bearing at the terminal — and one along the
    /// road into its terminal, so it arrives there square to the town's road. Where both sides have one, the two at each end
    /// leave from one node, the way against the road's onto it where the way along leaves it.
    /// </remarks>
    void Interchanges()
    {
        var legM = SlipLegM;

        // Every intercity road's ways are each reached where the other's slip roads meet it, so one interchange on it crosses.
        for (var road = 0; road < intercity.Length; road++)
        {
            var stations = _interchanges.Keys.Where(key => key.Road == road).ToList();
            if (stations.Count > 0 && !stations.Exists(_crossing.Contains)) _crossing.Add(stations[0]);
        }

        foreach (var ((road, stationM), terminals) in _interchanges)
        {
            var path = intercity[road];
            var line = paths.All[path].Line;
            var ahead = line.Heading(stationM);
            var crosses = _crossing.Contains((road, stationM)) || terminals[0] is null || terminals[1] is null;
            Terminal(road, stationM, 0);
            Terminal(road, stationM, 1);

            // The way driven along the line keeps to its right where the traffic does, and its slip roads stand on that side:
            // off it behind the terminal and onto it ahead, and the way against it the other way round.
            var (along, against) = config.RoadSideSign > 0f ? (terminals[0]!.Value, terminals[1]!.Value) : (terminals[1]!.Value, terminals[0]!.Value);
            var (offAlongM, onAlongM) = (along - (ahead * legM), along + (ahead * legM));
            var (offAgainstM, onAgainstM) = (against + (ahead * legM), against - (ahead * legM));

            // Both ways are met both off and onto them, or neither is: met by one alone, what the interchange reaches is left
            // by it alone.
            var behindM = Paired(Leaves(line, offAlongM, -1f), Leaves(line, onAgainstM, -1f));
            var aheadM = Paired(Leaves(line, onAlongM, 1f), Leaves(line, offAgainstM, 1f));
            if (SlipEnd(path, behindM, offAlongM, onAgainstM) is not { } behind || SlipEnd(path, aheadM, onAlongM, offAgainstM) is not { } aheadOn) continue;

            if (crosses) paths.Add(new Polyline([along, against]), RoadClass.Arterial, 1, [], over: true);

            paths.Add(new Polyline([behind, offAlongM, along]), RoadClass.Slip, 1, [], oneWay: true);
            paths.Add(new Polyline([along, onAlongM, aheadOn]), RoadClass.Slip, 1, [], oneWay: true);
            paths.Add(new Polyline([against, onAgainstM, behind]), RoadClass.Slip, 1, [], oneWay: true);
            paths.Add(new Polyline([aheadOn, offAgainstM, against]), RoadClass.Slip, 1, [], oneWay: true);
        }
    }

    /// <summary>
    /// <b>The leg a slip road runs along the intercity road into its terminal</b>: past the terminal's box and the turn off its
    /// shallow leg at a road between districts' own radius (GEN-47).
    /// </summary>
    float SlipLegM =>
        config.JunctionRadiusAcrossM(config.LaneWidthM * LayoutEdge.MostLanes * SimConfig.LanesPerCarriageway)
        + (2f * RoadLines.FloorRadiusM(config, RoadClass.Slip) * MathF.Tan(config.SlipLeavesRad * 0.5f));

    /// <summary>
    /// <b>How far along an intercity road a slip road leaves it to reach a corner at its own angle</b>
    /// (<see cref="CityGenFigures.SlipLeavesDeg"/>): walked out from beside the corner, behind the terminal or ahead of it,
    /// until the line to the corner lies that near the road's own — or none off the road's ends, or with no corner.
    /// </summary>
    /// <param name="way">Which way along the road from the corner it is walked: −1 behind, +1 ahead.</param>
    float? Leaves(Polyline line, Vector2? corner, float way)
    {
        if (corner is not { } cornerM) return null;

        var stepM = config.Terrain.GroundStepM;
        for (var alongM = line.Nearest(cornerM).AlongM; alongM > 0f && alongM < line.LengthM; alongM += way * stepM)
        {
            var toCorner = Vector2.Normalize(cornerM - line.At(alongM));
            if (Vector2.Dot(toCorner, line.Heading(alongM) * -way) >= MathF.Cos(config.SlipLeavesRad)) return alongM;
        }

        return null;
    }

    /// <summary>
    /// <b>How far along an intercity road either side of its terminals an interchange reaches</b>: its slip roads' leg and
    /// their shallow run, and the box they leave the road at.
    /// </summary>
    float InterchangeReachM(int path) =>
        SlipLegM + config.SlipRunM + config.SlipStandoffM(paths.All[path].CarriagewayM(config.LaneWidthM), config.SlipLeavesRad);

    /// <summary>
    /// The one place the slip roads at an end of an interchange meet the intercity road: midway between where each would,
    /// where both sides have one.
    /// </summary>
    static float? Paired(float? one, float? other) => one is { } a && other is { } b ? (a + b) * 0.5f : one ?? other;

    /// <summary>
    /// <b>The node slip roads meet an intercity road at</b>, this far along it, held out to the standoff the shallowest of
    /// them asks (<see cref="SimConfig.SlipStandoffM"/>) — or none where there is no such place or it is on a stretch closed
    /// to nodes.
    /// </summary>
    Vector2? SlipEnd(int path, float? alongM, Vector2? oneCorner, Vector2? otherCorner)
    {
        if (alongM is not { } atAlongM || paths.NodeAt(path, atAlongM, SlipWeldM) is var node && node < 0) return null;

        var line = paths.All[path].Line;
        var atM = paths.NodeM(node);
        var shallowestRad = MathF.PI;
        foreach (var corner in (ReadOnlySpan<Vector2?>)[oneCorner, otherCorner])
        {
            if (corner is not { } cornerM) continue;

            var leaves = Vector2.Normalize(cornerM - atM);
            var alongRoad = MathF.Abs(Vector2.Dot(leaves, line.Heading(atAlongM)));
            shallowestRad = MathF.Min(shallowestRad, MathF.Acos(MathF.Min(1f, alongRoad)));
        }

        paths.Reach(atM, config.SlipStandoffM(paths.All[path].CarriagewayM(config.LaneWidthM), shallowestRad));
        return atM;
    }

    /// <summary>How near along an intercity road a slip road's node stands to one already there to be that one.</summary>
    const float SlipWeldM = 1f;

    /// <summary>
    /// <b>The ground an intercity road keeps either side of its line</b> before the districts' edges: its corridor, and on an
    /// interchanged road the slip roads in its verge as well (<see cref="TownGen"/>'s barriers, GEN-68).
    /// </summary>
    float VergeM(int road) =>
        _interchanged ? config.CityGen.InterchangeOffsetM + TownGen.CorridorM(config, 1) : paths.CorridorM(intercity[road]);

    /// <summary>
    /// <b>The place a road to an interchange's terminal comes in square from</b>: straight out from the intercity road, past
    /// the terminal's box and the clearance a road's line keeps of it (<see cref="Paths"/>), so the road arrives square to
    /// the slip roads.
    /// </summary>
    Vector2 ApproachTo(int road, float stationM, Vector2 terminalM)
    {
        var outward = Vector2.Normalize(terminalM - paths.All[intercity[road]].Line.At(stationM));
        var clearM = (2f * config.JunctionRadiusAcrossM(config.LaneWidthM * LayoutEdge.MostLanes * SimConfig.LanesPerCarriageway))
                     + config.CityGen.LocalityM + config.CityGen.RouteCellM;
        return terminalM + (outward * clearM);
    }

    /// <summary>
    /// A road's points made to leave the terminal they start at through its approach (<see cref="ApproachTo"/>): every point
    /// nearer the terminal than that dropped, but the road's far end, and the approach put in where the road runs on past it.
    /// </summary>
    static void SquareOnto(List<Vector2> points, Vector2 approachM)
    {
        var approachAwayM = Vector2.Distance(approachM, points[0]);
        while (points.Count > 2 && Vector2.Distance(points[1], points[0]) <= approachAwayM) points.RemoveAt(1);
        if (Vector2.Distance(points[1], points[0]) > approachAwayM) points.Insert(1, approachM);
    }

    /// <summary>
    /// No node on an intercity road a locality either side of where a terminal, or the end of a road carried over it, stands
    /// <paramref name="offM"/> beside it (GEN-16): settled into one, the two would be a junction on it after all.
    /// </summary>
    void KeepBeside(int road, float stationM, float offM)
    {
        var keepM = offM + config.CityGen.LocalityM;
        paths.Keep(intercity[road], stationM - keepM, stationM + keepM);
    }

    /// <summary>Which side of an intercity road a place stands: 0 its right, 1 its left.</summary>
    static int SideOf(Polyline line, float stationM, Vector2 atM) =>
        Vector2.Dot(atM - line.At(stationM), Heading.RightOf(line.Heading(stationM))) >= 0f ? 0 : 1;

    /// <summary>
    /// <b>An interchange's terminal on one side</b>, where a road laid to the junction planned there ends instead of on the
    /// intercity road itself (GEN-68): the first road laid to that side settles it, at the offset off the line square to it.
    /// </summary>
    Vector2 Terminal(int road, float stationM, int side)
    {
        if (!_interchanges.TryGetValue((road, stationM), out var terminals))
        {
            _interchanges[(road, stationM)] = terminals = [null, null];
            KeepBeside(road, stationM, config.CityGen.InterchangeOffsetM);
        }

        var line = paths.All[intercity[road]].Line;
        var sign = side == 0 ? 1f : -1f;
        terminals[side] ??= line.At(stationM) + (Heading.RightOf(line.Heading(stationM)) * (sign * config.CityGen.InterchangeOffsetM));
        return terminals[side]!.Value;
    }

    /// <summary>
    /// <b>Every piece of the roads laid so far that the intercity network was left without, joined to it</b> (GEN-69) — once
    /// the paths are closed into roads and before any street is: a planned road the ground refused leaves the roads
    /// planned behind it a piece of their own, which the town would otherwise delete with its streets
    /// (<see cref="TownLayout.KeepTheLargestComponent"/>). Each is given the shortest straight district road the layout
    /// takes from one of its junctions to one of the network's, within a road in's reach; one none is taken for is left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked of the layout and not of the plan</b>, since the refusals are the layout's: the plan holds every rule it
    /// can, and this is what is left of the rest.
    /// </para>
    /// <para>
    /// <b>A link hangs a junction wherever it crosses a street to come</b> (GEN-72): the lattice was placed against the
    /// paths and not against a link no plan held, so each street whose chord the link crosses is cut there into two that
    /// meet it — and a link that cannot stand a junction at every crossing, a locality clear of every node and a road's
    /// length off each end, is not taken. Laid across the streets with none, each was refused as it was laid (GEN-49), and
    /// the district was cut by a road it could not reach.
    /// </para>
    /// <para>
    /// <b>The town's piece is <see cref="TownLayout.MainPiece"/></b>, the one the deletion after keeps.
    /// </para>
    /// </remarks>
    /// <param name="streets">
    /// The streets still to be laid, on the settled nodes (<see cref="Lattice.Settled"/>): each one a link crosses is
    /// cut in two at the junction it hangs there.
    /// </param>
    public void JoinWhatWasLeft(TownLayout layout, List<(int From, int To, bool Straight)> streets)
    {
        var edges = layout.Edges;
        var root = layout.Pieces();
        var main = layout.MainPiece(root);
        if (main < 0) return;

        var nodesOf = new Dictionary<int, List<int>>();
        var onARoad = new bool[layout.NodeM.Count];

        // A link is laid straight, and a road landing alone on a bridgehead leaves on the span's own line (GEN-14a), which a
        // straight to anywhere else does not: no link ends or passes at one, nor at a bridgehead that is no junction. Nor
        // at the map's edge, where only an intercity road runs off (GEN-63), nor on an interchanged intercity road, which
        // only its slip roads meet (GEN-68).
        var noLink = new bool[layout.NodeM.Count];
        foreach (var edge in edges)
        {
            if (edge.Class == RoadClass.Bridge || edge.Over || (_interchanged && edge.Class is RoadClass.Intercity or RoadClass.Slip))
            {
                (noLink[edge.From], noLink[edge.To]) = (true, true);
            }
        }

        var sizeM = town.Map.SizeM;
        for (var node = 0; node < noLink.Length; node++)
        {
            var atM = layout.NodeM[node];
            noLink[node] |= atM.X <= 0f || atM.Y <= 0f || atM.X >= sizeM.X || atM.Y >= sizeM.Y || layout.Sizes.IsJoint(atM);
        }

        foreach (var edge in edges)
        {
            foreach (var node in (ReadOnlySpan<int>)[edge.From, edge.To])
            {
                if (onARoad[node]) continue;

                onARoad[node] = true;
                var piece = Clusters.Find(root, node);
                if (!nodesOf.TryGetValue(piece, out var nodes)) nodesOf[piece] = nodes = [];
                nodes.Add(node);
            }
        }

        if (nodesOf.Count < 2) return;

        // A road in's reach in the district that reaches furthest: the widest spacing any district lays (ReachOf).
        var reachM = town.Districts.CoarsestM.AcrossM * config.CityGen.RoadInReachInBlocks;
        var shortestM = layout.ShortestRoadM;
        var network = new PointCells(config.Grid.Main, Vector2.Zero, town.Map.SizeM);
        var networkNode = new List<int>();
        foreach (var node in nodesOf[main]) Join(node);

        var chords = new SegmentCells(reachM);
        foreach (var (from, to, _) in streets) chords.Add(layout.NodeM[from], layout.NodeM[to]);

        var near = new List<int>();
        var candidates = new List<(float M, int From, int To)>();
        var crossings = new List<(float Along, int Street, Vector2 AtM)>();
        var through = new List<(int Node, Vector2 AtM)>();
        var cut = new List<int>();
        var localityM = config.CityGen.LocalityM;

        // Again until none is joined: a piece the network reaches only through another is joined once that one is.
        var left = new List<int>(nodesOf.Keys.Where(piece => piece != main));
        left.Sort((one, other) => (nodesOf[other].Count, one).CompareTo((nodesOf[one].Count, other)));
        for (var joinedAny = true; joinedAny;)
        {
            joinedAny = false;
            for (var at = 0; at < left.Count; at++)
            {
                if (!JoinedOn(nodesOf[left[at]])) continue;

                foreach (var node in nodesOf[left[at]]) Join(node);
                left.RemoveAt(at--);
                joinedAny = true;
            }
        }

        void Join(int node)
        {
            network.Add(layout.NodeM[node]);
            networkNode.Add(node);
        }

        // The shortest road the layout takes from one of the piece's junctions to one of the network's.
        bool JoinedOn(List<int> piece)
        {
            candidates.Clear();
            foreach (var from in piece)
            {
                if (noLink[from]) continue;

                network.Within(layout.NodeM[from], reachM, near);
                foreach (var point in near)
                {
                    var to = networkNode[point];
                    var apartM = Vector2.Distance(layout.NodeM[from], layout.NodeM[to]);
                    if (apartM >= shortestM && !noLink[to]) candidates.Add((apartM, from, to));
                }
            }

            candidates.Sort();
            foreach (var (_, from, to) in candidates)
            {
                if (Hung(from, to)) return true;
            }

            return false;
        }

        // One link laid from one node to another through a junction at every street to come it crosses, and each of those
        // streets cut there — or nothing, where a junction cannot stand at each or the layout refuses any piece of it.
        bool Hung(int from, int to)
        {
            if (!OffTheShore(layout.NodeM[from], layout.NodeM[to])) return false;

            chords.Crossing(layout.NodeM[from], layout.NodeM[to], crossings);
            crossings.Sort((one, other) => one.Along.CompareTo(other.Along));

            // <b>A crossing within a locality of the street's own end passes through that end</b> (GEN-16): a junction a
            // stride from another is one junction, and the street meets the link there whole. One standing clear of every
            // node is a junction of its own, and the street is cut at it; one near any other node is no place for either.
            through.Clear();
            cut.Clear();
            foreach (var (_, street, atM) in crossings)
            {
                var (streetFrom, streetTo, _) = streets[street];
                var (offFromM, offToM) = (Vector2.Distance(atM, layout.NodeM[streetFrom]), Vector2.Distance(atM, layout.NodeM[streetTo]));
                var node = MathF.Min(offFromM, offToM) >= MathF.Max(localityM, shortestM) ? -1 : offFromM <= offToM ? streetFrom : streetTo;
                // A node a link before this one stood is past the ones filed, and is neither.
                if ((node < 0 && !layout.StandsClear(atM)) || (node >= 0 && node < noLink.Length && noLink[node])) return false;
                if (node == from || node == to || (node >= 0 && through.Count > 0 && through[^1].Node == node)) continue;

                // Two junctions of its own a stride apart would be one (GEN-16); the nodes it has keep their locality already.
                var placeM = node >= 0 ? layout.NodeM[node] : atM;
                var lastM = through.Count == 0 ? layout.NodeM[from] : through[^1].Node >= 0 ? layout.NodeM[through[^1].Node] : through[^1].AtM;
                if (Vector2.Distance(placeM, lastM) < localityM) return false;

                through.Add((node, atM));
                cut.Add(node < 0 ? street : -1);
            }

            if (layout.JoinThrough(from, to, through, RoadClass.Arterial) is not { } hung) return false;

            for (var place = 0; place < through.Count; place++)
            {
                if (cut[place] is not (var street and >= 0)) continue;

                var (streetFrom, streetTo, straight) = streets[street];
                streets[street] = (streetFrom, hung[place], straight);
                streets.Add((hung[place], streetTo, straight));
                chords.Cut(street, layout.NodeM[streetFrom], layout.NodeM[hung[place]]);
                chords.Add(layout.NodeM[hung[place]], layout.NodeM[streetTo]);
            }

            return true;
        }
    }

    /// <summary>
    /// <b>Whether a straight link keeps off the shore</b> (GEN-71): its corridor and the waterfront's clearance past it. A
    /// planned road is kept off by the barriers the districts were cut round; a link laid after, between any two junctions,
    /// is not, and the layout asks only that its carriageway stand off the water itself.
    /// </summary>
    bool OffTheShore(Vector2 fromM, Vector2 toM)
    {
        var reachM = _streetM + config.CityGen.WaterfrontClearM;
        var runM = Vector2.Distance(fromM, toM);
        var steps = Math.Max(1, (int)MathF.Ceiling(runM / config.CityGen.RouteCellM));
        for (var step = 0; step <= steps; step++)
        {
            if (Connectors.Near(town.Ground, Vector2.Lerp(fromM, toM, step / (float)steps), reachM)) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>A road along every edge two districts share</b>: the edge as the partition traced it, thinned and rounded at a
    /// district road's own floor (GEN-47) — and, where it ends against an intercity road, run on to a junction on it.
    /// </summary>
    void Seams(List<Planned> planned)
    {
        foreach (var (a, b, corners) in _partition.Seams())
        {
            var figures = config.CityGen;
            var smoothed = Polyline.Cut(Polyline.Thinned(corners, _partition.Grid.CellM * figures.SeamThinCells), figures.SeamCornerCuts);
            var line = new Polyline([.. Polyline.Resampled(smoothed, figures.RouteCellM)]);
            if (line.LengthM < figures.LocalityM * figures.SeamShortestLocalities) continue;

            planned.Add(new Planned(a, b, line, [], OntoAnIntercity(line.FirstM), OntoAnIntercity(line.LastM), Seam: true));
        }
    }

    /// <summary>The planned junction on the nearest intercity road an edge ending here runs on to — or none where none is that near or may stand there.</summary>
    End OntoAnIntercity(Vector2 endM)
    {
        var (best, bestAlongM, bestM) = (-1, 0f, float.PositiveInfinity);
        for (var road = 0; road < intercity.Length; road++)
        {
            var (alongM, awayM) = paths.All[intercity[road]].Line.Nearest(endM);
            var reachM = VergeM(road) + _streetM + (config.CityGen.RunOnCells * _partition.Grid.CellM);
            if (awayM < reachM && awayM < bestM) (best, bestAlongM, bestM) = (road, alongM, awayM);
        }

        return best >= 0 && Station(best, bestAlongM) is { } stationM ? new End(Road: best, RoadAlongM: stationM) : End.None;
    }

    /// <summary>
    /// <b>A junction on an intercity road, planned</b>: the one already planned near the place, else a new one there where
    /// none stands within the spacing an intercity road's junctions keep (GEN-68) — or none.
    /// </summary>
    float? Station(int road, float alongM)
    {
        if (WouldStation(road, alongM) is not { } stationM) return null;

        if (!_stations[road].Contains(stationM)) _stations[road].Add(stationM);
        if (!_interchangesAt[road].Contains(stationM)) _interchangesAt[road].Add(stationM);
        return stationM;
    }

    /// <summary>
    /// <b>A link over every river two districts face each other across</b>, at the bridge sites whose two ends stand in
    /// them — spaced along their shared bank, kept off the bridges already standing on that river, and added where its
    /// bridges stand too far apart (GEN-70).
    /// </summary>
    void Bridged(List<Planned> planned)
    {
        var figures = config.CityGen;
        var standing = new List<Crossing>();
        foreach (var road in town.Connectors.Roads) standing.AddRange(road.Crossed.Where(crossing => crossing.Bridge));

        var faced = Faced(town.Connectors.Bridges.Except(standing));
        var chosen = new List<(Crossing Site, int A, int B)>();
        foreach (var ((a, b), sites) in faced)
        {
            foreach (var site in Spaced(sites, figures.DistrictLinksApartM))
            {
                if (TooNear(site, standing, chosen.Select(pick => pick.Site), figures.BridgesApartLeastM)) continue;

                chosen.Add((site, a, b));
            }
        }

        // Where a river parting districts runs further than a town crosses its water between two bridges, the site nearest
        // the middle of the gap is taken too, held off the bridges either side as the first were, until no gap is left
        // that a site could close.
        var every = faced.SelectMany(pair => pair.Value.Select(site => (Site: site, pair.Key.A, pair.Key.B))).ToList();
        var rivers = every.Select(pick => pick.Site.Barrier).Distinct().ToList();
        for (var added = true; added;)
        {
            added = false;
            foreach (var river in rivers)
            {
                if (GapClosed(river) is not { } pick) continue;

                chosen.Add(pick);
                added = true;
                break;
            }
        }

        foreach (var (site, a, b) in chosen) planned.Add(Across(planned, site, a, b));

        // The site nearest the middle of the widest gap along one river that one closes, or none.
        (Crossing Site, int A, int B)? GapClosed(int river)
        {
            var alongs = standing.Concat(chosen.Select(pick => pick.Site)).Where(site => site.Barrier == river).Select(site => site.AlongM).Order().ToList();
            var gaps = new List<(float WidthM, float BeforeM, float AfterM)>();
            for (var at = 0; at + 1 < alongs.Count; at++)
            {
                if (alongs[at + 1] - alongs[at] > figures.BridgesApartMostM) gaps.Add((alongs[at + 1] - alongs[at], alongs[at], alongs[at + 1]));
            }

            foreach (var (_, beforeM, afterM) in gaps.OrderByDescending(gap => gap.WidthM))
            {
                var middleM = (beforeM + afterM) * 0.5f;
                var picks = every
                    .Where(pick => pick.Site.Barrier == river
                                   && pick.Site.AlongM - beforeM >= figures.BridgesApartLeastM && afterM - pick.Site.AlongM >= figures.BridgesApartLeastM
                                   && !TooNear(pick.Site, standing, chosen.Select(chosenPick => chosenPick.Site), figures.BridgesApartLeastM))
                    .OrderBy(pick => MathF.Abs(pick.Site.AlongM - middleM));
                foreach (var pick in picks) return pick;
            }

            return null;
        }
    }

    /// <summary>A link over every railway two districts face each other across, at the level crossings whose ends stand in them.</summary>
    void LevelCrossed(List<Planned> planned)
    {
        var standing = town.Connectors.Roads.SelectMany(road => road.Crossed.Where(crossing => !crossing.Bridge)).ToList();
        var chosen = new List<Crossing>();
        foreach (var ((a, b), sites) in Faced(town.Connectors.LevelCrossings.Except(standing)))
        {
            foreach (var site in Spaced(sites, config.CityGen.DistrictLinksApartM))
            {
                if (TooNear(site, standing, chosen, config.CityGen.IntercityJunctionsApartLeastM)) continue;

                chosen.Add(site);
                planned.Add(Across(planned, site, a, b));
            }
        }
    }

    /// <summary>
    /// <b>The crossings two districts face each other over</b>: each one whose two ends, a few cells on past the stretch a
    /// road holds its line, stand in two different districts — filed by the pair, each pair's in order along the barrier.
    /// </summary>
    Dictionary<(int A, int B), List<Crossing>> Faced(IEnumerable<Crossing> crossings)
    {
        var faced = new Dictionary<(int A, int B), List<Crossing>>();
        var approachM = config.CityGen.BridgeApproachM;
        foreach (var crossing in crossings)
        {
            var past = crossing.Unit * (config.CityGen.FacedCells * _partition.Grid.CellM);
            var (a, b) = (_partition.At(crossing.NearApproachM(approachM) - past), _partition.At(crossing.FarApproachM(approachM) + past));
            if (a < 0 || b < 0 || a == b) continue;

            var (key, way) = a < b ? ((a, b), crossing) : ((b, a), crossing.Reversed);
            if (!faced.TryGetValue(key, out var list)) faced[key] = list = [];
            list.Add(way);
        }

        foreach (var list in faced.Values) list.Sort((x, y) => (x.Barrier, x.AlongM).CompareTo((y.Barrier, y.AlongM)));
        return faced;
    }

    /// <summary>
    /// <b>As many of one pair's crossings as their shared stretch is long</b>, one to every <paramref name="apartM"/> of it
    /// and at least one, each the shortest near the middle of its own share of the stretch
    /// (<see cref="CityGenFigures.CrossingWindowShare"/> of it either way).
    /// </summary>
    IEnumerable<Crossing> Spaced(List<Crossing> sites, float apartM)
    {
        var windowShare = config.CityGen.CrossingWindowShare;
        foreach (var barrier in sites.Select(site => site.Barrier).Distinct())
        {
            var on = sites.Where(site => site.Barrier == barrier).ToList();
            var (fromM, toM) = (on[0].AlongM, on[^1].AlongM);
            var picks = Math.Max(1, (int)MathF.Round((toM - fromM) / apartM));
            for (var pick = 0; pick < picks; pick++)
            {
                var middleM = fromM + ((toM - fromM) * (pick + 0.5f) / picks);
                var windowM = MathF.Max((toM - fromM) / picks * windowShare, 1f);
                yield return on.Where(site => MathF.Abs(site.AlongM - middleM) <= windowM).DefaultIfEmpty(on.MinBy(site => MathF.Abs(site.AlongM - middleM)))
                    .MinBy(site => site.SpanM);
            }
        }
    }

    /// <summary>
    /// Whether a crossing stands within a spacing of another of its kind: along the barrier where both cross one, and on the
    /// ground where each crosses its own — two tracks laid side by side, two arms of one river — so no two links run
    /// along each other to the same roads.
    /// </summary>
    static bool TooNear(Crossing site, IEnumerable<Crossing> standing, IEnumerable<Crossing> chosen, float apartM) =>
        standing.Concat(chosen).Any(other => other.Bridge == site.Bridge && (other.Barrier == site.Barrier
            ? MathF.Abs(other.AlongM - site.AlongM) < apartM
            : Vector2.Distance(Blend.Lerp(other.NearM, other.FarM, 0.5f), Blend.Lerp(site.NearM, site.FarM, 0.5f)) < apartM));

    /// <summary>
    /// <b>One link across a crossing</b>: from inside one district, held on the crossing's line through its approach, over
    /// it, and into the other — each end on its district's own roads where one is near, and on its lattice where not.
    /// </summary>
    Planned Across(List<Planned> planned, Crossing site, int a, int b)
    {
        var approachM = config.CityGen.BridgeApproachM;
        var near = site.NearApproachM(approachM);
        var far = site.FarApproachM(approachM);
        var (first, inA) = Into(planned, a, near, -site.Unit, anyReach: true);
        var (last, inB) = Into(planned, b, far, site.Unit, anyReach: true);
        return new Planned(a, b, new Polyline([inA, near, site.NearM, site.FarM, far, inB]), [site], first, last, Seam: false);
    }

    /// <summary>
    /// <b>Where a road coming into a district ends</b>: on the nearest road between districts already planned for that
    /// district — its edge roads, the roads in to it — within reach, held near the way it came in and crossing nothing but
    /// the district on the way, so the roads between districts are one network; and otherwise on the district's lattice
    /// (<see cref="Inside"/>).
    /// </summary>
    /// <param name="anyReach">
    /// Whether, past the near roads held to the way it came in, any of the district's roads a straight inside it reaches
    /// will do — a link over the water or the railway ending on the lattice brings its district to its neighbour only
    /// through the neighbour's own streets.
    /// </param>
    (End End, Vector2 AtM) Into(List<Planned> planned, int district, Vector2 fromM, Vector2 heading, bool anyReach = false)
    {
        var plan = town.Districts[district];
        var reachM = MathF.Max(plan.BlockAlongM, plan.BlockAcrossM) * config.CityGen.ToTheEdgeRoadInBlocks;
        foreach (var (withinM, inwardLeast) in (ReadOnlySpan<(float, float)>)[(reachM, _inwardLeast), (anyReach ? float.PositiveInfinity : 0f, 0f)])
        {
            var (best, bestAlongM, bestM) = (-1, 0f, withinM);
            for (var other = 0; other < planned.Count; other++)
            {
                if (planned[other].A != district && planned[other].B != district) continue;

                var (alongM, awayM) = planned[other].Line.Nearest(fromM);
                if (awayM >= bestM || awayM < _streetM) continue;

                // Held near the way it came in: a link turned along the barrier it crossed would lie against it (GEN-49).
                var ontoM = planned[other].Line.At(alongM);
                if (Vector2.Dot((ontoM - fromM) / awayM, heading) < inwardLeast || !InTheDistrict(fromM, ontoM, district)) continue;
                if (CrossesAny(planned, fromM, ontoM, other) || !Lands(planned, other, alongM, (fromM - ontoM) / awayM)) continue;

                (best, bestAlongM, bestM) = (other, alongM, awayM);
            }

            if (best >= 0) return (new End(Onto: best, OntoAlongM: bestAlongM), planned[best].Line.At(bestAlongM));
        }

        return (End.None, Inside(fromM, heading, town.Districts.CoarsestM.AlongM, district));
    }

    /// <summary>Whether a straight crosses any planned road but the one it ends on, where it would meet it at no junction (GEN-49).</summary>
    static bool CrossesAny(List<Planned> planned, Vector2 fromM, Vector2 toM, int but, int andBut = -1)
    {
        for (var other = 0; other < planned.Count; other++)
        {
            if (other == but || other == andBut) continue;

            var points = planned[other].Line.PointsM;
            for (var point = 0; point + 1 < points.Length; point++)
            {
                if (Polygons.SegmentsCross(fromM, toM, points[point], points[point + 1])) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the straight between two places stands in the district all the way, or beside it on an intercity road's
    /// corridor or the map's margin — and never on the water, the shore or the railway (GEN-71).
    /// </summary>
    bool InTheDistrict(Vector2 fromM, Vector2 toM, int district)
    {
        var steps = (int)MathF.Ceiling(Vector2.Distance(fromM, toM) / _partition.Grid.CellM);
        for (var step = 1; step < steps; step++)
        {
            var cell = _partition.Grid.IndexOf(Blend.Lerp(fromM, toM, step / (float)steps));
            var at = _partition.Label[cell];
            if (at >= 0 ? at != district : _partition.Barrier[cell] is not (Barrier.Road or Barrier.Edge or Barrier.None)) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>Where a link ends on its district's lattice</b>: a place up to a reach on from where it crosses into the district,
    /// drawn back toward it until it stands in the district, and moved onto the district's own lattice point nearest that
    /// (<see cref="Lattice.PointNearest"/>) where that is the district's too and no further off the way it came — so the
    /// lattice's streets meet the link's end as they meet each other.
    /// </summary>
    Vector2 Inside(Vector2 fromM, Vector2 heading, float reachM, int district)
    {
        var atM = fromM + (heading * _partition.Grid.CellM);
        for (var outM = reachM; outM > _partition.Grid.CellM; outM -= _partition.Grid.CellM)
        {
            if (_partition.At(fromM + (heading * outM)) != district) continue;

            atM = fromM + (heading * outM);
            break;
        }

        // Moved only as far as the link still leaves the road it came off as square as it was planned to (GEN-13).
        var onTheLatticeM = Lattice.PointNearest(_partition, town.Districts, district, atM);
        var toLatticeM = onTheLatticeM - fromM;
        var square = toLatticeM.Length() > _streetM && Vector2.Dot(Vector2.Normalize(toLatticeM), heading) >= _inwardLeast;
        return _partition.At(onTheLatticeM) == district && square ? onTheLatticeM : atM;
    }

    /// <summary>
    /// <b>Where each district fronts each intercity road</b>, either side of it: the places along it, a station apart, a
    /// district stands just past its corridor — filed by the road and the side.
    /// </summary>
    List<(int Road, float AlongM, Vector2 Inward, int Left, int Right)> Fronts()
    {
        var fronts = new List<(int, float, Vector2, int, int)>();
        for (var road = 0; road < intercity.Length; road++)
        {
            var line = paths.All[intercity[road]].Line;
            var offM = VergeM(road) + _streetM + (config.CityGen.FrontCells * _partition.Grid.CellM);
            var stepM = config.CityGen.FrontStationStepM;
            for (var alongM = stepM; alongM < line.LengthM - stepM; alongM += stepM)
            {
                var side = Heading.RightOf(line.Heading(alongM));
                var atM = line.At(alongM);
                fronts.Add((road, alongM, side, _partition.At(atM + (side * offM)), _partition.At(atM - (side * offM))));
            }
        }

        return fronts;
    }

    /// <summary>
    /// <b>A link over every intercity road two districts face each other across</b> (GEN-69): square to it, at a station
    /// where a district stands either side and a junction the road may have (GEN-68) — a crossroads on it, laid as two roads
    /// in from that junction, one to each district; or in a bypassed town one road from district to district carried over
    /// it, which meets it nowhere.
    /// </summary>
    void OverTheRoads(List<Planned> planned)
    {
        var faced = new Dictionary<(int Road, int A, int B), List<(float AlongM, Vector2 Side)>>();
        foreach (var (road, alongM, side, left, right) in Fronts())
        {
            if (left < 0 || right < 0 || left == right) continue;

            var key = left < right ? (road, left, right) : (road, right, left);
            if (!faced.TryGetValue(key, out var list)) faced[key] = list = [];
            list.Add((alongM, left < right ? side : -side));
        }

        foreach (var ((road, a, b), alongs) in faced)
        {
            var line = paths.All[intercity[road]].Line;
            var picks = Math.Max(1, (int)MathF.Round((alongs[^1].AlongM - alongs[0].AlongM) / config.CityGen.DistrictLinksApartM));
            for (var pick = 0; pick < picks; pick++)
            {
                var middleM = alongs[0].AlongM + ((alongs[^1].AlongM - alongs[0].AlongM) * (pick + 0.5f) / picks);
                var (alongM, towardA) = alongs.MinBy(at => MathF.Abs(at.AlongM - middleM));
                if (WouldStation(road, alongM, over: _interchanged) is not { } stationM) continue;

                var atM = line.At(stationM);
                if (_interchanged)
                {
                    // Carried over it on a span of its own, meeting it nowhere (GEN-68).
                    var halfSpanM = TownGen.OverHalfSpanM(config);
                    var (nearM, farM) = (atM + (towardA * halfSpanM), atM - (towardA * halfSpanM));
                    if (BesideAnother(road, nearM, halfSpanM) || BesideAnother(road, farM, halfSpanM)) continue;

                    var (fromA, overA) = Into(planned, a, nearM, towardA);
                    var (fromB, overB) = Into(planned, b, farM, -towardA);
                    _overAt[road].Add(stationM);
                    KeepBeside(road, stationM, halfSpanM);
                    planned.Add(new Planned(
                        a, b, new Polyline([overA, nearM, farM, overB]), [new Crossing(nearM, farM, false, road, stationM, Over: true)], fromA, fromB,
                        Seam: false));
                    continue;
                }

                var station = new End(Road: road, RoadAlongM: stationM);
                var (intoA, inA) = Into(planned, a, atM, towardA);
                var (intoB, inB) = Into(planned, b, atM, -towardA);
                if (!ArmsClear(planned, road, stationM, atM, Vector2.Normalize(inA - atM)) || !ArmsClear(planned, road, stationM, atM, Vector2.Normalize(inB - atM))) continue;

                Station(road, stationM);
                planned.Add(new Planned(a, b, new Polyline([atM, inA]), [], station, intoA, Seam: false));
                planned.Add(new Planned(a, b, new Polyline([atM, inB]), [], station, intoB, Seam: false));
            }
        }
    }

    /// <summary>
    /// Whether a span's end over one intercity road would stand within its reach of another (GEN-14a, GEN-68): the road
    /// carrying it on leaves the bridgehead with no junction's ground round it, so it would start on that road's.
    /// </summary>
    bool BesideAnother(int road, Vector2 endM, float reachM)
    {
        for (var other = 0; other < intercity.Length; other++)
        {
            if (other != road && paths.All[intercity[other]].Line.Nearest(endM).AwayM < reachM) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>A road in from an intercity road to every piece of the roads between districts that does not reach one</b>
    /// (GEN-69): from a junction on an intercity road that one of the piece's districts fronts, where one may stand (GEN-68),
    /// to the nearest of that district's roads in the piece — and, to a district no road between districts serves at all,
    /// to its lattice. Asked again after each, since a road in to one district is its neighbours' over the edges they share.
    /// </summary>
    void RoadsIn(List<Planned> planned, int seams)
    {
        var fronts = Fronts();
        var tried = new HashSet<int>();
        for (var given = true; given;)
        {
            given = false;
            var (root, outside) = Joined(planned, seams);
            var reaching = Clusters.Find(root, outside);
            var reaches = Enumerable.Range(0, planned.Count).Select(at => Clusters.Find(root, at) == reaching).ToArray();
            foreach (var piece in Enumerable.Range(0, planned.Count).Where(at => !reaches[at]).GroupBy(at => Clusters.Find(root, at)))
            {
                if (Crossing(planned, [.. piece]) || RoadIn(planned, fronts, [.. piece]) || Across(planned, [.. piece], reaches)) given = true;
                if (given) break;
            }

            if (given) continue;

            // A district no road between districts serves at all is reached on its lattice — and in an interchanged town one
            // whose roads do not reach the intercity network either, a road carried over it reaching nothing (GEN-68).
            var served = new bool[_partition.Count];
            for (var at = 0; at < planned.Count; at++)
            {
                if (!_interchanged || reaches[at]) (served[planned[at].A], served[planned[at].B]) = (true, true);
            }

            foreach (var district in Enumerable.Range(0, _partition.Count).OrderBy(at => Vector2.Distance(_partition.CentreM[at], town.Connectors.CoreM)))
            {
                if (served[district] || !tried.Add(district)) continue;

                var fronting = fronts.Where(front => front.Left == district || front.Right == district).ToList();
                if (fronting.Count == 0) continue;

                // Nearest the middle of what it fronts of each road, each road's along measured on that road alone.
                var middleM = fronting.GroupBy(front => front.Road).ToDictionary(road => road.Key, road => road.Average(front => front.AlongM));
                foreach (var (road, alongM, side, left, _) in fronting.OrderBy(front => MathF.Abs(front.AlongM - middleM[front.Road])))
                {
                    if (WouldStation(road, alongM) is not { } stationM) continue;

                    var atM = paths.All[intercity[road]].Line.At(stationM);
                    var (last, inward) = Into(planned, district, atM, left == district ? side : -side);
                    if (!ArmsClear(planned, road, stationM, atM, Vector2.Normalize(inward - atM))) continue;

                    Station(road, stationM);
                    if (_interchanged) _crossing.Add((road, stationM));
                    planned.Add(new Planned(district, district, new Polyline([atM, inward]), [], new End(Road: road, RoadAlongM: stationM), last, Seam: false));
                    given = true;
                    break;
                }

                if (given) break;
            }
        }
    }

    /// <summary>
    /// <b>A piece already met at an interchange made to cross</b> (GEN-68): met there on one side, it is left by one way of
    /// the intercity road alone, so the first interchange it has is the one that reaches both — or false where it has none.
    /// </summary>
    bool Crossing(List<Planned> planned, int[] piece)
    {
        if (!_interchanged) return false;

        foreach (var at in piece)
        {
            foreach (var end in (ReadOnlySpan<End>)[planned[at].First, planned[at].Last])
            {
                if (end.Road >= 0 && _crossing.Add((end.Road, end.RoadAlongM))) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <b>One road in to a piece of the roads between districts</b>, from the first junction it may have on an intercity
    /// road one of the piece's districts fronts, to the nearest of that district's roads in the piece within reach — or
    /// false where no front of any of its districts takes one.
    /// </summary>
    bool RoadIn(List<Planned> planned, List<(int Road, float AlongM, Vector2 Inward, int Left, int Right)> fronts, int[] piece)
    {
        var districts = new HashSet<int>();
        foreach (var at in piece)
        {
            districts.Add(planned[at].A);
            districts.Add(planned[at].B);
        }

        foreach (var (road, alongM, side, left, right) in fronts)
        {
            var district = districts.Contains(left) ? left : districts.Contains(right) ? right : -1;
            if (district < 0 || WouldStation(road, alongM) is not { } stationM) continue;

            var atM = paths.All[intercity[road]].Line.At(stationM);
            var inward = district == left ? side : -side;
            var (best, bestAlongM, bestM) = (-1, 0f, ReachOf(district));
            foreach (var at in piece)
            {
                if (planned[at].A != district && planned[at].B != district) continue;

                var (ontoAlongM, awayM) = planned[at].Line.Nearest(atM);
                var ontoM = planned[at].Line.At(ontoAlongM);
                if (awayM >= bestM || awayM < _streetM || Vector2.Dot((ontoM - atM) / awayM, inward) < _inwardLeast || !InTheDistrict(atM, ontoM, district)) continue;
                if (CrossesAny(planned, atM, ontoM, at) || !ArmsClear(planned, road, stationM, atM, (ontoM - atM) / awayM)) continue;
                if (!Lands(planned, at, ontoAlongM, (atM - ontoM) / awayM)) continue;

                (best, bestAlongM, bestM) = (at, ontoAlongM, awayM);
            }

            if (best < 0) continue;

            Station(road, stationM);
            if (_interchanged) _crossing.Add((road, stationM));
            planned.Add(new Planned(
                district, district, new Polyline([atM, planned[best].Line.At(bestAlongM)]), [], new End(Road: road, RoadAlongM: stationM),
                new End(Onto: best, OntoAlongM: bestAlongM), Seam: false));
            return true;
        }

        return false;
    }

    /// <summary>How far a road in to a district, or across it, may run to the roads it joins.</summary>
    float ReachOf(int district) =>
        MathF.Max(town.Districts[district].BlockAlongM, town.Districts[district].BlockAcrossM) * config.CityGen.RoadInReachInBlocks;

    /// <summary>
    /// <b>One road across a district</b> from a road between districts that reaches the intercity network to one of a piece
    /// that does not, both that district's — the shortest, square enough to each (GEN-13), inside the district all the way
    /// and crossing no other — or false where none is. What joins a piece no intercity road can be brought in to.
    /// </summary>
    bool Across(List<Planned> planned, int[] piece, bool[] reaches)
    {
        var (bestFrom, bestFromAlongM, bestTo, bestToAlongM, bestM) = (-1, 0f, -1, 0f, float.PositiveInfinity);
        var bestDistrict = -1;
        foreach (var to in piece)
        {
            foreach (var district in (ReadOnlySpan<int>)[planned[to].A, planned[to].B])
            {
                for (var from = 0; from < planned.Count; from++)
                {
                    if (!reaches[from] || (planned[from].A != district && planned[from].B != district)) continue;

                    var line = planned[from].Line;
                    for (var alongM = 0f; alongM <= line.LengthM; alongM += config.CityGen.RouteCellM)
                    {
                        var fromM = line.At(alongM);
                        var (toAlongM, awayM) = planned[to].Line.Nearest(fromM);
                        if (awayM >= bestM || awayM < _streetM || awayM > ReachOf(district)) continue;

                        var toM = planned[to].Line.At(toAlongM);
                        var heading = (toM - fromM) / awayM;
                        if (!Lands(planned, from, alongM, heading) || !Lands(planned, to, toAlongM, -heading)) continue;
                        if (!InTheDistrict(fromM, toM, district) || CrossesAny(planned, fromM, toM, from, to)) continue;

                        (bestFrom, bestFromAlongM, bestTo, bestToAlongM, bestM) = (from, alongM, to, toAlongM, awayM);
                        bestDistrict = district;
                    }
                }
            }
        }

        if (bestFrom < 0) return false;

        planned.Add(new Planned(
            bestDistrict, bestDistrict, new Polyline([planned[bestFrom].Line.At(bestFromAlongM), planned[bestTo].Line.At(bestToAlongM)]), [],
            new End(Onto: bestFrom, OntoAlongM: bestFromAlongM), new End(Onto: bestTo, OntoAlongM: bestToAlongM), Seam: false));
        return true;
    }

    /// <summary>
    /// <b>Whether a road may end on a planned road here, leaving it this way</b> (GEN-13): square enough to it, clear of its
    /// ends — where the roads it meets there stand — and far enough off every road planned to end on it near the same place,
    /// which the same node would carry.
    /// </summary>
    bool Lands(List<Planned> planned, int onto, float alongM, Vector2 leaving)
    {
        var line = planned[onto].Line;
        var weldM = config.CityGen.DistrictJunctionsApartLeastM;
        if (alongM < weldM || alongM > line.LengthM - weldM) return false;
        if (MathF.Abs(Vector2.Dot(Heading.RightOf(line.Heading(alongM)), leaving)) < _inwardLeast) return false;

        var atM = line.At(alongM);
        foreach (var plan in planned)
        {
            foreach (var (end, first) in (ReadOnlySpan<(End, bool)>)[(plan.First, true), (plan.Last, false)])
            {
                if (end.Onto != onto || MathF.Abs(end.OntoAlongM - alongM) >= weldM) continue;

                if (Vector2.Dot(Leaving(plan.Line, atM, first), leaving) > _armsApartCos) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a road leaving a planned junction on an intercity road this way stands far enough off every road planned to
    /// leave it already (GEN-13) — two leaving it nearer would be refused as the second is laid.
    /// </summary>
    bool ArmsClear(List<Planned> planned, int road, float stationM, Vector2 atM, Vector2 heading)
    {
        foreach (var plan in planned)
        {
            foreach (var (end, first) in (ReadOnlySpan<(End, bool)>)[(plan.First, true), (plan.Last, false)])
            {
                if (end.Road != road || end.RoadAlongM != stationM) continue;

                if (Vector2.Dot(Leaving(plan.Line, atM, first), heading) > _armsApartCos) return false;
            }
        }

        return true;
    }

    /// <summary>Which way a line leaves a place it ends at or ends near: toward its first point a street's width off it.</summary>
    Vector2 Leaving(Polyline line, Vector2 atM, bool first)
    {
        var points = line.PointsM;
        for (var at = 0; at < points.Length; at++)
        {
            var pointM = points[first ? at : points.Length - 1 - at];
            if (Vector2.Distance(pointM, atM) > _streetM) return Vector2.Normalize(pointM - atM);
        }

        return Vector2.Zero;
    }

    /// <summary>The junction <see cref="Station"/> would plan on an intercity road for a place along it, without planning it.</summary>
    /// <param name="over">
    /// Whether it is where a road is carried over the intercity road rather than met by it (GEN-68): that meets nothing, and
    /// keeps only its own span and an interchange's reach off every other.
    /// </param>
    float? WouldStation(int road, float alongM, bool over = false)
    {
        // An interchange is welded only to another, the nodes the road stood with being no junction a road may meet there.
        var weldM = paths.CorridorM(intercity[road]) * config.CityGen.StationWeldCorridors;
        foreach (var stationM in !_interchanged ? _stations[road] : over ? [] : _interchangesAt[road])
        {
            if (MathF.Abs(stationM - alongM) < weldM) return stationM;
        }

        var reachM = _interchanged ? InterchangeReachM(intercity[road]) : 0f;
        foreach (var stationM in _stations[road])
        {
            if (MathF.Abs(stationM - alongM) < (over ? reachM : config.CityGen.IntercityJunctionsApartLeastM)) return null;
        }

        if (!_interchanged) return alongM;

        foreach (var stationM in _interchangesAt[road])
        {
            if (MathF.Abs(stationM - alongM) < (over ? reachM : config.CityGen.InterchangesApartLeastM)) return null;
        }

        foreach (var stationM in _overAt[road])
        {
            if (MathF.Abs(stationM - alongM) < reachM) return null;
        }

        return paths.Open(intercity[road], alongM - reachM, alongM + reachM) ? alongM : null;
    }

    /// <summary>
    /// <b>The roads planned between districts joined into the pieces they make</b>: joined where one ends on another and
    /// where two edges meet, and every one ending on an intercity road joined to one more, <paramref name="outside"/>,
    /// which stands for the intercity network.
    /// </summary>
    (int[] Root, int Outside) Joined(List<Planned> planned, int seams)
    {
        var root = Clusters.Apart(planned.Count + 1);
        var outside = planned.Count;
        var endsAt = new Dictionary<Vector2, int>();
        for (var at = 0; at < planned.Count; at++)
        {
            var plan = planned[at];
            if (Reaches(plan.First) || Reaches(plan.Last)) Clusters.Union(root, at, outside);
            if (plan.First.Onto >= 0) Clusters.Union(root, at, plan.First.Onto);
            if (plan.Last.Onto >= 0) Clusters.Union(root, at, plan.Last.Onto);
            if (at >= seams) continue;

            foreach (var endM in (ReadOnlySpan<Vector2>)[plan.Line.FirstM, plan.Line.LastM])
            {
                if (endsAt.TryGetValue(endM, out var other)) Clusters.Union(root, at, other);
                else endsAt[endM] = at;
            }
        }

        return (root, outside);

        // An interchange's one side reaches only that way, and only one that crosses reaches the network both ways.
        bool Reaches(End end) => end.Road >= 0 && (!_interchanged || _crossing.Contains((end.Road, end.RoadAlongM)));
    }

    /// <summary>
    /// <b>Each road's lanes each way</b> (GEN-67): the demand between districts routed over the roads between them and
    /// along the intercity roads between the junctions planned on them (<see cref="Demand"/>), and the most-loaded share of
    /// the roads between districts laid four lanes.
    /// </summary>
    int[] Lanes(List<Planned> planned)
    {
        // The districts are the first places (Demand.Loads), and every other place is numbered after them as it is met.
        var places = _partition.Count;
        var links = new List<Demand.Link>();
        var stops = new List<(float AlongM, int Place)>[planned.Count];
        for (var at = 0; at < planned.Count; at++) stops[at] = [];

        var stations = new Dictionary<(int Road, float AlongM), int>();
        var landings = new Dictionary<(int Onto, float AlongM), int>();
        var ends = new Dictionary<Vector2, int>();
        for (var at = 0; at < planned.Count; at++)
        {
            var plan = planned[at];
            foreach (var (end, first) in (ReadOnlySpan<(End, bool)>)[(plan.First, true), (plan.Last, false)])
            {
                var place = end.Road >= 0 ? Place(stations, (end.Road, end.RoadAlongM))
                    : end.Onto >= 0 ? Place(landings, (end.Onto, end.OntoAlongM))
                    : Place(ends, first ? plan.Line.FirstM : plan.Line.LastM);
                stops[at].Add((first ? 0f : plan.Line.LengthM, place));
                if (end.Onto >= 0) stops[end.Onto].Add((end.OntoAlongM, place));
            }
        }

        // Each road a piece between each two places along it, each place reached from the districts it stands in.
        var reached = new HashSet<(int Place, int District)>();
        for (var at = 0; at < planned.Count; at++)
        {
            var plan = planned[at];
            stops[at].Sort();
            for (var stop = 0; stop < stops[at].Count; stop++)
            {
                var (alongM, place) = stops[at][stop];
                if (stop > 0) links.Add(new Demand.Link(stops[at][stop - 1].Place, place, alongM - stops[at][stop - 1].AlongM, at));

                var atM = plan.Line.At(alongM);
                foreach (var district in plan.Seam ? (ReadOnlySpan<int>)[plan.A, plan.B] : [_partition.At(atM)])
                {
                    if (district >= 0 && reached.Add((place, district)))
                    {
                        links.Add(new Demand.Link(district, place, Vector2.Distance(_partition.CentreM[district], atM), -1));
                    }
                }
            }
        }

        // Along each intercity road, between the junctions planned on it.
        for (var road = 0; road < intercity.Length; road++)
        {
            var along = new List<(float AlongM, int Place)>();
            foreach (var ((onRoad, alongM), place) in stations)
            {
                if (onRoad == road) along.Add((alongM, place));
            }

            along.Sort();
            for (var at = 1; at < along.Count; at++)
            {
                links.Add(new Demand.Link(along[at - 1].Place, along[at].Place, (along[at].AlongM - along[at - 1].AlongM) * config.CityGen.IntercityDemandWeight, -1));
            }
        }

        var loads = Demand.Loads(places, links, planned.Count, _partition.CentreM, _partition.AreaM2);
        var busiest = Demand.Busiest(loads, config.CityGen.FourLaneDistrictShare);
        var lanes = new int[planned.Count];
        for (var at = 0; at < lanes.Length; at++) lanes[at] = busiest[at] ? LayoutEdge.MostLanes : 1;

        return lanes;

        int Place<TKey>(Dictionary<TKey, int> known, TKey key)
            where TKey : notnull
        {
            if (!known.TryGetValue(key, out var place)) known[key] = place = places++;

            return place;
        }
    }

    /// <summary>
    /// <b>One planned road laid as a path</b>, each end on the node it meets — a junction of an intercity road, or a node
    /// on the road between districts it was planned onto, which was laid before it.
    /// </summary>
    /// <returns>The path it was laid as.</returns>
    int Add(List<Planned> planned, int at, int lanes, int[] pathOf)
    {
        var plan = planned[at];
        var points = plan.Line.PointsM.ToList();
        var runOn = false;
        if (EndM(plan.First, planned, pathOf, plan.Line.At(config.CityGen.InterchangeOffsetM)) is { } firstM)
        {
            if (plan.Seam) points.Insert(0, firstM);
            else points[0] = firstM;
            runOn |= plan.Seam;
        }

        if (EndM(plan.Last, planned, pathOf, plan.Line.At(plan.Line.LengthM - config.CityGen.InterchangeOffsetM)) is { } lastM)
        {
            if (plan.Seam) points.Add(lastM);
            else points[^1] = lastM;
            runOn |= plan.Seam;
        }

        if (_interchanged)
        {
            points.Reverse();
            if (plan.Last.Road >= 0) SquareOnto(points, ApproachTo(plan.Last.Road, plan.Last.RoadAlongM, points[0]));
            points.Reverse();
            if (plan.First.Road >= 0) SquareOnto(points, ApproachTo(plan.First.Road, plan.First.RoadAlongM, points[0]));
        }

        // An edge run on to the intercity road it ended against turns onto it, rounded as the rest of it is (GEN-47).
        if (runOn) points = Polyline.Resampled(Polyline.Cut(points, config.CityGen.SeamCornerCuts), config.CityGen.RouteCellM);

        return paths.Add(new Polyline([.. points]), RoadClass.Arterial, lanes, plan.Crossed);
    }

    /// <summary>
    /// Where a planned end is laid to: the node it meets, or none where it meets none or that node cannot stand — and on an
    /// interchanged intercity road, the interchange's terminal on the side the road comes from (GEN-68).
    /// </summary>
    /// <param name="inwardM">A place on the road in from its end, which says the side of an intercity road it comes from.</param>
    Vector2? EndM(End end, List<Planned> planned, int[] pathOf, Vector2 inwardM)
    {
        if (end.Road >= 0 && _interchanged)
        {
            var line = paths.All[intercity[end.Road]].Line;
            return Terminal(end.Road, end.RoadAlongM, SideOf(line, end.RoadAlongM, inwardM));
        }

        if (end.Road >= 0)
        {
            var node = paths.NodeAt(intercity[end.Road], end.RoadAlongM, 1f);
            return node < 0 ? null : paths.NodeM(node);
        }

        if (end.Onto < 0) return null;

        var onto = pathOf[end.Onto];
        var onM = planned[end.Onto].Line.At(end.OntoAlongM);
        var ontoNode = paths.NodeAt(onto, paths.All[onto].Line.Nearest(onM).AlongM, config.CityGen.DistrictJunctionsApartLeastM);
        return ontoNode < 0 ? null : paths.NodeM(ontoNode);
    }
}
