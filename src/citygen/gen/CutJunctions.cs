using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>One arm a cut junction is to carry besides the road it was cut into</b> (GEN-52), as the turn off that
/// road's own bearing at the node and how far the arm's far stand line is from it.
/// </summary>
/// <param name="TurnRad">
/// Which way the arm runs, measured off the tangent of the road at the node — so an arm square to the
/// street is a quarter turn whichever way the street happens to run.
/// </param>
/// <param name="LeadM">
/// How far out along that bearing the arm's own road begins — <b>the standoff at that end of the turn</b>,
/// as the cut's own standoff is the one at the street's end of it. The ordinary one is
/// <see cref="CityGenFigures.ConnectionStandoffM"/>, which is what a cut whose arms are streets asks for; a
/// car park asks for whatever the turn into that bay spends (<see cref="SimConfig.CarParkBayLeadM"/>), which
/// is the arm's own because the lane it is turned off may run either side of the node (TER-4d, GEN-53).
/// </param>
/// <param name="StandM">
/// How far out along that bearing the arm's far connection points stand, measured from the arm's own foot.
/// The node at the end of it stands a lead further out again, that node's own arm being a lead like any
/// other (GEN-46).
/// </param>
/// <param name="Square">
/// Whether the arm is held to stand square to the others (GEN-13). <b>An arm that is not is one whose ground
/// is an apron rather than a second carriageway</b> — a rank of bays off one node is one piece of tarmac, and
/// nothing is filleted, crossed or barred between two of them, which is the whole of what GEN-13 is about.
/// </param>
/// <param name="AsideM">
/// How far along the road's own tangent the arm's foot stands off the node, so a rank of arms can be laid
/// <em>parallel</em> rather than fanned out of one point: every arm of a rank carries one bearing and the
/// bay each reaches is a step further along the street. Nought puts the foot on the node itself, which is
/// where an ordinary arm's is.
/// </param>
internal readonly record struct CutArm(
    float TurnRad, float LeadM, float StandM, RoadClass Class, bool Square = true, float AsideM = 0f);

/// <summary>What a cut left behind: the junction, the roads whose arms are now read off their lines, and the arms.</summary>
/// <param name="Junction">The node the cut put in the road.</param>
/// <param name="Roads">
/// Every road the cut made a <em>cut</em> road (GEN-52) — the two pieces of the road that was parted and
/// every arm laid off them — which is what the plan carries so the lanes can be derived off it again.
/// </param>
/// <param name="Arms">The arm roads, in the order they were asked for.</param>
internal readonly record struct Cut(int Junction, int[] Roads, int[] Arms);

/// <summary>
/// <b>A junction let into a road that already stands</b> (GEN-52): the road is parted at a place along its
/// own line into the stretch before the junction and the stretch after it, the node stands between them, and
/// whatever arms the caller asked for are laid off it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The road does not move</b>, which is the whole of what this is for. The two pieces are the ground the
/// road was already laid on — the same arcs, cut — so every lane offset from them lies where it lay, and the
/// movement the new junction draws between them is a biarc between two poses of one arc, which is that arc.
/// Nothing here is drawn again: a piece redrawn would be a road laid to two bearings that were never asked
/// for, and the carriageway would step sideways at the very place a reader is looking.
/// </para>
/// <para>
/// <b>It inverts the inversion, and it says so.</b> Everywhere else the arms come first and the road is laid
/// to them (<see cref="ConnectionPoints"/>, GEN-46); here the road came first and the arms are read off it
/// (<see cref="CityPlan.RoadArrays.Cut"/>). That is why the cut is exact rather than near: the node is placed
/// a standoff of the road's <em>own</em> arc back from each parting, so the lead an arm reads is a piece of
/// the line and not a curve fitted through it.
/// </para>
/// <para>
/// <b>A cut is asked for inside one arc, with a standoff of that arc either side of it</b>
/// (<see cref="SitesOn"/>). A lead is one arc (<see cref="ConnectionPoints.Arm.Lead"/>), so a node placed
/// across a joint in the road would be one whose arms stand somewhere the road does not go.
/// </para>
/// <para>
/// <b>An arm need not leave from the node.</b> One may stand its foot a way along the street
/// (<see cref="CutArm.AsideM"/>), which is what lets a rank of arms run parallel to each other rather than
/// fan out of a point. What joins such an arm to the road is the movement the junction draws between two
/// lane ends, exactly as for any other turn (<see cref="LaneLines"/>), and the bearing it is read back off
/// is the one its own line carries (<see cref="ConnectionPoints"/>).
/// </para>
/// <para>
/// <b>Nothing is laid and taken back</b> (GEN-10). Every piece and every arm is drawn and asked about before
/// any of it is installed, and a cut that fails any of it leaves the road exactly as it was.
/// </para>
/// <para>
/// <b>It is the last thing done to a layout.</b> The bearings <see cref="TownLayout.Rebuilt"/> refills are
/// read off each road's chord, which is not where a cut road leaves its nodes — so nothing may be offered to
/// a layout that has been cut, and the generator runs this after every stage that offers anything.
/// </para>
/// </remarks>
internal static class CutJunctions
{
    /// <summary>One place a junction could be cut into a road: which road, how far along its line, and where that is.</summary>
    internal readonly record struct Site(int Road, float AlongM, Vector2 AtM);

