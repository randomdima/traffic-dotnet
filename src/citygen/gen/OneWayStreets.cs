using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Which streets of the town run one way</b> (GEN-18): scattered across the whole of it, never two of
/// them at one junction, and only the ones it can still be driven round with (GEN-50). It runs on the
/// layout the deletions left, so what it chooses is chosen against the town there actually is.
/// </summary>
/// <remarks>
/// <para>
/// <b>A one-way street is a street on its own.</b> Every one is entered and left by roads that admit both
/// ways, which is what keeps a district off the sinks and the traps a grid of them falls into, and what
/// leaves the choice of which streets local rather than a property of a whole lattice. The scatter is a
/// spacing between the ones already taken (<see cref="CityGenFigures.OneWayApartMinM"/>) and a raster over
/// the streets in the order they were laid — sequential inhibition, which spreads them across the town
/// without a district's bearing or the ring deciding where they may be.
/// </para>
/// <para>
/// <b>Drivable means every junction is reachable from every lane</b> — not merely that every junction has
/// a way out of it. A car is on a lane and not at a node, so the question is asked of the movements and
/// never of the roads: a block whose streets all ran inwards has a way out of every junction on it and is
/// still a place a car drives into and never leaves.
/// </para>
/// <para>
/// <b>A car may not turn round in the road</b> (TER-5f), which is the whole reason this is not the
/// undirected connectivity <see cref="TownLayout.KeepTheLargestComponent"/> already keeps. A movement runs
/// on to the movements leaving the junction it arrives at, less the one back down the road it came in on —
/// so a junction of two arms with both of them running in is a trap, and the road graph is what says so.
/// </para>
/// <para>
/// <b>Kept one at a time, and never searched for.</b> The whole scatter is tried first; where it does not
/// stand, the streets are taken in the order they were chosen and each is kept only if the town is still
/// drivable with it. Opening a street again can cost the scatter a member but never its shape — nothing is
/// ever added back — so at worst this is the town this generator laid before there were one-way streets.
/// </para>
/// </remarks>
internal static class OneWayStreets
{
    /// <remarks>
    /// <b>Opening a street again puts back the way it was laid and not two ways</b>: a roundabout's own ring
    /// was directed before this ran (GEN-19) and is none of the scatter's business, so what a street gives
    /// up is its own choice rather than every direction on the layout.
    /// </remarks>
    public static void Lay(TownLayout layout, SimConfig config)
    {
        var laid = new RoadFlow[layout.Edges.Count];
        for (var edge = 0; edge < laid.Length; edge++) laid[edge] = layout.Edges[edge].Flow;

        var chosen = Scattered(layout, config);
        var town = new Movements(layout);

        for (var edge = 0; edge < chosen.Length; edge++)
        {
            if (chosen[edge] != RoadFlow.BothWays) town.RunsOneWay(edge, chosen[edge]);
        }

        if (town.Stands()) return;

        for (var edge = 0; edge < chosen.Length; edge++) town.RunsOneWay(edge, laid[edge]);

        for (var edge = 0; edge < chosen.Length; edge++)
        {
            if (chosen[edge] == RoadFlow.BothWays) continue;

            town.RunsOneWay(edge, chosen[edge]);
            if (!town.Stands()) town.RunsOneWay(edge, laid[edge]);
        }
    }

