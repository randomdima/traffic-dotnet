using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>A node that carries one road through it is not a junction, and the plan does not carry one</b>
/// (GEN-12b): the two arms swept onto one tangent there (GEN-12a) are joined into a single road and the node
/// goes with them. What is left is a street that bends drawn as one line, rather than two lines meeting at a
/// point no driver decides anything at.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only where the sweep actually took.</b> A pair with room for nothing better than the fillet keeps its
/// junction (<see cref="RoadStage.Bends"/>), because the corner there is one a car turns across rather than
/// one it drives round. <b>And only where the road is the same road on both sides of the node</b>: the class
/// it is laid at and the ways it is driven are facts about a road and not about a stretch of one, so two
/// arms that disagree about either are two roads whatever the geometry does. A roundabout's ring is the case
/// that makes the second clause load-bearing — its arcs meet tangentially, so the sweep has nothing to do
/// there and every ring node would otherwise be joined away (GEN-19).
/// </para>
/// <para>
/// <b>The chains are joined and never redrawn</b> — the two arrays end to end, and the bend's two halves
/// left as the two pieces they were laid in. Rubbing out the joint between them
/// (<see cref="Spline.JoinedInto"/>) would be a piece fewer and a crease at the joint after it: what that
/// measures is how far the shape moves, so two halves of one circle that disagree in the last bits of a
/// float are merged onto whichever radius came first and the chain leaves the bend pointing somewhere else.
/// <b>Two tangent halves of one circle are one arc</b> wherever the line is read, which is what GEN-12a
/// asks for and all it asks for.
/// </para>
/// <para>
/// <b>The layout handed back carries chords through nodes that are gone</b>, so the arm bearings it rebuilds
/// are those chords' and no longer the carriageways'. Nothing below the road stage reads them: what is asked
/// of the layout there is which class a road is, which ways it is driven and which nodes it runs between.
/// </para>
/// </remarks>
internal static class ThroughRoads
{
    /// <summary>The roads that are left, each as the chain it is now drawn on and the width it is laid at.</summary>
    internal readonly record struct Laid(ArcSeg[][] Chains, float[] WidthM, Vector2[] CentreM);

