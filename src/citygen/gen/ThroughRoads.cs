using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Every run of roads that only passes through the nodes between them is one road</b> (GEN-51). A node
/// two roads carry on through is not a place anything meets: it is where the town's own arithmetic happened
/// to stop a line — a spacing along an arterial nothing welded onto, or a lattice point the prunes left
/// holding two of its four arms — and a junction laid there is a box, a pair of movements and a lane split
/// in the middle of a road.
/// </summary>
/// <remarks>
/// <para>
/// <b>The corner is the town's and the road keeps it</b> (<see cref="LayoutEdge.ThroughM"/>). The two arms
/// are not straightened onto the chord between their far ends — that would move the carriageway off the
/// ground it was laid on and through whatever the corner was drawn round. The node's place is handed to the
/// joined road as somewhere it passes, and every arm, spline and lane end is drawn to it
/// (<see cref="ConnectionPoints.ArmOf"/>, <c>RoadStage.Chain</c>).
/// </para>
/// <para>
/// <b>No corner is too sharp to join through</b>, because every one of them was a junction and what a car
/// held there was the movement across it: a joined road turns at a junction's own floor and not at its
/// class's (GEN-47, <see cref="RoadLines"/>), and a driver reads every arc of the line ahead of it and is
/// down to that arc's speed before it arrives.
/// </para>
/// <para>
/// <b>A run is shortened rather than given up on</b> (GEN-51), and there are two ways it is shortened. One is
/// a run that comes back where it set off, having no second end to be a road between. The other is a joined
/// road the line cannot be laid as — too tight a corner for the junction's floor, or ground another road
/// holds — and then the run is cut in two at the middle of what it passes and each half is joined afresh, so
/// the refusal costs one corner and not every place the run went through. <b>Nothing is laid and taken
/// back</b>: the joined line is drawn before the pieces are given up (<see cref="TownLayout.CouldLay"/>).
/// </para>
/// <para>
/// <b>It runs on the layout the deletions left</b>, after the stranded pieces and the dead ends
/// and before the roundabouts (<see cref="TownGenerator"/>): a ring is opened out of a junction, so what is
/// offered one has to be a junction already. Nothing here deletes a road — every run comes back as the one
/// road it was — and the nodes it empties are dropped by the prune it ends on, the way
/// <see cref="TownLayout.RingOut"/>'s husks are.
/// </para>
/// </remarks>
internal static class ThroughRoads
{
    /// <summary>
    /// Every run of roads through nodes nothing meets at, joined into the one road it is, and the nodes it
    /// emptied dropped.
    /// </summary>
    public static void Lay(TownLayout layout, SimConfig config)
    {
        var was = (List<LayoutEdge>)[.. layout.Edges];
        var edgesAt = new List<int>[layout.NodeM.Count];
        for (var node = 0; node < edgesAt.Length; node++) edgesAt[node] = [];
        for (var edge = 0; edge < was.Count; edge++)
        {
            edgesAt[was[edge].From].Add(edge);
            edgesAt[was[edge].To].Add(edge);
        }

        var passed = new bool[edgesAt.Length];
        for (var node = 0; node < passed.Length; node++)
        {
            passed[node] = edgesAt[node].Count == 2
                           && OneRoad(layout, node, edgesAt[node][0], edgesAt[node][1]);
        }

        // Every run, walked before any of it is laid: the pieces it is made of and the places between them.
        var runs = new List<(List<int> Pieces, List<int> Nodes, int Start)>();
        var walked = new bool[was.Count];
        var inARun = new bool[was.Count];
        for (var edge = 0; edge < was.Count; edge++)
        {
            // An edge with a junction at neither end is inside a run and is reached by walking one; one
            // whose run is a closed circle of passed nodes is never reached at all, and stays the road it
            // was rather than becoming a road with no ends.
            if (walked[edge] || (passed[was[edge].From] && passed[was[edge].To])) continue;

            var start = passed[was[edge].From] ? was[edge].To : was[edge].From;
            var pieces = new List<int>();
            var nodes = new List<int>();
            Walk(was, edgesAt, passed, walked, start, edge, pieces, nodes);

            // <b>A run that comes back where it set off is shortened by one place, never given up on</b>: it
            // has no second end to be a road between. The last place it passes stays the junction it was,
            // and what is left is still joined.
            while (pieces.Count > 1 && nodes[^1] == start)
            {
                walked[pieces[^1]] = false;
                pieces.RemoveAt(pieces.Count - 1);
                nodes.RemoveAt(nodes.Count - 1);

                // Saying it is a junction again is what stops the piece just given back being walked
                // straight through it and back down the run, which is the same road laid twice.
                passed[nodes[^1]] = false;
            }

            if (pieces.Count == 1) continue;

            foreach (var piece in pieces) inARun[piece] = true;

            runs.Add((pieces, nodes, start));
        }

        if (runs.Count == 0) return;

        // <b>The town without a single one of the runs' pieces</b>, and then every run offered to it as the
        // one road it is — through the same gate every other road passed (<see cref="TownLayout.Join"/>), so a
        // joined road is held to the ground the roads that stayed hold and to the ground the runs joined
        // before it hold. Measured against the pieces instead, two joined roads could each clear the other's
        // pieces and still be laid into one another.
        var stays = new List<LayoutEdge>(was.Count);
        var staysLines = new List<ArcSeg[]>(was.Count);
        var nodeM = (List<Vector2>)[.. layout.NodeM];
        for (var edge = 0; edge < was.Count; edge++)
        {
            if (inARun[edge]) continue;

            stays.Add(was[edge]);
            staysLines.Add([.. layout.LineOf(edge)]);
        }

        layout.Rebuilt(nodeM, stays, staysLines);
        foreach (var (pieces, nodes, start) in runs) Offer(layout, was, pieces, nodes, start);

        // Every node a run passed through is left standing with nothing at it, and this is what drops them.
        layout.PruneTheDeadEnds();
    }

