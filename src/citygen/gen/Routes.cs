using System.Numerics;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The way a road finds across the land</b> (GEN-63, GEN-69): a search over square cells the barriers block, which
/// crosses a river or a railway only at a crossing found for it (<see cref="Crossings"/>) — so what it crosses, it
/// crosses short and square, and nothing else of the water or the track is ever under it.
/// </summary>
/// <remarks>
/// <b>A route is corners and not a road.</b> It comes back as the cells it stepped through and the crossings it took, each
/// crossing's own two ends and the stretch held on its line either side, and the caller draws the curve a road is laid
/// along through them (<see cref="RouteSpline"/>).
/// </remarks>
internal sealed class Routes
{
    readonly CellGrid _grid;
    readonly bool[] _blocked;
    readonly List<Crossing> _crossings = [];

    /// <summary>Each of <see cref="_crossings"/> as it was handed in, whichever way round it is filed.</summary>
    readonly List<Crossing> _given = [];

    /// <summary>The crossings that leave from each cell, each one way: a crossing is filed from both its ends.</summary>
    readonly Dictionary<int, List<int>> _leaving = [];

    readonly float _approachM, _roomM;

    /// <summary>
    /// A search's working set, a cell each and kept from one search to the next: a cell's cost and step stand only where
    /// <see cref="_seen"/> holds the search's own number, and it is closed only where <see cref="_closed"/> does — so a
    /// search clears nothing, and a failing one that floods the map costs no table grown cell by cell.
    /// </summary>
    readonly float[] _costM;

    readonly (int From, int Crossing)[] _came;
    readonly int[] _seen;
    readonly int[] _closed;
    int _search;

    /// <param name="blocked">Whether a road may not stand at a place: the water and the railway, and their clearances.</param>
    /// <param name="approachM">How far either side of a crossing a road holds its line (<see cref="Crossing.NearApproachM"/>).</param>
    /// <param name="roomM">
    /// How far past its approach a route still runs on the crossing's line: the room a road needs to turn off the straight,
    /// which a route leaves it and no other crossing takes.
    /// </param>
    public Routes(CellGrid grid, Func<Vector2, bool> blocked, IEnumerable<Crossing> crossings, float approachM, float roomM)
    {
        _grid = grid;
        (_approachM, _roomM) = (approachM, roomM);
        _blocked = new bool[grid.Count];
        for (var cell = 0; cell < grid.Count; cell++) _blocked[cell] = blocked(grid.CentreOf(cell));

        (_costM, _came, _seen, _closed) = (new float[grid.Count], new (int, int)[grid.Count], new int[grid.Count], new int[grid.Count]);

        foreach (var crossing in crossings)
        {
            foreach (var way in (ReadOnlySpan<Crossing>)[crossing, crossing.Reversed])
            {
                var from = grid.IndexOf(way.NearApproachM(approachM + roomM));
                var to = grid.IndexOf(way.FarApproachM(approachM + roomM));
                if (_blocked[from] || _blocked[to]) continue;

                if (!_leaving.TryGetValue(from, out var leaving)) _leaving[from] = leaving = [];
                leaving.Add(_crossings.Count);
                _crossings.Add(way);
                _given.Add(crossing);
            }
        }
    }

    public CellGrid Grid => _grid;

    public bool Blocked(Vector2 atM) => _blocked[_grid.IndexOf(atM)];

    /// <summary>
    /// <b>A place barred after the search was laid</b>: every cell within a reach of it, and every crossing either end of
    /// which now stands on one — where a curve could not be drawn along the way found, so the next search finds another.
    /// </summary>
    public void Bar(Vector2 atM, float reachM)
    {
        _grid.Band(atM, atM, reachM, cell => _blocked[cell] = true);
        foreach (var (from, leaving) in _leaving)
        {
            leaving.RemoveAll(crossing => _blocked[from] || _blocked[_grid.IndexOf(_crossings[crossing].FarApproachM(_approachM + _roomM))]);
        }
    }

