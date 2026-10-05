namespace TrafficSimulation.CityGen.Traced;

internal static partial class TracedStreets
{
    /// <summary>
    /// <b>What became of the roads of one lane both ways share</b> (<see cref="OneWayShared"/>): how many were run one
    /// way, and how many were taken out as a spur or as part of a piece of nothing else.
    /// </summary>
    internal readonly record struct SharedLaid(int Directed, int Spurs, int Stranded);

    /// <summary>
    /// <b>A lane both ways share is driven one way</b> (GEN-57): no car can meet another on one, so each road of one is
    /// run whichever way still lets every junction reach every junction it reached before, and is taken out where
    /// neither way does — a spur, a dead end however it runs — and so is any piece of the network left holding nothing
    /// but such roads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked of the junctions, a strongly connected piece of them at a time</b>: a shared road on a cycle of its
    /// piece's roads can always be run one way or the other with the piece still strongly connected, and one on no
    /// cycle — a bridge of the piece — never can (Boesch and Tindell, <i>Robbins's theorem for mixed multigraphs</i>,
    /// 1980). So the bridges go first, and each road left is run with itself where its far end reaches its near end
    /// without it, against itself where the near end reaches the far.
    /// </para>
    /// <para>
    /// <b>Nothing it runs one way dangles</b> (GEN-50): the junction it arrives at still reaches on, by a road other
    /// than itself, and the one it leaves is still reached.
    /// </para>
    /// </remarks>
    static SharedLaid OneWayShared(List<Road> roads, int junctions)
    {
        var network = new SharedNetwork(roads, junctions);
        var spurs = network.TakeOutBridges(network.Pieces());
        var stranded = network.TakeOutStranded();
        var directed = 0;
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads[road].Gone || !roads[road].Carriage.Shared) continue;

            if (network.WayRound(road) is not { } withItself)
            {
                roads[road].Gone = true;
                spurs++;
                continue;
            }