    /// <summary>
    /// The streets proposed to run one way, and which way each of them runs: <b>no two at one junction, none
    /// within <see cref="CityGenFigures.OneWayApartMinM"/> of one already taken, and every one of them arriving somewhere the
    /// traffic already there still has a choice</b> (GEN-18).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Streets alone, and only where both ends fork</b>. An arterial is how a district is reached and
    /// runs both ways; a street at a node of fewer than three arms is a lane nothing could ever arrive on
    /// (GEN-50), so taking one there spends a place in the scatter on a street the settling would open
    /// again.
    /// </para>
    /// <para>
    /// <b>Which way it runs is where it may arrive and which half it would stand on</b>
    /// (<see cref="ArrivesWithoutStarving"/>, <see cref="HoldsItsBends"/>), and the alternation decides only
    /// between two ends that both may. A street that may run neither way is not taken.
    /// </para>
    /// </remarks>
    static RoadFlow[] Scattered(TownLayout layout, SimConfig config)
    {
        var chosen = new RoadFlow[layout.Edges.Count];
        var arms = layout.Arms();
        var taken = OnAnArterial(layout);
        var apartM = config.CityGen.OneWayApartMinM;
        var apartSqM = apartM * apartM;
        var middleM = new List<Vector2>();

        for (var edge = 0; edge < chosen.Length; edge++)
        {
            var street = layout.Edges[edge];
            if (street.Class != RoadClass.Street) continue;
            if (taken[street.From] || taken[street.To]) continue;
            if (arms[street.From] < 3 || arms[street.To] < 3) continue;

            var atM = (layout.NodeM[street.From] + layout.NodeM[street.To]) * 0.5f;
            if (StandsNearOne(middleM, atM, apartSqM)) continue;

            // Arriving at <c>To</c> is the road driven with itself, and arriving at <c>From</c> is against it.
            var toTo = ArrivesWithoutStarving(arms, street.To)
                       && HoldsItsBends(layout, config, edge, RoadFlow.WithTheRoad);
            var toFrom = ArrivesWithoutStarving(arms, street.From)
                         && HoldsItsBends(layout, config, edge, RoadFlow.AgainstTheRoad);
            if (!toTo && !toFrom) continue;

            chosen[edge] = toTo && toFrom
                ? (middleM.Count & 1) == 0 ? RoadFlow.WithTheRoad : RoadFlow.AgainstTheRoad
                : toTo ? RoadFlow.WithTheRoad : RoadFlow.AgainstTheRoad;

            taken[street.From] = true;
            taken[street.To] = true;
            middleM.Add(atM);
        }

        return chosen;
    }

    /// <summary>
    /// <b>Whether a one-way street may end at this node without taking the choice away from the traffic that
    /// meets it</b> (GEN-18, GEN-50): four arms, because a street arriving takes one way out away from every
    /// approach there and an approach may not be left with one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the arithmetic of the node and not a search.</b> At a node of <c>n</c> arms whose every other
    /// arm runs both ways — which is what the rest of this scatter guarantees (GEN-18) — an approach may leave
    /// on any arm but its own and but the one-way street it cannot drive up, so it has <c>n − 2</c> ways out.
    /// Three arms leaves one, which is a car driven through a junction it decides nothing at; four leaves two,
    /// which is a junction.
    /// </para>
    /// <para>
    /// <b>Nothing downstream has to cope with the shape because the town does not contain it.</b> A node that
    /// offers an approach one movement is a lane with nowhere to decide anything, and every layer below would
    /// then need a rule about it — a run to lay, a merge to rank, a place a lane is driven through, a link a
    /// route joins partway along. The departure end needs nothing asked of it: a street leaving takes no way
    /// out away from anybody.
    /// </para>
    /// </remarks>
    static bool ArrivesWithoutStarving(int[] arms, int node) => arms[node] >= 4;

    /// <summary>
    /// <b>Whether the street still bends no tighter than its class affords once it stands on the half its
    /// traffic drives</b> (GEN-47, TER-4d). A line moved half a lane towards the inside of its own bend is a
    /// line half a lane tighter there, so a street laid right at its floor cannot be taken one way — and the
    /// answer is the line itself offset the way <c>RoadStage.OntoTheDrivenHalf</c> will offset it, never a
    /// figure standing in for it.
    /// </summary>
    static bool HoldsItsBends(TownLayout layout, SimConfig config, int edge, RoadFlow flow)
    {
        var line = layout.LineOf(edge);
        var halfM = config.LaneOffsetM * config.RoadSideSign;
        var moved = new ArcSeg[line.Length];
        Spline.OffsetInto(line, flow == RoadFlow.WithTheRoad ? halfM : -halfM, moved);

        var floorM = RoadLines.FloorRadiusM(config, layout.Edges[edge].Class);
        foreach (var arc in moved)
        {
            if (MathF.Abs(arc.Curvature) > 1e-6f && 1f / MathF.Abs(arc.Curvature) < floorM) return false;
        }

        return true;
    }