    /// <summary>
    /// <b>A way from a place to any cell a goal holds</b>, as corners — or none where there is none. Every crossing taken
    /// costs <paramref name="crossingWeight"/> times its own length, so a road crosses a barrier where it has to and not
    /// wherever the crossing happens to be shorter than the way round.
    /// </summary>
    /// <remarks>
    /// <b>Steered toward <paramref name="towardM"/> and not held to the cheapest</b>: the goals are the core and every road
    /// already laid, and the steer is the distance to the core alone, which can overstate the way to a nearer road — so
    /// the way found is a good one and not always the best.
    /// </remarks>
    /// <param name="towardM">Where the goal lies, which the search is steered toward and never held to.</param>
    public List<RouteCorner>? Find(Vector2 fromM, Func<int, bool> goal, Vector2 towardM, float crossingWeight)
    {
        var search = ++_search;
        var start = _grid.IndexOf(fromM);
        (_costM[start], _came[start], _seen[start]) = (0f, (-1, -1), search);
        var open = new PriorityQueue<int, float>();
        open.Enqueue(start, Vector2.Distance(fromM, towardM));

        while (open.TryDequeue(out var cell, out _))
        {
            if (_closed[cell] == search) continue;

            _closed[cell] = search;
            if (goal(cell)) return Walked(cell, start, fromM);

            var (column, row) = (cell % _grid.Columns, cell / _grid.Columns);
            var hereM = _costM[cell];
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

            if (!_leaving.TryGetValue(cell, out var leaving) || !RoomedSinceACrossing(cell)) continue;

            foreach (var crossing in leaving)
            {
                var way = _crossings[crossing];
                var heldM = _approachM + _roomM;
                Offer(_grid.IndexOf(way.FarApproachM(heldM)), hereM + (way.SpanM * crossingWeight) + (2f * heldM), cell, crossing);
            }
        }

        return null;

        void Offer(int next, float nextM, int from, int crossing)
        {
            if (_closed[next] == search || (_seen[next] == search && _costM[next] <= nextM)) return;

            (_costM[next], _came[next], _seen[next]) = (nextM, (from, crossing), search);
            open.Enqueue(next, nextM + Vector2.Distance(_grid.CentreOf(next), towardM));
        }
    }

    /// <summary>
    /// Whether the way a search came to a cell runs its room clear of the last crossing it took: one taken nearer leaves
    /// a road nowhere to turn between the two straights.
    /// </summary>
    bool RoomedSinceACrossing(int cell)
    {
        var walkedM = 0f;
        for (var at = cell; walkedM < _roomM;)
        {
            var (from, crossing) = _came[at];
            if (crossing >= 0) return false;
            if (from < 0) return true;

            walkedM += Vector2.Distance(_grid.CentreOf(at), _grid.CentreOf(from));
            at = from;
        }

        return true;
    }

    /// <summary>
    /// The corners a search walked, from its start to the goal it reached, each crossing laid as its own straight and the
    /// room past either end of it as a corner of the route's own.
    /// </summary>
    List<RouteCorner> Walked(int goal, int start, Vector2 fromM)
    {
        var backwards = new List<RouteCorner>();
        for (var cell = goal; cell != start;)
        {
            var step = _came[cell];
            if (step.Crossing < 0)
            {
                backwards.Add(new RouteCorner(_grid.CentreOf(cell), -1));
            }
            else
            {
                var way = _crossings[step.Crossing];
                backwards.Add(new RouteCorner(way.FarApproachM(_approachM + _roomM), -1));
                backwards.Add(new RouteCorner(way.FarApproachM(_approachM), step.Crossing));
                backwards.Add(new RouteCorner(way.FarM, step.Crossing));
                backwards.Add(new RouteCorner(way.NearM, step.Crossing));
                backwards.Add(new RouteCorner(way.NearApproachM(_approachM), step.Crossing));
                backwards.Add(new RouteCorner(way.NearApproachM(_approachM + _roomM), -1));
            }

            cell = step.From;
        }

        backwards.Add(new RouteCorner(fromM, -1));
        backwards.Reverse();
        return backwards;
    }

    /// <summary>
    /// <b>The crossings a route took</b>, in the order it took them and each as it was handed in — not the way round it was
    /// crossed — so a caller asks what the route stands on of the route, and not of a line drawn off it.
    /// </summary>
    public List<Crossing> Taken(List<RouteCorner> corners)
    {
        var taken = new List<Crossing>();
        foreach (var corner in corners)
        {
            if (corner.Crossing < 0) continue;

            var given = _given[corner.Crossing];
            if (taken.Count == 0 || taken[^1] != given) taken.Add(given);
        }

        return taken;
    }
}

/// <summary>One corner of a route: a place, and the crossing it is an end of, or −1 where it is a cell's.</summary>
internal readonly record struct RouteCorner(Vector2 AtM, int Crossing);