            if (!withItself) roads[road].Turn();
            roads[road].Carriage = roads[road].Carriage with { Lanes = new RoadLanes(1, 0), Shared = false };
            directed++;
        }

        roads.RemoveAll(road => road.Gone);
        return new SharedLaid(directed, spurs, stranded);
    }

    /// <summary>The junctions and the roads between them, a shared road driven both ways until it is run one.</summary>
    sealed class SharedNetwork
    {
        readonly List<Road> _roads;
        readonly int _junctions;
        readonly List<int>[] _ends;
        readonly Queue<int> _with = new();
        readonly Queue<int> _against = new();
        readonly int[] _seenWith;
        readonly int[] _seenAgainst;
        int _search;

        public SharedNetwork(List<Road> roads, int junctions)
        {
            _roads = roads;
            _junctions = junctions;
            _ends = new List<int>[junctions];
            for (var junction = 0; junction < junctions; junction++) _ends[junction] = [];
            for (var road = 0; road < roads.Count; road++)
            {
                _ends[roads[road].From].Add(road);
                if (roads[road].To != roads[road].From) _ends[roads[road].To].Add(road);
            }

            _seenWith = new int[junctions];
            _seenAgainst = new int[junctions];
        }

        /// <summary>Every junction's strongly connected piece, by Tarjan's, iteratively — a city is thousands of junctions deep.</summary>
        public int[] Pieces()
        {
            var index = new int[_junctions];
            var low = new int[_junctions];
            var piece = new int[_junctions];
            var standing = new bool[_junctions];
            Array.Fill(index, -1);
            var open = new Stack<int>();
            var walk = new Stack<(int Junction, int At)>();
            var (found, pieces) = (0, 0);
            for (var root = 0; root < _junctions; root++)
            {
                if (index[root] >= 0) continue;

                Enter(root);
                while (walk.Count > 0)
                {
                    var (junction, at) = walk.Pop();
                    if (at < _ends[junction].Count)
                    {
                        walk.Push((junction, at + 1));
                        var road = _ends[junction][at];
                        if (!Leaves(road, junction)) continue;

                        var next = Across(road, junction);
                        if (index[next] < 0) Enter(next);
                        else if (standing[next]) low[junction] = Math.Min(low[junction], index[next]);

                        continue;
                    }

                    if (low[junction] == index[junction])
                    {
                        int closed;
                        do
                        {
                            closed = open.Pop();
                            standing[closed] = false;
                            piece[closed] = pieces;
                        }
                        while (closed != junction);

                        pieces++;
                    }

                    if (walk.Count > 0)
                    {
                        var behind = walk.Peek().Junction;
                        low[behind] = Math.Min(low[behind], low[junction]);
                    }
                }
            }

            return piece;

            void Enter(int junction)
            {
                index[junction] = low[junction] = found++;
                open.Push(junction);
                standing[junction] = true;
                walk.Push((junction, 0));
            }
        }

        /// <summary>
        /// Takes out every shared road that is a bridge of its piece — on no cycle of the piece's roads, whichever way
        /// each runs — and says how many.
        /// </summary>
        public int TakeOutBridges(int[] piece)
        {
            var index = new int[_junctions];
            var low = new int[_junctions];
            Array.Fill(index, -1);
            var walk = new Stack<(int Junction, int Through, int At)>();
            var bridges = new List<int>();
            var found = 0;
            for (var root = 0; root < _junctions; root++)
            {
                if (index[root] >= 0) continue;

                index[root] = low[root] = found++;
                walk.Push((root, -1, 0));
                while (walk.Count > 0)
                {
                    var (junction, through, at) = walk.Pop();
                    if (at < _ends[junction].Count)
                    {
                        walk.Push((junction, through, at + 1));
                        var road = _ends[junction][at];
                        var next = Across(road, junction);
                        if (road == through || _roads[road].Gone || piece[next] != piece[junction]) continue;

                        if (index[next] < 0)
                        {
                            index[next] = low[next] = found++;
                            walk.Push((next, road, 0));
                        }
                        else
                        {
                            low[junction] = Math.Min(low[junction], index[next]);
                        }

                        continue;
                    }

                    if (through < 0) continue;

                    var parent = Across(through, junction);
                    low[parent] = Math.Min(low[parent], low[junction]);
                    if (low[junction] > index[parent] && _roads[through].Carriage.Shared) bridges.Add(through);
                }
            }

            foreach (var road in bridges) _roads[road].Gone = true;
            return bridges.Count;
        }

        /// <summary>
        /// Takes out every piece of the network, joined whichever way its roads run, that holds nothing but shared
        /// roads, and says how many roads that was.
        /// </summary>
        public int TakeOutStranded()
        {
            var seen = new bool[_junctions];
            var queue = new Queue<int>();
            var gathered = new List<int>();
            var stranded = 0;
            for (var root = 0; root < _junctions; root++)
            {
                if (seen[root]) continue;

                seen[root] = true;
                queue.Enqueue(root);
                gathered.Clear();
                var sharedOnly = true;
                while (queue.TryDequeue(out var junction))
                {
                    foreach (var road in _ends[junction])
                    {
                        if (_roads[road].Gone) continue;

                        if (_roads[road].From == junction)
                        {
                            gathered.Add(road);
                            sharedOnly &= _roads[road].Carriage.Shared;
                        }

                        var next = Across(road, junction);
                        if (seen[next]) continue;

                        seen[next] = true;
                        queue.Enqueue(next);
                    }
                }

                if (!sharedOnly) continue;

                foreach (var road in gathered) _roads[road].Gone = true;
                stranded += gathered.Count;
            }

            return stranded;
        }

        /// <summary>
        /// <b>Which way a shared road can run and still be come back from</b>: true with itself, where its far end
        /// reaches its near end without it; false against itself, where the near end reaches the far; null where
        /// neither does.
        /// </summary>
        /// <remarks>
        /// Both are searched at once, a junction each in turn, so the nearer way round answers and a way that never
        /// comes round costs no more than the other took.
        /// </remarks>
        public bool? WayRound(int road)
        {
            _search++;
            var (from, to) = (_roads[road].From, _roads[road].To);
            _with.Clear();
            _against.Clear();
            _with.Enqueue(to);
            _seenWith[to] = _search;
            _against.Enqueue(from);
            _seenAgainst[from] = _search;
            while (_with.Count > 0 || _against.Count > 0)
            {
                if (Reaches(_with, _seenWith, road, from)) return true;
                if (Reaches(_against, _seenAgainst, road, to)) return false;
            }

            return null;
        }

        /// <summary>One junction of a search taken off its queue: whether a road leaving it, other than the one asked about, reaches the target.</summary>
        bool Reaches(Queue<int> queue, int[] seen, int asked, int target)
        {
            if (!queue.TryDequeue(out var junction)) return false;

            foreach (var road in _ends[junction])
            {
                if (road == asked || !Leaves(road, junction)) continue;

                var next = Across(road, junction);
                if (next == target) return true;
                if (seen[next] == _search) continue;

                seen[next] = _search;
                queue.Enqueue(next);
            }

            return false;
        }

        /// <summary>Whether a road is driven away from one of its junctions: a shared one is, until it is run one way.</summary>
        bool Leaves(int road, int junction)
        {
            var laid = _roads[road];
            if (laid.Gone) return false;
            if (laid.Carriage.Shared) return true;

            return laid.Carriage.Lanes.Flow != (junction == laid.From ? RoadFlow.AgainstTheRoad : RoadFlow.WithTheRoad);
        }

        int Across(int road, int junction) => _roads[road].From == junction ? _roads[road].To : _roads[road].From;
    }
}
