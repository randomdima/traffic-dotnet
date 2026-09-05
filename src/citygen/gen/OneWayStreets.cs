namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Which of the one-way streets the layout proposed the town keeps</b> (GEN-18): the ones it can still
/// be driven round with, and the ones that leave no lane dangling (GEN-18a). What lays a street says which
/// way it would run (<see cref="Lattice"/>); this is where that proposal meets the town the deletions left,
/// and a street the town cannot afford runs both ways again.
/// </summary>
/// <remarks>
/// <para>
/// <b>Drivable means every junction is reachable from every lane</b> — not merely that every junction has
/// a way out of it. A car is on a lane and not at a node, so the question a one-way grid can answer no to
/// is asked of the movements and never of the roads: a block whose streets all ran inwards has a way out
/// of every junction on it and is still a place a car drives into and never leaves.
/// </para>
/// <para>
/// <b>A car may not turn round in the road</b> (TER-5f), which is the whole reason this is not the
/// undirected connectivity <see cref="TownLayout.KeepTheLargestComponent"/> already keeps. A movement runs
/// on to the movements leaving the junction it arrives at, less the one back down the road it came in on —
/// so a junction of two arms with both of them running in is a trap, and the road graph is what says so.
/// </para>
/// <para>
/// <b>Kept one at a time, and never searched for.</b> The whole proposal is tried first, which is what a
/// grid the water left alone comes out as; where it does not stand, the streets are taken in the order they
/// were laid and each is kept only if the town is still drivable with it. What that leaves is a grid with a
/// handful of its streets opened again rather than a district with none — and, at worst, the town this
/// generator laid before there were one-way streets at all.
/// </para>
/// </remarks>
internal static class OneWayStreets
{
    public static void Settle(TownLayout layout)
    {
        var proposed = new RoadFlow[layout.Edges.Count];
        var oneWays = 0;
        for (var edge = 0; edge < layout.Edges.Count; edge++)
        {
            proposed[edge] = layout.Edges[edge].Flow;
            if (proposed[edge] != RoadFlow.BothWays) oneWays++;
        }

        if (oneWays == 0 || Stands(layout)) return;

        for (var edge = 0; edge < proposed.Length; edge++) layout.RunsOneWay(edge, RoadFlow.BothWays);

        for (var edge = 0; edge < proposed.Length; edge++)
        {
            if (proposed[edge] == RoadFlow.BothWays) continue;

            layout.RunsOneWay(edge, proposed[edge]);
            if (!Stands(layout)) layout.RunsOneWay(edge, RoadFlow.BothWays);
        }
    }

    /// <summary>Whether the town keeps both of the things a one-way street can cost it.</summary>
    static bool Stands(TownLayout layout) => NothingDangles(layout) && CanBeDrivenRound(layout);