    /// <summary>
    /// Every through node joined out of the town at once, the layout rewritten onto the nodes and roads that
    /// are left.
    /// </summary>
    /// <param name="swept">
    /// Which nodes the bend pass carried one road through, node for node (<see cref="RoadStage.Bends"/>).
    /// </param>
    public static Laid Join(
        TownLayout layout, ArcSeg[][] chains, float[] widthM, Vector2[] centreM, ReadOnlySpan<bool> swept)
    {
        var nodeCount = centreM.Length;
        var roadCount = chains.Length;

        // Counted over every road and not only the drawn ones, so a node an undrawn road still names is a
        // node nothing here may take away from it.
        var armsAt = new List<int>[nodeCount];
        for (var node = 0; node < nodeCount; node++) armsAt[node] = [];
        for (var road = 0; road < roadCount; road++)
        {
            armsAt[layout.Edges[road].From].Add(road);
            armsAt[layout.Edges[road].To].Add(road);
        }

        var runsThrough = new bool[nodeCount];
        for (var node = 0; node < nodeCount; node++)
        {
            runsThrough[node] = swept[node] && armsAt[node].Count == 2
                                && TheSameRoad(layout.Edges[armsAt[node][0]], layout.Edges[armsAt[node][1]]);
        }

        var taken = new bool[roadCount];
        var edges = new List<LayoutEdge>(roadCount);
        var joined = new List<ArcSeg[]>(roadCount);
        var widths = new List<float>(roadCount);
        var pieces = new List<ArcSeg>();

        for (var road = 0; road < roadCount; road++)
        {
            if (taken[road] || chains[road].Length == 0) continue;

            if (!runsThrough[layout.Edges[road].From]) Walk(layout.Edges[road].From, road);
            else if (!runsThrough[layout.Edges[road].To]) Walk(layout.Edges[road].To, road);
        }

        // <b>A run every one of whose nodes carries it through is a ring with no junction on it</b> — a loop
        // of street a car could be driven round for ever and never leave. It keeps the node its first road
        // starts at, which is as arbitrary as the ring is and is the whole of what has to be decided.
        for (var road = 0; road < roadCount; road++)
        {
            if (taken[road] || chains[road].Length == 0) continue;

            runsThrough[layout.Edges[road].From] = false;
            Walk(layout.Edges[road].From, road);
        }

        for (var road = 0; road < roadCount; road++)
        {
            if (taken[road]) continue;

            edges.Add(layout.Edges[road]);
            joined.Add(chains[road]);
            widths.Add(widthM[road]);
        }

        var kept = new bool[nodeCount];
        for (var node = 0; node < nodeCount; node++) kept[node] = !runsThrough[node];
        foreach (var edge in edges)
        {
            kept[edge.From] = true;
            kept[edge.To] = true;
        }

        var moved = new int[nodeCount];
        var nodeM = new List<Vector2>(nodeCount);
        var centres = new List<Vector2>(nodeCount);
        for (var node = 0; node < nodeCount; node++)
        {
            if (!kept[node])
            {
                moved[node] = -1;
                continue;
            }

            moved[node] = nodeM.Count;
            nodeM.Add(layout.NodeM[node]);
            centres.Add(centreM[node]);
        }

        for (var road = 0; road < edges.Count; road++)
        {
            edges[road] = edges[road] with { From = moved[edges[road].From], To = moved[edges[road].To] };
        }

        layout.Rebuilt(nodeM, edges);
        return new Laid([.. joined], [.. widths], [.. centres]);

        // One run of road walked out of a node it is not carried through, taking every road it meets at a
        // node that does carry it, and written down as the one road it is.
        void Walk(int from, int first)
        {
            pieces.Clear();
            var at = from;
            var road = first;
            while (true)
            {
                taken[road] = true;
                var edge = layout.Edges[road];
                var beyond = edge.From == at ? edge.To : edge.From;
                Append(pieces, chains[road], backwards: edge.From != at);
                at = beyond;
                if (!runsThrough[at]) break;

                var next = armsAt[at][0] == road ? armsAt[at][1] : armsAt[at][0];
                if (taken[next]) break;

                // <b>A run never closes on itself</b>: a road from a junction back to the same junction is a
                // ring with one place on it, and the two ends every road has would be the same end. The node
                // the run would have closed through keeps its junction instead.
                var beyondNext = layout.Edges[next].From == at ? layout.Edges[next].To : layout.Edges[next].From;
                if (beyondNext == from) break;

                road = next;
            }

            edges.Add(layout.Edges[first] with { From = from, To = at });
            joined.Add([.. pieces]);
            widths.Add(widthM[first]);
        }
    }

    /// <summary>
    /// Whether the two arms at a node are stretches of one road rather than two roads that meet: driven the
    /// same ways, and neither of them a thing with a shape of its own.
    /// </summary>
    /// <remarks>
    /// <b>What class each was laid at is not asked</b>, because a plan does not carry it: a class decides how
    /// wide a road is, how far it may wander and how tightly it may bend, and all three are spent by the time
    /// there is a chain to join. What a bridge and a ring are is the exception and it is not a class — it is
    /// that each is a shape settled somewhere else and answered for as a whole (GEN-14a, GEN-19).
    /// </remarks>
    static bool TheSameRoad(LayoutEdge first, LayoutEdge second) =>
        first.Flow == second.Flow
        && first.Flow == RoadFlow.BothWays
        && OwnsItsShape(first.Class) is false
        && OwnsItsShape(second.Class) is false;

    static bool OwnsItsShape(RoadClass roadClass) =>
        roadClass is RoadClass.Bridge or RoadClass.Roundabout;

    /// <summary>One chain onto the end of a run, walked the way the run is going.</summary>
    static void Append(List<ArcSeg> into, ArcSeg[] chain, bool backwards)
    {
        if (!backwards)
        {
            into.AddRange(chain);
            return;
        }

        var reversed = new ArcSeg[chain.Length];
        Spline.ReverseInto(chain, reversed);
        into.AddRange(reversed);
    }
}
