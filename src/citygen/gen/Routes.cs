using System.Numerics;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The way a road finds across the land</b> (GEN-63, GEN-69): a search over square cells the barriers block, which
/// crosses a river or a railway only at a crossing found for it (<see cref="Crossings"/>) — so what it crosses, it
/// crosses short and square, and nothing else of the water or the track is ever under it.
/// </summary>
/// <remarks>
/// <b>A route is corners and not a road.</b> It comes back as the cells it stepped through and the crossings it took, each
/// crossing's own two ends and the stretch held on its line either side, and the caller rounds it into a line a road can
/// be laid along (<see cref="Polyline.Rounded"/>).
/// </remarks>
internal sealed class Routes
{
    readonly CellGrid _grid;
    readonly bool[] _blocked;
    readonly List<Crossing> _crossings = [];

    /// <summary>The crossings that leave from each cell, each one way: a crossing is filed from both its ends.</summary>
    readonly Dictionary<int, List<int>> _leaving = [];

    readonly float _approachM;

    /// <param name="blocked">Whether a road may not stand at a place: the water and the railway, and their clearances.</param>
    /// <param name="approachM">How far either side of a crossing a road holds its line (<see cref="Crossing.NearApproachM"/>).</param>
    public Routes(CellGrid grid, Func<Vector2, bool> blocked, IEnumerable<Crossing> crossings, float approachM)
    {
        _grid = grid;
        _approachM = approachM;
        _blocked = new bool[grid.Count];
        for (var cell = 0; cell < grid.Count; cell++) _blocked[cell] = blocked(grid.CentreOf(cell));

        foreach (var crossing in crossings)
        {
            foreach (var way in (ReadOnlySpan<Crossing>)[crossing, crossing.Reversed])
            {
                var from = grid.IndexOf(way.NearApproachM(approachM));
                var to = grid.IndexOf(way.FarApproachM(approachM));
                if (_blocked[from] || _blocked[to]) continue;

                if (!_leaving.TryGetValue(from, out var leaving)) _leaving[from] = leaving = [];
                leaving.Add(_crossings.Count);
                _crossings.Add(way);
            }
        }
    }

    public CellGrid Grid => _grid;

    public bool Blocked(Vector2 atM) => _blocked[_grid.IndexOf(atM)];

    /// <summary>
    /// <b>The cheapest way from a place to any cell a goal holds</b>, as corners — or none where there is none. Every
    /// crossing taken costs <paramref name="crossingWeight"/> times its own length, so a road crosses a barrier where it has
    /// to and not wherever the crossing happens to be shorter than the way round.
    /// </summary>
    /// <param name="towardM">Where the goal lies, which the search is steered toward and never held to.</param>
    public List<RouteCorner>? Find(Vector2 fromM, Func<int, bool> goal, Vector2 towardM, float crossingWeight)
    {
        var start = _grid.IndexOf(fromM);
        var costM = new Dictionary<int, float> { [start] = 0f };
        var came = new Dictionary<int, (int From, int Crossing)>();
        var open = new PriorityQueue<int, float>();
        open.Enqueue(start, Vector2.Distance(fromM, towardM));
        var closed = new HashSet<int>();

        while (open.TryDequeue(out var cell, out _))
        {
            if (!closed.Add(cell)) continue;
            if (goal(cell)) return Walked(cell, came, fromM);

            var (column, row) = (cell % _grid.Columns, cell / _grid.Columns);
            var hereM = costM[cell];
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if ((dx | dy) == 0 || !_grid.Holds(column + dx, row + dy)) continue;

                    var next = _grid.Index(column + dx, row + dy);
                    if (_blocked[next] && next != start) continue;

                    // A diagonal step past a blocked corner would clip it.
                    if (dx != 0 && dy != 0 && (_blocked[_grid.Index(column + dx, row)] || _blocked[_grid.Index(column, row + dy)])) continue;

                    Offer(next, hereM + (_grid.CellM * ((dx != 0 && dy != 0) ? MathF.Sqrt(2f) : 1f)), cell, -1);
                }
            }

            if (!_leaving.TryGetValue(cell, out var leaving)) continue;