    /// <summary>
    /// <b>Whether every movement the layout carries is one a car can both reach and leave</b> (GEN-18a).
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
    public static bool NothingDangles(TownLayout layout)
    {
        var nodes = layout.NodeM.Count;
        var arms = new int[nodes];
        var arriving = new int[nodes];
        var leaving = new int[nodes];
        foreach (var edge in layout.Edges)
        {
            arms[edge.From]++;
            arms[edge.To]++;
            if (edge.Flow != RoadFlow.AgainstTheRoad)
            {
                leaving[edge.From]++;
                arriving[edge.To]++;
            }

            if (edge.Flow != RoadFlow.WithTheRoad)
            {
                leaving[edge.To]++;
                arriving[edge.From]++;
            }
        }

        foreach (var edge in layout.Edges)
        {
            if (!Holds(edge, edge.From, atFromEnd: true) || !Holds(edge, edge.To, atFromEnd: false)) return false;
        }

        return true;

        bool Holds(LayoutEdge edge, int node, bool atFromEnd)
        {
            if (arms[node] < 2) return true;

            var arrivesHere = edge.Flow != (atFromEnd ? RoadFlow.WithTheRoad : RoadFlow.AgainstTheRoad);
            var leavesHere = edge.Flow != (atFromEnd ? RoadFlow.AgainstTheRoad : RoadFlow.WithTheRoad);
            if (leavesHere && arriving[node] - (arrivesHere ? 1 : 0) == 0) return false;

            return !arrivesHere || leaving[node] - (leavesHere ? 1 : 0) > 0;
        }
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
    public static bool CanBeDrivenRound(TownLayout layout)
    {
        var nodes = layout.NodeM.Count;
        var moves = layout.Edges.Count * 2;
        if (moves == 0) return true;

        var leaving = new List<int>[nodes];
        for (var node = 0; node < nodes; node++) leaving[node] = [];
        for (var edge = 0; edge < layout.Edges.Count; edge++)
        {
            if (layout.Edges[edge].Flow != RoadFlow.AgainstTheRoad) leaving[layout.Edges[edge].From].Add(edge * 2);
            if (layout.Edges[edge].Flow != RoadFlow.WithTheRoad) leaving[layout.Edges[edge].To].Add((edge * 2) + 1);
        }

        var piece = Pieces(layout, leaving, moves, out var pieces);
        if (pieces == 0) return true;

        // A piece nothing leaves is where a car ends up; every one of them has to reach the whole town.
        var open = new bool[pieces];
        for (var move = 0; move < moves; move++)
        {
            if (piece[move] < 0) continue;

            foreach (var onto in leaving[Arrives(layout, move)])
            {
                if (onto / 2 != move / 2 && piece[onto] != piece[move]) open[piece[move]] = true;
            }
        }

        var reaches = new int[pieces];
        var seenBy = new int[nodes];
        Array.Fill(seenBy, -1);
        var order = Sorted(piece, moves, pieces);
        foreach (var move in order)
        {
            var arrives = Arrives(layout, move);
            if (seenBy[arrives] == piece[move]) continue;

            seenBy[arrives] = piece[move];
            reaches[piece[move]]++;
        }

        for (var at = 0; at < pieces; at++)
        {
            if (!open[at] && reaches[at] < nodes) return false;
        }

        return true;
    }

    /// <summary>Which junction a movement arrives at: the far end of its road, read the way it is driven.</summary>
    static int Arrives(TownLayout layout, int move) =>
        move % 2 == 0 ? layout.Edges[move / 2].To : layout.Edges[move / 2].From;

    /// <summary>
    /// Every movement's own strongly connected piece of the graph, by Tarjan's, iteratively — a town is
    /// thousands of movements deep and the recursion is the stack this cannot have.
    /// </summary>
    static int[] Pieces(TownLayout layout, List<int>[] leaving, int moves, out int pieces)
    {
        var index = new int[moves];
        var low = new int[moves];
        var piece = new int[moves];
        var standing = new bool[moves];
        Array.Fill(index, -1);
        Array.Fill(piece, -1);

        var found = 0;
        var open = new Stack<int>();
        var walk = new Stack<(int Move, int At)>();
        pieces = 0;

        for (var root = 0; root < moves; root++)
        {
            if (index[root] >= 0 || !Runs(layout, root)) continue;

            Enter(root);
            while (walk.Count > 0)
            {
                var (move, at) = walk.Pop();
                var onward = leaving[Arrives(layout, move)];
                if (at < onward.Count)
                {
                    walk.Push((move, at + 1));
                    var onto = onward[at];

                    // Nothing turns round in the road (TER-5f), so the way back down it is not a movement on.
                    if (onto / 2 == move / 2) continue;

                    if (index[onto] < 0) Enter(onto);
                    else if (standing[onto]) low[move] = Math.Min(low[move], index[onto]);

                    continue;
                }

                if (low[move] == index[move])
                {
                    int closed;
                    do
                    {
                        closed = open.Pop();
                        standing[closed] = false;
                        piece[closed] = pieces;
                    }
                    while (closed != move);

                    pieces++;
                }

                if (walk.Count > 0)
                {
                    var behind = walk.Peek().Move;
                    low[behind] = Math.Min(low[behind], low[move]);
                }
            }
        }

        return piece;

        void Enter(int move)
        {
            index[move] = low[move] = found++;
            open.Push(move);
            standing[move] = true;
            walk.Push((move, 0));
        }
    }

    /// <summary>Whether the road is driven this way at all, which is what a one-way street says of one of its two.</summary>
    static bool Runs(TownLayout layout, int move) =>
        layout.Edges[move / 2].Flow == RoadFlow.BothWays
        || (layout.Edges[move / 2].Flow == RoadFlow.WithTheRoad) == (move % 2 == 0);

    /// <summary>The movements gathered piece by piece, so what one piece reaches is counted in one pass.</summary>
    static int[] Sorted(int[] piece, int moves, int pieces)
    {
        var first = new int[pieces + 1];
        for (var move = 0; move < moves; move++)
        {
            if (piece[move] >= 0) first[piece[move] + 1]++;
        }

        for (var at = 1; at <= pieces; at++) first[at] += first[at - 1];

        var order = new int[first[pieces]];
        var cursor = (int[])first.Clone();
        for (var move = 0; move < moves; move++)
        {
            if (piece[move] >= 0) order[cursor[piece[move]]++] = move;
        }

        return order;
    }
}