    /// <summary>
    /// <b>One run offered as the road it is</b>, and <b>cut in two where that road cannot be laid</b> (GEN-51):
    /// the place in the middle of what it passes stays the junction it was and each half is offered in its
    /// turn, so a corner that cannot be drawn costs that corner and never the whole run. A run of one piece is
    /// the road it always was.
    /// </summary>
    static void Offer(TownLayout layout, List<LayoutEdge> was, List<int> pieces, List<int> nodes, int start)
    {
        // A piece on its own is the road it always was, offered again exactly as it stood — and its line is a
        // function of the link, so it comes back the line it had.
        var road = pieces.Count == 1 ? was[pieces[0]] : Merged(was, layout, pieces, nodes, start);
        if (layout.Join(road.From, road.To, road.Class, road.Curvature, road.ThroughM, road.Straight) >= 0) return;

        if (pieces.Count == 1) return;

        // Each half keeps one node a piece — the far end of each of its own pieces — and the place between
        // them is the junction they now meet at.
        var at = pieces.Count / 2;
        Offer(layout, was, pieces.GetRange(0, at), nodes.GetRange(0, at), start);
        Offer(layout, was, pieces.GetRange(at, pieces.Count - at), nodes.GetRange(at, nodes.Count - at), nodes[at - 1]);
    }

    /// <summary>
    /// <b>Whether the two arms at a node are one road carrying on</b> rather than two roads meeting: the
    /// same traffic through, and a corner the road that comes of it can turn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A bridge and a ring piece are never joined to anything</b>, and their ends are not this rule's
    /// subject: a deck is one straight span between its own two bridgeheads (GEN-14a) and a ring is one
    /// junction laid out as a circle (GEN-19), so each end of each is a place the carriageway itself
    /// changes rather than a place the town's arithmetic stopped a line. It is the same reason
    /// <c>TownLayout.IsALeaf</c> will not prune a ring node.
    /// </para>
    /// <para>
    /// <b>Everything else is joined, whatever it is.</b> A street and an arterial come out as the arterial
    /// (<see cref="TownLayout.Precedence"/>, GEN-16), a carriageway being the width its ways make it
    /// whatever class it is (GEN-15) so that the class decides only how far the road may wander. A street
    /// the scatter took one way is joined on the same terms: the traffic through has to agree either way,
    /// and what keeps GEN-18 true is that a road which later comes apart runs both ways again
    /// (<see cref="TownLayout.Join"/>).
    /// </para>
    /// </remarks>
    static bool OneRoad(TownLayout layout, int node, int one, int other)
    {
        var a = layout.Edges[one];
        var b = layout.Edges[other];
        if (NeverJoined(a.Class) || NeverJoined(b.Class)) return false;

        // <b>The two arms carry the same traffic or they are not one road</b> (TER-4d): what arrives on one
        // is what leaves on the other, both ways round, and a node nothing passes through at all is a dead
        // end for the prune to find rather than a road to join (GEN-5a).
        var arrives = layout.Arrives(node, one);
        var leaves = layout.Leaves(node, one);
        return arrives == layout.Leaves(node, other)
               && leaves == layout.Arrives(node, other)
               && (arrives || leaves);
    }

