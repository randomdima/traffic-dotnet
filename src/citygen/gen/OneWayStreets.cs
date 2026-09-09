using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Which streets of the town run one way</b> (GEN-18): scattered across the whole of it, never two of
/// them at one junction, and only the ones it can still be driven round with (GEN-18a). It runs on the
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

        var chosen = Scattered(layout, config.CityGen.OneWayApartMinM);

        for (var edge = 0; edge < chosen.Length; edge++)
        {
            if (chosen[edge] != RoadFlow.BothWays) layout.RunsOneWay(edge, chosen[edge]);
        }

        if (Stands(layout)) return;

        for (var edge = 0; edge < chosen.Length; edge++) layout.RunsOneWay(edge, laid[edge]);

        for (var edge = 0; edge < chosen.Length; edge++)
        {
            if (chosen[edge] == RoadFlow.BothWays) continue;

            layout.RunsOneWay(edge, chosen[edge]);
            if (!Stands(layout)) layout.RunsOneWay(edge, laid[edge]);
        }
    }

    /// <summary>
    /// The streets proposed to run one way, and which way each of them runs: <b>no two at one junction and
    /// none within <paramref name="apartM"/> of one already taken</b> (GEN-18).
    /// </summary>
    /// <remarks>
    /// <b>Streets alone, and only where both ends fork</b>. An arterial is how a district is reached and
    /// runs both ways; a street at a node of fewer than three arms is a lane nothing could ever arrive on
    /// (GEN-18a), so taking one there spends a place in the scatter on a street the settling would open
    /// again. <b>Which way it runs is an alternation and nothing more</b> — a scattered street has no
    /// family to align with, and both directions being drawn is all the town wants of it.
    /// </remarks>
    static RoadFlow[] Scattered(TownLayout layout, float apartM)
    {
        var chosen = new RoadFlow[layout.Edges.Count];
        var arms = layout.Arms();
        var taken = OnAnArterial(layout);
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

            taken[street.From] = true;
            taken[street.To] = true;
            chosen[edge] = (middleM.Count & 1) == 0 ? RoadFlow.WithTheRoad : RoadFlow.AgainstTheRoad;
            middleM.Add(atM);
        }

        return chosen;
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