    /// <summary>
    /// <b>The places a junction could be cut into one road</b>, one per arc of it that can carry one: the
    /// middle of the stretch of that arc which is far enough from both of the road's own junctions
    /// (<c>CityGenFigures.LocalityM</c>, GEN-16), <paramref name="standoffM"/> clear of both ends of the arc,
    /// and bending no tighter than <paramref name="curvatureMax"/>.
    /// </summary>
    /// <param name="standoffM">
    /// How far back of the node the cut parts the road, which is how far its two pieces' lane ends stand off
    /// the junction. <b>The caller's and not the town's</b>: a junction whose arms are spread along the
    /// street stands off further than one whose arms leave from the node (GEN-53).
    /// </param>
    /// <remarks>
    /// <b>A road that runs one way is offered like any other</b> (GEN-18, TER-4d): the two pieces a cut makes
    /// of one are that street parted rather than two streets meeting, on the terms a roundabout's arcs are
    /// one carriageway. What the street's one direction costs is the bays' own — each is reached the one way
    /// the street runs, which is every way it has (GEN-53).
    /// </remarks>
    public static void SitesOn(
        TownLayout layout, SimConfig config, int road, float standoffM, float curvatureMax, List<Site> into)
    {
        var edge = layout.Edges[road];
        if (edge.Class is RoadClass.Bridge or RoadClass.Roundabout or RoadClass.CarPark) return;

        var line = layout.LineOf(road);
        if (line.Length == 0) return;

        var leadM = config.CityGen.ConnectionStandoffM;
        var localityM = config.CityGen.LocalityM;
        var lengthM = Spline.TotalLengthM(line);

        // A node's distance from the junction behind it is the lead that junction's own arm stands off plus
        // the line between them, so what the locality costs the line is a locality less one lead at each end.
        var fromM = MathF.Max(standoffM, localityM - leadM);
        var toM = lengthM - fromM;

        var arcM = 0f;
        foreach (var arc in line)
        {
            var startM = arcM;
            arcM += arc.LengthM;
            if (MathF.Abs(arc.Curvature) > curvatureMax) continue;

            var earliestM = MathF.Max(fromM, startM + standoffM);
            var latestM = MathF.Min(toM, arcM - standoffM);
            if (latestM <= earliestM) continue;

            var alongM = (earliestM + latestM) * 0.5f;
            into.Add(new Site(road, alongM, Spline.SampleAt(line, alongM).PositionM));
        }
    }

