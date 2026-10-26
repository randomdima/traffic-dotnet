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
    /// <summary>How far apart along an intercity road the places a district fronts it are asked.</summary>
    const float StationStepM = 50f;

    /// <summary>How much less a metre along an intercity road costs than one along a district road: it is the faster.</summary>
    const float IntercityWeight = 0.6f;

    /// <summary>How many times an edge's corners are cut: enough that its steps off the cells are gone and it bends smoothly.</summary>
    const int EdgeCuts = 4;

    /// <summary>How far, in a district's widest spacing, a link may run on to reach its district's edge road.</summary>
    const float ToTheEdgeRoadInBlocks = 2f;


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

        var lanes = Lanes(planned);
        var pathOf = new int[planned.Count];
        for (var at = 0; at < planned.Count; at++) pathOf[at] = Add(planned, at, lanes[at], pathOf);
    }

    /// <summary>
    /// <b>Every piece of the roads laid so far that the intercity network was left without, joined to it</b> (GEN-69) — once
    /// the paths are closed into roads and before any street is: a planned road the ground refused leaves the roads
    /// planned behind it a piece of their own, which the town would otherwise delete with its streets
    /// (<see cref="TownLayout.KeepTheLargestComponent"/>). Each is given the shortest straight district road the layout
    /// takes from one of its junctions to one of the network's, within a road in's reach; one none is taken for is left.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the layout and not of the plan</b>, since the refusals are the layout's: the plan holds every rule it
    /// can, and this is what is left of the rest.
    /// </remarks>
    public void JoinWhatWasLeft(TownLayout layout)
    {
        var edges = layout.Edges;
        var root = Clusters.Apart(layout.NodeM.Count);
        foreach (var edge in edges) Clusters.Union(root, edge.From, edge.To);

        var size = new Dictionary<int, int>();
        var outside = new HashSet<int>();
        foreach (var edge in edges)
        {
            var piece = Clusters.Find(root, edge.From);
            size[piece] = size.GetValueOrDefault(piece) + 1;
            if (edge.Class == RoadClass.Intercity) outside.Add(piece);
        }

        if (size.Count < 2) return;

        var main = size.Keys.Where(piece => outside.Count == 0 || outside.Contains(piece)).MaxBy(piece => size[piece]);
        var reachM = config.CityGen.BlockSpacingAlongMaxM * RoadInReachInBlocks;
        var shortestM = 2f * TownGen.CorridorM(config, 1);
        var onARoad = new bool[layout.NodeM.Count];
        foreach (var edge in edges) (onARoad[edge.From], onARoad[edge.To]) = (true, true);

        // Again until none is joined: a piece the network reaches only through another is joined once that one is.
        var left = size.Keys.Where(piece => piece != main).OrderByDescending(piece => size[piece]).ToList();
        for (var joinedAny = true; joinedAny;)
        {
            joinedAny = false;
            foreach (var piece in left.ToList())
            {
                if (!JoinedOn(piece)) continue;

                left.Remove(piece);
                joinedAny = true;
            }
        }

        // The shortest road the layout takes from one of the piece's junctions to one of the network's.
        bool JoinedOn(int piece)
        {
            var joined = Clusters.Find(root, main);
            var candidates = new List<(int From, int To, float M)>();
            for (var from = 0; from < onARoad.Length; from++)
            {
                if (!onARoad[from] || Clusters.Find(root, from) != piece) continue;

                for (var to = 0; to < onARoad.Length; to++)
                {
                    if (!onARoad[to] || Clusters.Find(root, to) != joined) continue;

                    var apartM = Vector2.Distance(layout.NodeM[from], layout.NodeM[to]);
                    if (apartM >= shortestM && apartM <= reachM) candidates.Add((from, to, apartM));
                }
            }

            foreach (var (from, to, _) in candidates.OrderBy(candidate => candidate.M))
            {
                if (layout.Join(from, to, RoadClass.Arterial, straight: true) < 0) continue;

                Clusters.Union(root, from, to);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// <b>A road along every edge two districts share</b>: the edge as the partition traced it, thinned and rounded at a
    /// district road's own floor (GEN-47) — and, where it ends against an intercity road, run on to a junction on it.
    /// </summary>
    void Seams(List<Planned> planned)
    {
        foreach (var (a, b, corners) in _partition.Seams())
        {
            var smoothed = Polyline.Cut(Polyline.Thinned(corners, _partition.Grid.CellM * 0.75f), EdgeCuts);
            var line = new Polyline([.. Polyline.Resampled(smoothed, config.CityGen.RouteCellM)]);
            if (line.LengthM < config.CityGen.LocalityM * 3f) continue;

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
            var reachM = paths.CorridorM(intercity[road]) + _streetM + (3f * _partition.Grid.CellM);
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
        var weldM = paths.CorridorM(intercity[road]) * 2f;
        foreach (var stationM in _stations[road])
        {
            if (MathF.Abs(stationM - alongM) < weldM) return stationM;
        }

        foreach (var stationM in _stations[road])
        {
            if (MathF.Abs(stationM - alongM) < config.CityGen.IntercityJunctionsApartLeastM) return null;
        }

        _stations[road].Add(alongM);
        return alongM;
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
        foreach (var road in town.Connectors.Roads) standing.AddRange(TownGen.CrossingsOn(road.Line, town.Connectors).Where(crossing => crossing.Bridge));

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
        // the middle of the gap is taken too, until no gap is left that a site could close.
        var every = faced.SelectMany(pair => pair.Value.Select(site => (Site: site, pair.Key.A, pair.Key.B))).ToList();
        for (var added = true; added;)
        {
            added = false;
            foreach (var river in every.Select(pick => pick.Site.Barrier).Distinct())
            {
                var alongs = standing.Concat(chosen.Select(pick => pick.Site)).Where(site => site.Barrier == river).Select(site => site.AlongM).Order().ToList();
                var open = every.Where(pick => pick.Site.Barrier == river && !alongs.Any(alongM => MathF.Abs(alongM - pick.Site.AlongM) < figures.BridgesApartLeastM)).ToList();
                foreach (var pick in open)
                {
                    var before = alongs.LastOrDefault(alongM => alongM < pick.Site.AlongM, float.NegativeInfinity);
                    var after = alongs.FirstOrDefault(alongM => alongM > pick.Site.AlongM, float.PositiveInfinity);
                    if (after - before <= figures.BridgesApartMostM || float.IsInfinity(after - before)) continue;

                    chosen.Add(pick);
                    added = true;
                    break;
                }

                if (added) break;
            }
        }

        foreach (var (site, a, b) in chosen) planned.Add(Across(planned, site, a, b));
    }

    /// <summary>A link over every railway two districts face each other across, at the level crossings whose ends stand in them.</summary>
    void LevelCrossed(List<Planned> planned)
    {
        var standing = town.Connectors.Roads.SelectMany(road => TownGen.CrossingsOn(road.Line, town.Connectors).Where(crossing => !crossing.Bridge)).ToList();
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
            var past = crossing.Unit * (3f * _partition.Grid.CellM);
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
    /// and at least one, each the shortest near the middle of its own share of the stretch.
    /// </summary>
    static IEnumerable<Crossing> Spaced(List<Crossing> sites, float apartM)
    {
        foreach (var barrier in sites.Select(site => site.Barrier).Distinct())
        {
            var on = sites.Where(site => site.Barrier == barrier).ToList();
            var (fromM, toM) = (on[0].AlongM, on[^1].AlongM);
            var picks = Math.Max(1, (int)MathF.Round((toM - fromM) / apartM));
            for (var pick = 0; pick < picks; pick++)
            {
                var middleM = fromM + ((toM - fromM) * (pick + 0.5f) / picks);
                var windowM = MathF.Max((toM - fromM) / picks * 0.25f, 1f);
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
        var reachM = MathF.Max(plan.BlockAlongM, plan.BlockAcrossM) * ToTheEdgeRoadInBlocks;
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

        return (End.None, Inside(fromM, heading, config.CityGen.BlockSpacingAlongMaxM, district));
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
            var offM = paths.CorridorM(intercity[road]) + _streetM + (2f * _partition.Grid.CellM);
            for (var alongM = StationStepM; alongM < line.LengthM - StationStepM; alongM += StationStepM)
            {
                var side = Heading.RightOf(line.Heading(alongM));
                var atM = line.At(alongM);
                fronts.Add((road, alongM, side, _partition.At(atM + (side * offM)), _partition.At(atM - (side * offM))));
            }
        }

        return fronts;
    }

    /// <summary>
    /// <b>A link over every intercity road two districts face each other across</b> (GEN-69): a crossroads on it, square to
    /// it, at a station where a district stands either side — on a junction the road may have (GEN-68) — laid as two roads
    /// in from that junction, one to each district.
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
                if (WouldStation(road, alongM) is not { } stationM) continue;

                var atM = line.At(stationM);
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

    /// <summary>How far, in a district's widest spacing, a road in from an intercity road may run to the roads it joins.</summary>
    const float RoadInReachInBlocks = 4f;

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
                if (RoadIn(planned, fronts, [.. piece]) || Across(planned, [.. piece], reaches)) given = true;
                if (given) break;
            }

            if (given) continue;

            // A district no road between districts serves at all is reached on its lattice.
            var served = new bool[_partition.Count];
            foreach (var plan in planned) (served[plan.A], served[plan.B]) = (true, true);

            foreach (var district in Enumerable.Range(0, _partition.Count).OrderBy(at => Vector2.Distance(_partition.CentreM[at], town.Connectors.CoreM)))
            {
                if (served[district] || !tried.Add(district)) continue;

                var fronting = fronts.Where(front => front.Left == district || front.Right == district).ToList();
                if (fronting.Count == 0) continue;

                var middleM = fronting.Average(front => front.AlongM);
                foreach (var (road, alongM, side, left, _) in fronting.OrderBy(front => MathF.Abs(front.AlongM - middleM)))
                {
                    if (WouldStation(road, alongM) is not { } stationM) continue;

                    var atM = paths.All[intercity[road]].Line.At(stationM);
                    var (last, inward) = Into(planned, district, atM, left == district ? side : -side);
                    if (!ArmsClear(planned, road, stationM, atM, Vector2.Normalize(inward - atM))) continue;

                    Station(road, stationM);
                    planned.Add(new Planned(district, district, new Polyline([atM, inward]), [], new End(Road: road, RoadAlongM: stationM), last, Seam: false));
                    given = true;
                    break;
                }

                if (given) break;
            }
        }
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
            planned.Add(new Planned(
                district, district, new Polyline([atM, planned[best].Line.At(bestAlongM)]), [], new End(Road: road, RoadAlongM: stationM),
                new End(Onto: best, OntoAlongM: bestAlongM), Seam: false));
            return true;
        }

        return false;
    }

    /// <summary>How far a road in to a district, or across it, may run to the roads it joins.</summary>
    float ReachOf(int district) => MathF.Max(town.Districts[district].BlockAlongM, town.Districts[district].BlockAcrossM) * RoadInReachInBlocks;

    /// <summary>
    /// <b>One road across a district</b> from a road between districts that reaches the intercity network to one of a piece
    /// that does not, both that district's — the shortest, square enough to each (GEN-13), inside the district all the way
    /// and crossing no other — or false where none is. What joins a piece no intercity road can be brought in to.
    /// </summary>
    bool Across(List<Planned> planned, int[] piece, bool[] reaches)
    {
        var (bestFrom, bestFromAlongM, bestTo, bestToAlongM, bestM) = (-1, 0f, -1, 0f, float.PositiveInfinity);
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
                    }
                }
            }
        }

        if (bestFrom < 0) return false;

        var shared = planned[bestTo].A == planned[bestFrom].A || planned[bestTo].A == planned[bestFrom].B ? planned[bestTo].A : planned[bestTo].B;
        planned.Add(new Planned(
            shared, shared, new Polyline([planned[bestFrom].Line.At(bestFromAlongM), planned[bestTo].Line.At(bestToAlongM)]), [],
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
    float? WouldStation(int road, float alongM)
    {
        var weldM = paths.CorridorM(intercity[road]) * 2f;
        foreach (var stationM in _stations[road])
        {
            if (MathF.Abs(stationM - alongM) < weldM) return stationM;
        }

        foreach (var stationM in _stations[road])
        {
            if (MathF.Abs(stationM - alongM) < config.CityGen.IntercityJunctionsApartLeastM) return null;
        }

        return alongM;
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
            if (plan.First.Road >= 0 || plan.Last.Road >= 0) Clusters.Union(root, at, outside);
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
    }

    /// <summary>
    /// <b>Each road's lanes each way</b> (GEN-67): the demand between districts routed over the roads between them and
    /// along the intercity roads, and the most-loaded share of the roads between districts laid four lanes.
    /// </summary>
    int[] Lanes(List<Planned> planned)
    {
        var links = new List<Demand.Link>();
        foreach (var plan in planned) links.Add(new Demand.Link(plan.A, plan.B, plan.Line.LengthM, 1f));

        // Along an intercity road, each district beside it is one faster step from the next.
        foreach (var road in intercity)
        {
            var line = paths.All[road].Line;
            var offM = paths.CorridorM(road) + _streetM + (2f * _partition.Grid.CellM);
            foreach (var sign in (ReadOnlySpan<float>)[1f, -1f])
            {
                var (last, lastM) = (-1, 0f);
                for (var alongM = 0f; alongM < line.LengthM; alongM += StationStepM)
                {
                    var district = _partition.At(line.At(alongM) + (Heading.RightOf(line.Heading(alongM)) * (sign * offM)));
                    if (district < 0 || district == last) continue;

                    if (last >= 0) links.Add(new Demand.Link(last, district, alongM - lastM, IntercityWeight));
                    (last, lastM) = (district, alongM);
                }
            }
        }

        var loads = Demand.Loads(links, _partition.CentreM, _partition.AreaM2);
        var busiest = Demand.Busiest(loads.AsSpan(0, planned.Count), config.CityGen.FourLaneDistrictShare);
        var lanes = new int[planned.Count];
        for (var at = 0; at < lanes.Length; at++) lanes[at] = busiest[at] ? LayoutEdge.MostLanes : 1;

        return lanes;
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
        if (EndM(plan.First, planned, pathOf) is { } firstM)
        {
            if (plan.Seam) points.Insert(0, firstM);
            else points[0] = firstM;
            runOn |= plan.Seam;
        }

        if (EndM(plan.Last, planned, pathOf) is { } lastM)
        {
            if (plan.Seam) points.Add(lastM);
            else points[^1] = lastM;
            runOn |= plan.Seam;
        }

        // An edge run on to the intercity road it ended against turns onto it, rounded as the rest of it is (GEN-47).
        if (runOn) points = Polyline.Resampled(Polyline.Cut(points, EdgeCuts), config.CityGen.RouteCellM);

        return paths.Add(new Polyline([.. points]), RoadClass.Arterial, lanes, plan.Crossed);
    }

    /// <summary>Where a planned end is laid to: the node it meets, or none where it meets none or that node cannot stand.</summary>
    Vector2? EndM(End end, List<Planned> planned, int[] pathOf)
    {
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