    /// <summary>
    /// A class of road whose ends are not places the town's arithmetic happened to stop a line, so they are
    /// junctions however few arms they carry.
    /// </summary>
    static bool NeverJoined(RoadClass roadClass) => roadClass is RoadClass.Bridge or RoadClass.Roundabout;

    /// <summary>Which of two classes the road joined out of them is, which is the one the other gives way to.</summary>
    static RoadClass Keeps(RoadClass one, RoadClass other) =>
        TownLayout.Precedence(one) >= TownLayout.Precedence(other) ? one : other;

    /// <summary>
    /// <b>One run followed from a junction until it reaches another</b>: the roads it is made of, and where
    /// each of them arrives. A passed node carries exactly two arms, so the walk arrives on one and leaves
    /// on the other and can never reach the same node twice.
    /// </summary>
    static void Walk(
        List<LayoutEdge> was, List<int>[] edgesAt, bool[] passed, bool[] walked, int start, int edge,
        List<int> run, List<int> nodes)
    {
        var at = start;
        var along = edge;
        while (true)
        {
            run.Add(along);
            walked[along] = true;

            var road = was[along];
            var far = road.From == at ? road.To : road.From;
            nodes.Add(far);
            if (!passed[far]) return;

            along = edgesAt[far][0] == along ? edgesAt[far][1] : edgesAt[far][0];
            at = far;
        }
    }

    /// <summary>
    /// <b>The one road a run is</b>: its two junctions, the class and the traffic its pieces share, and the
    /// places it passes — each piece's own, read the way the run goes, and the nodes between them.
    /// <b>It asks the layout for no bend of its own</b>: what a joined road does between its ends is the
    /// points it goes through, and a curvature beside them would be a second answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every road runs both ways here</b>, so there is no traffic to carry over: which streets run one way
    /// is settled on the layout that stands, after this (<see cref="OneWayStreets"/>, GEN-18).
    /// </para>
    /// <para>
    /// <b>It is laid the way most of its length was</b> (<see cref="LayoutEdge.Straight"/>), each piece
    /// weighed by the chord between its own two nodes. Straight only where every piece was, a run the length
    /// of a district's edge came out wandering however straight its district, the chance of it being the
    /// share raised to the number of its pieces.
    /// </para>
    /// </remarks>
    static LayoutEdge Merged(List<LayoutEdge> was, TownLayout layout, List<int> run, List<int> nodes, int start)
    {
        var throughM = new List<Vector2>();
        var roadClass = was[run[0]].Class;
        var straightM = 0f;
        var wanderingM = 0f;
        var at = start;
        for (var piece = 0; piece < run.Count; piece++)
        {
            var road = was[run[piece]];
            roadClass = Keeps(roadClass, road.Class);

            var chordM = Vector2.Distance(layout.NodeM[road.From], layout.NodeM[road.To]);
            if (road.Straight) straightM += chordM;
            else wanderingM += chordM;

            if (road.From == at)
            {
                throughM.AddRange(road.ThroughM);
            }
            else
            {
                for (var point = road.ThroughM.Length - 1; point >= 0; point--)
                {
                    throughM.Add(road.ThroughM[point]);
                }
            }

            if (piece + 1 < run.Count) throughM.Add(layout.NodeM[nodes[piece]]);
            at = nodes[piece];
        }

        return new LayoutEdge(
            start, nodes[^1], roadClass, 0f, RoadFlow.BothWays, [.. throughM], straightM > wanderingM);
    }
}