    /// <summary>
    /// <b>Every place in the town a junction could be cut into a road</b>, over every road there is and with
    /// the ones standing inside a locality of a junction already there left out (GEN-16).
    /// </summary>
    /// <inheritdoc cref="SitesOn" path="/param[@name='standoffM']"/>
    public static List<Site> Sites(TownLayout layout, SimConfig config, float standoffM, float curvatureMax)
    {
        var onARoad = new List<Site>();
        var sites = new List<Site>();
        for (var road = 0; road < layout.Edges.Count; road++)
        {
            onARoad.Clear();
            SitesOn(layout, config, road, standoffM, curvatureMax, onARoad);
            foreach (var site in onARoad)
            {
                if (ClearOfEveryJunction(layout, config, site.AtM)) sites.Add(site);
            }
        }

        return sites;
    }

    /// <summary>
    /// Whether a node here would stand a locality clear of every node the town already has (GEN-16) — asked
    /// of all of them and not of the two the road runs between, a road being free to bow past a third.
    /// </summary>
    static bool ClearOfEveryJunction(TownLayout layout, SimConfig config, Vector2 atM)
    {
        var localityM = config.CityGen.LocalityM;
        for (var node = 0; node < layout.NodeM.Count; node++)
        {
            if (Vector2.DistanceSquared(layout.NodeM[node], atM) < localityM * localityM) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>One junction cut into one road</b>, with the arms asked for laid off it — or nothing, where any
    /// piece of it could not be laid.
    /// </summary>
    /// <param name="alongM">How far along the road's own line the node stands (<see cref="SitesOn"/>).</param>
    /// <inheritdoc cref="SitesOn" path="/param[@name='standoffM']"/>
    public static Cut? Into(
        TownLayout layout, SimConfig config, int road, float alongM, float standoffM,
        ReadOnlySpan<CutArm> arms)
    {
        var line = layout.LineOf(road);
        var lengthM = Spline.TotalLengthM(line);
        if (line.Length == 0 || alongM <= standoffM || alongM >= lengthM - standoffM) return null;

        var at = Spline.SampleAt(line, alongM);
        if (!layout.Dry(at.PositionM) || !StandSquareEnough(layout, arms)) return null;
        if (!ClearOfEveryJunction(layout, config, at.PositionM)) return null;

        var pieces = new ArcSeg[line.Length + 1];
        var before = Piece(line, 0f, alongM - standoffM, pieces);
        var after = Piece(line, alongM + standoffM, lengthM, pieces);
        if (before.Length == 0 || after.Length == 0) return null;

        var was = layout.Edges[road];
        var node = layout.NodeM.Count;
        var (beforeThroughM, afterThroughM) = Passes(line, was.ThroughM, alongM);
        var beforeEdge = was with { To = node, ThroughM = beforeThroughM };
        var afterEdge = was with { From = node, ThroughM = afterThroughM };

        // <b>Everything is drawn and asked about before any of it is installed</b> (GEN-10). The two pieces
        // stand on the ground their own road stood on, so that road is the one thing any of this may share
        // ground with (GEN-49).
        ReadOnlySpan<int> instead = [road];
        if (!layout.Clear(before, beforeEdge, instead) || !layout.Clear(after, afterEdge, instead)) return null;

        var armM = new Vector2[arms.Length];
        var armLine = new ArcSeg[arms.Length][];
        for (var arm = 0; arm < arms.Length; arm++)
        {
            var leadM = arms[arm].LeadM;
            if (arms[arm].StandM <= leadM) return null;

            var unit = Heading.Unit(at.HeadingRad + arms[arm].TurnRad);
            var footM = at.PositionM + (Heading.Unit(at.HeadingRad) * arms[arm].AsideM);
            armM[arm] = footM + (unit * (arms[arm].StandM + leadM));
            armLine[arm] = [Straight(footM, unit, leadM, arms[arm].StandM)];

            // <b>Asked of the arm and of the lead at the end of it</b>: the node an arm ends at stands a
            // lead past the line, so the ground an arm takes reaches further than the road it lays.
            var reach = Straight(footM, unit, leadM, arms[arm].StandM + leadM);
            // <b>The node at the end of an arm owes the town a locality like any other</b> (GEN-16) — and
            // owes the cut's own nodes nothing, they being one junction laid out with a stub rather than two
            // spacings that landed on the same ground, exactly as a ring's nodes are (GEN-19).
            var edge = new LayoutEdge(node, node + 1 + arm, arms[arm].Class, 0f, RoadFlow.BothWays, []);
            if (!layout.Dry(armM[arm]) || !ClearOfEveryJunction(layout, config, armM[arm])) return null;
            if (!layout.Clear([reach], edge, instead)) return null;
        }

        var nodeM = (List<Vector2>)[.. layout.NodeM, at.PositionM, .. armM];
        var edges = (List<LayoutEdge>)[.. layout.Edges];
        var lineOf = new List<ArcSeg[]>(edges.Count + arms.Length + 1);
        for (var edge = 0; edge < edges.Count; edge++) lineOf.Add([.. layout.LineOf(edge)]);

        edges[road] = beforeEdge;
        lineOf[road] = before;

        var cut = new List<int> { road, edges.Count };
        edges.Add(afterEdge);
        lineOf.Add(after);

        var laid = new int[arms.Length];
        for (var arm = 0; arm < arms.Length; arm++)
        {
            laid[arm] = edges.Count;
            cut.Add(edges.Count);
            edges.Add(new LayoutEdge(node, node + 1 + arm, arms[arm].Class, 0f, RoadFlow.BothWays, []));
            lineOf.Add(armLine[arm]);
        }

        layout.Rebuilt(nodeM, edges, lineOf);
        return new Cut(node, [.. cut], laid);
    }

    /// <summary>
    /// Whether the arms asked for stand far enough off each other and off the road they are cut into
    /// (GEN-13). <b>The road's two arms leave the node exactly opposite</b> — they are one line parted — so
    /// what is asked is the turn each new arm makes off it.
    /// </summary>
    static bool StandSquareEnough(TownLayout layout, ReadOnlySpan<CutArm> arms)
    {
        var apartRad = layout.ArmsApartMinRad;
        for (var arm = 0; arm < arms.Length; arm++)
        {
            if (!arms[arm].Square) continue;

            if (Apart(arms[arm].TurnRad, 0f) < apartRad || Apart(arms[arm].TurnRad, MathF.PI) < apartRad)
            {
                return false;
            }

            for (var other = arm + 1; other < arms.Length; other++)
            {
                if (arms[other].Square && Apart(arms[arm].TurnRad, arms[other].TurnRad) < apartRad) return false;
            }
        }

        return true;
    }

    static float Apart(float oneRad, float otherRad) =>
        MathF.Abs(MathF.IEEERemainder(oneRad - otherRad, MathF.Tau));

    /// <summary>One stretch of the road's own line, as the chain it is in its own right.</summary>
    static ArcSeg[] Piece(ReadOnlySpan<ArcSeg> line, float fromM, float toM, Span<ArcSeg> into) =>
        [.. into[..Spline.SubChainInto(line, fromM, toM, into)]];

    /// <summary>The straight one arm is laid as, from its stand point by the junction out to its own far one.</summary>
    static ArcSeg Straight(Vector2 footM, Vector2 unit, float fromM, float toM) =>
        new(footM + (unit * fromM), MathF.Atan2(unit.Y, unit.X), toM - fromM, 0f);

    /// <summary>
    /// The places the road passed, split between the two pieces by where each of them stands along the line
    /// (GEN-51). <b>A cut lands inside one arc and a place passed is a joint between two</b>, so nothing here
    /// is ever on the parting itself.
    /// </summary>
    static (Vector2[] Before, Vector2[] After) Passes(
        ReadOnlySpan<ArcSeg> line, Vector2[] throughM, float alongM)
    {
        if (throughM.Length == 0) return ([], []);

        var lengthM = Spline.TotalLengthM(line);
        var before = new List<Vector2>();
        var after = new List<Vector2>();
        foreach (var pointM in throughM)
        {
            var atM = Spline.ProjectM(line, pointM, lengthM * 0.5f, lengthM);
            (atM < alongM ? before : after).Add(pointM);
        }

        return ([.. before], [.. after]);
    }
}