    /// <summary>
    /// The nodes no one-way street may hang off, to begin with: <b>the ones something other than a street
    /// reaches</b>. A district is entered and left off the roads that carry the town between districts, and
    /// taking one of them one way is how a whole district ends up drivable only one way round; a
    /// roundabout's ring is directed already (GEN-19), and a second one-way road at one of its nodes is the
    /// junction GEN-18 refuses.
    /// </summary>
    static bool[] OnAnArterial(TownLayout layout)
    {
        var found = new bool[layout.NodeM.Count];
        foreach (var edge in layout.Edges)
        {
            if (edge.Class == RoadClass.Street) continue;

            found[edge.From] = true;
            found[edge.To] = true;
        }

        return found;
    }

    static bool StandsNearOne(List<Vector2> middleM, Vector2 atM, float apartSqM)
    {
        foreach (var takenM in middleM)
        {
            if (Vector2.DistanceSquared(takenM, atM) < apartSqM) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>The layout's movements, laid out once and asked again after every street is directed</b>: every
    /// movement each junction could ever send off, in edge order, and whether one runs is read off the
    /// road's flow at the time it is asked.
    /// </summary>
    /// <remarks>
    /// Only the flows change between two questions, so the roads, the movements leaving each junction and
    /// every working array are kept from one to the next. A movement that does not run is passed over where
    /// it stands, so a walk meets the ones that do in the order a list of them alone would give.
    /// </remarks>
    sealed class Movements
    {
        readonly TownLayout _layout;
        readonly int _nodes;
        readonly int _moves;
        readonly int[] _from;
        readonly int[] _to;
        readonly RoadFlow[] _flow;

        /// <summary>Where each junction's movements start in <see cref="_leaving"/>; one past the last junction's end at <c>[nodes]</c>.</summary>
        readonly int[] _firstLeaving;
        readonly int[] _leaving;

        readonly int[] _arms;
        readonly int[] _arrivingCount;
        readonly int[] _leavingCount;

        readonly int[] _index;
        readonly int[] _low;
        readonly int[] _piece;
        readonly bool[] _standing;
        readonly int[] _open;
        readonly int[] _walkMove;
        readonly int[] _walkAt;

        readonly bool[] _pieceOpen;
        readonly int[] _reaches;
        readonly int[] _seenBy;
        readonly int[] _firstOfPiece;
        readonly int[] _order;

        public Movements(TownLayout layout)
        {
            _layout = layout;
            _nodes = layout.NodeM.Count;
            var edges = layout.Edges.Count;
            _moves = edges * 2;
            _from = new int[edges];
            _to = new int[edges];
            _flow = new RoadFlow[edges];
            _firstLeaving = new int[_nodes + 1];
            for (var edge = 0; edge < edges; edge++)
            {
                var road = layout.Edges[edge];
                _from[edge] = road.From;
                _to[edge] = road.To;
                _flow[edge] = road.Flow;
                _firstLeaving[road.From + 1]++;
                _firstLeaving[road.To + 1]++;
            }

            for (var node = 1; node <= _nodes; node++) _firstLeaving[node] += _firstLeaving[node - 1];

            _leaving = new int[_moves];
            var cursor = (int[])_firstLeaving.Clone();
            for (var edge = 0; edge < edges; edge++)
            {
                _leaving[cursor[_from[edge]]++] = edge * 2;
                _leaving[cursor[_to[edge]]++] = (edge * 2) + 1;
            }

            _arms = new int[_nodes];
            _arrivingCount = new int[_nodes];
            _leavingCount = new int[_nodes];
            _index = new int[_moves];
            _low = new int[_moves];
            _piece = new int[_moves];
            _standing = new bool[_moves];
            _open = new int[_moves];
            _walkMove = new int[_moves];
            _walkAt = new int[_moves];
            _pieceOpen = new bool[_moves];
            _reaches = new int[_moves];
            _seenBy = new int[_nodes];
            _firstOfPiece = new int[_moves + 1];
            _order = new int[_moves];
        }

        /// <summary>Directs one road on the layout and here alike.</summary>
        public void RunsOneWay(int edge, RoadFlow flow)
        {
            _layout.RunsOneWay(edge, flow);
            _flow[edge] = flow;
        }

        /// <summary>Whether the town keeps both of the things a one-way street can cost it.</summary>
        public bool Stands() => NothingDangles() && CanBeDrivenRound();

        /// <summary>
        /// <b>Whether every movement the layout carries is one a car can both reach and leave</b> (GEN-50).
        /// <b>A node that forks nothing may not change how many lanes there are</b>: a road of two ways meeting a
        /// road of one leaves the way back out of that node with nothing that ever arrives on it, which is a lane
        /// drawn on the town that no car is ever on.
        /// </summary>
        /// <remarks>
        /// Asked road by road at each node rather than of the graph as a whole, because the fault is local: a
        /// movement leaving a node needs some road other than its own arriving there (TER-5f bans the way back
        /// down its own), and a movement arriving needs some other road leaving. <b>A node of one arm is passed
        /// over</b> — a dead end is a place only turning round leaves, which is
        /// <see cref="TownLayout.PruneTheDeadEnds"/>'s to drop and not this pass's to refuse.
        /// </remarks>
        bool NothingDangles()
        {
            Array.Clear(_arms);
            Array.Clear(_arrivingCount);
            Array.Clear(_leavingCount);
            for (var edge = 0; edge < _flow.Length; edge++)
            {
                var from = _from[edge];
                var to = _to[edge];
                _arms[from]++;
                _arms[to]++;
                if (_flow[edge] != RoadFlow.AgainstTheRoad)
                {
                    _leavingCount[from]++;
                    _arrivingCount[to]++;
                }

                if (_flow[edge] != RoadFlow.WithTheRoad)
                {
                    _leavingCount[to]++;
                    _arrivingCount[from]++;
                }
            }

            for (var edge = 0; edge < _flow.Length; edge++)
            {
                if (!Holds(edge, _from[edge], atFromEnd: true) || !Holds(edge, _to[edge], atFromEnd: false)) return false;
            }

            return true;
        }

        bool Holds(int edge, int node, bool atFromEnd)
        {
            if (_arms[node] < 2) return true;

            var flow = _flow[edge];
            var arrivesHere = flow != (atFromEnd ? RoadFlow.WithTheRoad : RoadFlow.AgainstTheRoad);
            var leavesHere = flow != (atFromEnd ? RoadFlow.AgainstTheRoad : RoadFlow.WithTheRoad);
            if (leavesHere && _arrivingCount[node] - (arrivesHere ? 1 : 0) == 0) return false;

            return !arrivesHere || _leavingCount[node] - (leavesHere ? 1 : 0) > 0;
        }

        /// <summary>
        /// Whether every junction of the layout can be driven to from every movement on it. <b>The movements
        /// are the graph and the roads are not</b>: one runs on to the movements leaving the junction it
        /// arrives at, less the one back down its own road (TER-5f).
        /// </summary>
        /// <remarks>
        /// A movement that cannot be come back from is one whose own piece of the graph is a sink — nothing
        /// leaves it — so the question is asked of the sinks alone: from anywhere at all a car ends up in one of
        /// them, and it is drivable exactly when each of them reaches every junction in the town. <b>A town of
        /// one loop is drivable and is not one piece</b>: its two directions never meet, and a car on either
        /// still comes to every junction there is, which is why this asks what the sinks reach and not whether
        /// there is one of them.
        /// </remarks>
        bool CanBeDrivenRound()
        {
            if (_moves == 0) return true;

            var pieces = Pieces();
            if (pieces == 0) return true;

            // A piece nothing leaves is where a car ends up; every one of them has to reach the whole town.
            Array.Clear(_pieceOpen, 0, pieces);
            for (var move = 0; move < _moves; move++)
            {
                var piece = _piece[move];
                if (piece < 0) continue;

                var arrives = Arrives(move);
                for (var at = _firstLeaving[arrives]; at < _firstLeaving[arrives + 1]; at++)
                {
                    var onto = _leaving[at];
                    if (Runs(onto) && onto / 2 != move / 2 && _piece[onto] != piece) _pieceOpen[piece] = true;
                }
            }

            Array.Clear(_reaches, 0, pieces);
            Array.Fill(_seenBy, -1);
            var sorted = Sorted(pieces);
            for (var at = 0; at < sorted; at++)
            {
                var move = _order[at];
                var arrives = Arrives(move);
                if (_seenBy[arrives] == _piece[move]) continue;

                _seenBy[arrives] = _piece[move];
                _reaches[_piece[move]]++;
            }

            for (var at = 0; at < pieces; at++)
            {
                if (!_pieceOpen[at] && _reaches[at] < _nodes) return false;
            }

            return true;
        }

        /// <summary>Which junction a movement arrives at: the far end of its road, read the way it is driven.</summary>
        int Arrives(int move) => move % 2 == 0 ? _to[move / 2] : _from[move / 2];

        /// <summary>Whether the road is driven this way at all, which is what a one-way street says of one of its two.</summary>
        bool Runs(int move) =>
            _flow[move / 2] == RoadFlow.BothWays || (_flow[move / 2] == RoadFlow.WithTheRoad) == (move % 2 == 0);

        /// <summary>
        /// Every movement's own strongly connected piece of the graph into <see cref="_piece"/>, by Tarjan's,
        /// iteratively — a town is thousands of movements deep and the recursion is the stack this cannot have.
        /// Returns how many pieces there are.
        /// </summary>
        /// <remarks>A walk's place is a position in <see cref="_leaving"/>, not a count of movements met.</remarks>
        int Pieces()
        {
            Array.Fill(_index, -1);
            Array.Fill(_piece, -1);
            Array.Clear(_standing);

            var found = 0;
            var open = 0;
            var walk = 0;
            var pieces = 0;

            for (var root = 0; root < _moves; root++)
            {
                if (_index[root] >= 0 || !Runs(root)) continue;

                Enter(root);
                while (walk > 0)
                {
                    walk--;
                    var move = _walkMove[walk];
                    var at = _walkAt[walk];
                    if (at < _firstLeaving[Arrives(move) + 1])
                    {
                        _walkAt[walk++] = at + 1;
                        var onto = _leaving[at];
                        if (!Runs(onto)) continue;

                        // Nothing turns round in the road (TER-5f), so the way back down it is not a movement on.
                        if (onto / 2 == move / 2) continue;

                        if (_index[onto] < 0) Enter(onto);
                        else if (_standing[onto]) _low[move] = Math.Min(_low[move], _index[onto]);

                        continue;
                    }

                    if (_low[move] == _index[move])
                    {
                        int closed;
                        do
                        {
                            closed = _open[--open];
                            _standing[closed] = false;
                            _piece[closed] = pieces;
                        }
                        while (closed != move);

                        pieces++;
                    }

                    if (walk > 0)
                    {
                        var behind = _walkMove[walk - 1];
                        _low[behind] = Math.Min(_low[behind], _low[move]);
                    }
                }
            }

            return pieces;

            void Enter(int move)
            {
                _index[move] = _low[move] = found++;
                _open[open++] = move;
                _standing[move] = true;
                _walkMove[walk] = move;
                _walkAt[walk++] = _firstLeaving[Arrives(move)];
            }
        }

        /// <summary>
        /// The movements gathered piece by piece into <see cref="_order"/>, so what one piece reaches is counted
        /// in one pass. Returns how many were gathered.
        /// </summary>
        int Sorted(int pieces)
        {
            var first = _firstOfPiece;
            Array.Clear(first, 0, pieces + 1);
            for (var move = 0; move < _moves; move++)
            {
                if (_piece[move] >= 0) first[_piece[move] + 1]++;
            }

            for (var at = 1; at <= pieces; at++) first[at] += first[at - 1];

            for (var move = 0; move < _moves; move++)
            {
                if (_piece[move] >= 0) _order[first[_piece[move]]++] = move;
            }

            return first[pieces];
        }
    }
}