            foreach (var crossing in leaving)
            {
                var way = _crossings[crossing];
                Offer(_grid.IndexOf(way.FarApproachM(_approachM)), hereM + (way.SpanM * crossingWeight) + (2f * _approachM), cell, crossing);
            }
        }

        return null;

        void Offer(int next, float nextM, int from, int crossing)
        {
            if (closed.Contains(next) || (costM.TryGetValue(next, out var knownM) && knownM <= nextM)) return;

            costM[next] = nextM;
            came[next] = (from, crossing);
            open.Enqueue(next, nextM + Vector2.Distance(_grid.CentreOf(next), towardM));
        }
    }

    /// <summary>The corners a search walked, from its start to the goal it reached, each crossing laid as its own straight.</summary>
    List<RouteCorner> Walked(int goal, Dictionary<int, (int From, int Crossing)> came, Vector2 fromM)
    {
        var backwards = new List<RouteCorner>();
        var cell = goal;
        while (came.TryGetValue(cell, out var step))
        {
            if (step.Crossing < 0)
            {
                backwards.Add(new RouteCorner(_grid.CentreOf(cell), -1));
            }
            else
            {
                var way = _crossings[step.Crossing];
                backwards.Add(new RouteCorner(way.FarApproachM(_approachM), step.Crossing));
                backwards.Add(new RouteCorner(way.FarM, step.Crossing));
                backwards.Add(new RouteCorner(way.NearM, step.Crossing));
                backwards.Add(new RouteCorner(way.NearApproachM(_approachM), step.Crossing));
            }

            cell = step.From;
        }

        backwards.Add(new RouteCorner(fromM, -1));
        backwards.Reverse();
        return backwards;
    }

    /// <summary>The crossing a route's corner belongs to.</summary>
    public Crossing CrossingOf(int crossing) => _crossings[crossing];

    /// <summary>
    /// <b>A route as the line a road is laid along</b>: the cells it stepped through thinned to the corners that keep them
    /// within a cell, every crossing's own corners kept exactly, and every corner rounded at the radius
    /// (<see cref="Polyline.Rounded"/>) — so it holds each crossing's line through the crossing and for half its
    /// approach either side.
    /// </summary>
    /// <remarks>
    /// <b>No cell's corner stands within the radius of a crossing's approach</b>: the cell a route steps into off a crossing
    /// is a few metres past its approach, and a corner there leaves a leg no corner of the radius fits on, so the road
    /// laid along it is refused for turning tighter than its class.
    /// </remarks>
    public static Polyline Line(List<RouteCorner> corners, float radiusM, float stepM, float toleranceM)
    {
        var kept = new List<Vector2>();
        var run = new List<Vector2>();
        var held = -1;
        foreach (var corner in corners)
        {
            if (corner.Crossing < 0)
            {
                run.Add(corner.AtM);
                continue;
            }

            Flush(corner.AtM);
            if (kept.Count == 0 || Vector2.DistanceSquared(kept[^1], corner.AtM) > 1e-6f) kept.Add(corner.AtM);
            held = kept.Count - 1;
        }

        Flush(null);
        return new Polyline([.. Polyline.Rounded(kept, radiusM, stepM)]);

        // The run up to a crossing's approach, or to the route's end: kept clear of the approach either side of it.
        void Flush(Vector2? crossingM)
        {
            var thinned = Polyline.Thinned(run, toleranceM);
            for (var at = 0; at < thinned.Count; at++)
            {
                var atM = thinned[at];
                var last = at == thinned.Count - 1 && crossingM is null;
                var nearTheLast = held >= 0 && Vector2.Distance(kept[held], atM) < radiusM;
                var nearTheNext = crossingM is { } nextM && Vector2.Distance(nextM, atM) < radiusM;
                if (kept.Count > 0 && !last && (nearTheLast || nearTheNext)) continue;

                if (kept.Count == 0 || Vector2.DistanceSquared(kept[^1], atM) > 1e-6f) kept.Add(atM);
            }

            run.Clear();
        }
    }
}

/// <summary>One corner of a route: a place, and the crossing it is an end of, or −1 where it is a cell's.</summary>
internal readonly record struct RouteCorner(Vector2 AtM, int Crossing);
