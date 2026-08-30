using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Terrain;
using Xunit;
using Xunit.Abstractions;

namespace TrafficSimulation.Tests.Routing;

/// <summary>
/// The walking network asked of every shipped map that has a pavement, and asked the same things the
/// driving one is: the two are deliberately one shape, so a fault in the contraction that only shows on
/// one of them is a fault in how that side feeds it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class WalkingNetworkTests(ITestOutputHelper output)
{
    public static TheoryData<string> Maps => Towns.EveryMapWithAFootway();

    /// <summary>One map's pavement and the network contracted over it, built once and read by every claim.</summary>
    static (FootGraph Foot, WalkingNetwork Network) Of(string map) => Built.GetOrAdd(map, at =>
    {
        var plan = Towns.Of(at);
        var config = SimConfig.Shipped();
        var foot = FootGraph.Build(plan, config);
        return (foot, WalkingNetwork.Build(foot, new TerrainGrid(plan, config), config));
    });

    static readonly ConcurrentDictionary<string, (FootGraph Foot, WalkingNetwork Network)> Built = new();

    /// <summary>Every stretch of pavement still belongs to some run, exactly once, in one place along it.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryStretchBelongsToExactlyOneRun(string map)
    {
        var (foot, network) = Of(map);
        var runs = network.Runs;
        var timesWalked = new int[foot.EdgeCount];

        for (var link = 0; link < runs.LinkCount; link++)
        {
            foreach (var edge in runs.PiecesOf(link)) timesWalked[edge]++;
        }

        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            Assert.True(timesWalked[edge] == 1, $"{map}: stretch {edge} is in {timesWalked[edge]} runs, not one");
            Assert.Equal(edge, runs.PiecesOf(network.LinkOfEdge(edge))[network.SlotOfEdge(edge)]);
        }
    }

    /// <summary>
    /// <b>A node is a place a walker can go more than one way, and nothing else is a node.</b> Asked both
    /// ways round: a bend on the network is a decision nobody makes, and a split off it is a way no route
    /// could ever plan.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ANetworkNodeIsExactlyAPlaceTheFootwaySplits(string map)
    {
        var (foot, network) = Of(map);
        var runs = network.Runs;
        var onNetwork = new bool[foot.NodeCount];

        for (var node = 0; node < runs.Graph.NodeCount; node++)
        {
            var fine = runs.FineNodeOf(node);
            onNetwork[fine] = true;
            Assert.True(
                foot.EdgesOut(fine).Length != 2,
                $"{map}: fine node {fine} at {foot.AnchorM(fine)} carries a walk straight through and is on the network");
        }

        for (var fine = 0; fine < foot.NodeCount; fine++)
        {
            var ways = foot.EdgesOut(fine).Length;
            if (ways is 0 or 2) continue;

            Assert.True(
                onNetwork[fine],
                $"{map}: fine node {fine} at {foot.AnchorM(fine)} has {ways} ways on and is off the network");
        }
    }

    /// <summary>
    /// A run's stations agree with its own weight: monotone, starting at zero, ending at the run's own
    /// length, and <b>never priced below the span between its two anchors</b>, which is what makes the
    /// search's bound admissible.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ARunsStationsAgreeWithItsOwnWeight(string map)
    {
        var (foot, network) = Of(map);
        var runs = network.Runs;

        for (var link = 0; link < runs.LinkCount; link++)
        {
            var edges = runs.PiecesOf(link);
            var stations = runs.StationsOf(link);

            Assert.False(edges.IsEmpty, $"{map}: run {link} is made of no edges");
            Assert.Equal(0f, stations[0], 4);
            for (var slot = 1; slot < edges.Length; slot++)
            {
                Assert.True(stations[slot] > stations[slot - 1], $"{map}: run {link} steps backwards at piece {slot}");
                Assert.Equal(stations[slot - 1] + foot.LengthM(edges[slot - 1]), stations[slot], 3);
            }

            Assert.Equal(stations[^1] + foot.LengthM(edges[^1]), runs.LengthM(link), 3);

            var spanM = (runs.Graph.EndAnchorM(link) - runs.Graph.StartAnchorM(link)).Length();
            Assert.True(
                runs.Graph.WeightM(link) >= spanM - 1e-3f,
                $"{map}: run {link} is priced at {runs.Graph.WeightM(link):F2} m over a {spanM:F2} m span");
        }
    }

    /// <summary>The pieces of a run are walked in the order they are laid: each ends where the next begins.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ARunsPiecesAreWalkedEndToEnd(string map)
    {
        var (foot, network) = Of(map);
        var runs = network.Runs;

        for (var link = 0; link < runs.LinkCount; link++)
        {
            var edges = runs.PiecesOf(link);
            for (var slot = 1; slot < edges.Length; slot++)
            {
                Assert.Equal(foot.ToNode(edges[slot - 1]), foot.FromNode(edges[slot]));
            }

            Assert.Equal(runs.FineNodeOf(runs.Graph.FromNode(link)), foot.FromNode(edges[0]));
            Assert.Equal(runs.FineNodeOf(runs.Graph.ToNode(link)), foot.ToNode(edges[^1]));
        }
    }

    /// <summary>
    /// <b>Both ends of a walk are plural.</b> A body standing on a stretch is offered both ways along it
    /// and a destination on one is offered on both links that cover it — and the two are the same stretch
    /// walked the other way, which is what makes them comparable in one search.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AWalkerStandingOnAStretchIsOfferedBothWaysAlongIt(string map)
    {
        var (foot, network) = Of(map);
        Span<RouteEntry> entries = stackalloc RouteEntry[2];
        Span<RouteGoal> goals = stackalloc RouteGoal[2];

        var asked = 0;
        for (var edge = 0; edge < foot.EdgeCount; edge += 2 + 2 * (foot.EdgeCount / 200))
        {
            var lengthM = foot.LengthM(edge);
            var pointM = Spline.SampleAt(foot.ArcsOf(edge), lengthM * 0.5f).PositionM;

            Assert.Equal(2, network.EntriesNear(pointM, entries));
            Assert.Equal(2, network.GoalsAt(pointM, goals));
            Assert.NotEqual(entries[0].Link, entries[1].Link);
            Assert.Equal(entries[0].Link, goals[0].Link);
            Assert.Equal(entries[1].Link, goals[1].Link);

            foreach (var entry in entries)
            {
                Assert.InRange(entry.AlongM, 0f, network.Runs.LengthM(entry.Link) + 1e-3f);
                Assert.Equal(network.Runs.LengthM(entry.Link) - entry.AlongM, entry.RemainingM, 3);
            }

            asked++;
        }

        Assert.True(asked > 0, $"{map}: not one stretch was asked about");
    }

    /// <summary>Where a place on a stretch stands in its own run, read back through the run's own bisection.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void APlaceOnAStretchIsFoundAgainInItsOwnRun(string map)
    {
        var (foot, network) = Of(map);
        var runs = network.Runs;

        for (var edge = 0; edge < foot.EdgeCount; edge += 1 + foot.EdgeCount / 200)
        {
            var alongEdgeM = foot.LengthM(edge) * 0.5f;
            var link = network.LinkOfEdge(edge);
            var slot = runs.PieceAt(link, network.PlaceOfM(edge, alongEdgeM), out var backM);

            Assert.True(
                network.SlotOfEdge(edge) == slot && MathF.Abs(alongEdgeM - backM) < 1e-3f,
                $"{map}: {alongEdgeM:F4} m into stretch {edge} (slot {network.SlotOfEdge(edge)} of run {link}, " +
                $"{runs.LengthM(link):F2} m long) reads back as {backM:F4} m into slot {slot}");
        }
    }

    /// <summary>
    /// <b>The offset is one figure for the whole stretch and the ground cuts it back, never the plan.</b>
    /// What is asserted is the relation — a body walking either lane of the stretch stands clear on both
    /// hands at every station — and the share of stretches keeping the full quarter is printed rather than
    /// asserted, because it is a fact about the towns and not about the construction.
    /// </summary>
    /// <remarks>
    /// The shoulder is asked for <b>half a cell inside where it actually stands</b>, which is TER-7's own
    /// tolerance: the classifier agrees with the plan's shapes to within half a cell, and the shipped
    /// pavement leaves a lane's shoulder exactly that much spare — so a construction sampling the line at
    /// one spacing and a check resampling it at another disagree by a centimetre wherever a shoulder runs
    /// down a kerb, and every one of those is a cell boundary rather than a lane in the road. What the
    /// tolerance costs is stated instead of hidden: the worst overhang any shoulder actually has is
    /// printed, and a lane laid into the carriageway would be half a band out rather than a centimetre.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ALaneIsOffsetAsFarAsTheGroundAllowsAndNoFurther(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var terrain = new TerrainGrid(plan, config);
        var (foot, network) = Of(map);
        var bodyM = config.PersonDiameterM * 0.5f;

        var toleranceM = terrain.CellSizeM * 0.5f;
        var kept = 0;
        var flattened = 0;
        var totalM = 0f;
        var worstOverhangM = 0f;
        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            var offsetM = network.LaneOffsetM(edge);
            Assert.InRange(offsetM, 0f, config.WalkingLaneOffsetM);
            Assert.Equal(offsetM, network.LaneOffsetM(foot.Reverse(edge)));

            if (offsetM >= config.WalkingLaneOffsetM - 1e-4f) kept++;
            if (offsetM <= 0f) flattened++;
            totalM += offsetM;

            if (offsetM <= 0f) continue;

            var lengthM = foot.LengthM(edge);
            var stations = Math.Max(1, (int)MathF.Ceiling(lengthM / terrain.CellSizeM));
            for (var station = 0; station <= stations; station++)
            {
                var at = Spline.SampleAt(foot.ArcsOf(edge), lengthM * station / stations);
                foreach (var shoulderM in (ReadOnlySpan<float>)
                         [offsetM - bodyM, offsetM + bodyM, -offsetM - bodyM, -offsetM + bodyM])
                {
                    var standingM = at.PositionM + at.Right * shoulderM;
                    if (terrain.At(standingM).Walkable) continue;

                    var pulledInM = at.PositionM + at.Right * (shoulderM - MathF.CopySign(toleranceM, shoulderM));
                    Assert.True(
                        terrain.At(pulledInM).Walkable,
                        $"{map}: stretch {edge} keeps {offsetM:F2} m but a body at {standingM} stands on " +
                        $"{terrain.GroundAt(standingM)}, and so does it half a cell in");

                    worstOverhangM = MathF.Max(worstOverhangM, Overhang(terrain, at, shoulderM, toleranceM));
                }
            }
        }

        var stretches = foot.EdgeCount / 2;
        output.WriteLine(
            $"{map}: {stretches} stretches, {kept} keep the full {config.WalkingLaneOffsetM:F2} m " +
            $"({(stretches == 0 ? 0f : 100f * kept / stretches):F0} %), {flattened} have no room for a lane, " +
            $"mean kept {(stretches == 0 ? 0f : totalM / stretches):F2} m, worst shoulder overhang " +
            $"{worstOverhangM:F3} m");
    }

    /// <summary>How far past the last walkable ground a shoulder actually stands, so the tolerance above is a figure and not a shrug.</summary>
    static float Overhang(TerrainGrid terrain, SplineSample at, float shoulderM, float toleranceM)
    {
        var inwardM = MathF.CopySign(0.01f, -shoulderM);
        for (var backM = 0f; backM <= toleranceM; backM += MathF.Abs(inwardM))
        {
            if (terrain.At(at.PositionM + at.Right * (shoulderM + MathF.CopySign(backM, inwardM))).Walkable) return backM;
        }

        return toleranceM;
    }

    /// <summary>
    /// <b>Every corner of the network is one line and not two that cross.</b> Both stretches end at the
    /// node they share, but each is laid a quarter of its own band to its own right, so a lane's end
    /// stands a lane offset off the next one's start — and where a crossing meets a pavement that is two
    /// right angles' worth, which is a walk that steps sideways into the road. The mitre is what closes
    /// it, and this asserts that it does, at both of its own ends.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryCornerIsMitredIntoOneLine(string map)
    {
        var (foot, network) = Of(map);
        var lane = new ArcSeg[MostArcs(foot)];
        var next = new ArcSeg[lane.Length];

        var corners = 0;
        var straightOn = 0;
        var totalM = 0f;
        var longestM = 0f;
        var worstM = 0f;
        var longest = string.Empty;
        var tightestM = float.PositiveInfinity;
        var tightest = string.Empty;

        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            var arriving = network.LaneOf(edge);
            if (arriving.Length == 0) continue;

            var arrivingM = Spline.TotalLengthM(arriving);
            var turns = network.TurnsFrom(edge);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                var onward = network.LaneOf(turns[turn]);
                if (onward.Length == 0) continue;

                var slot = network.TurnSlotAt(edge, turn);
                var joins = Spline.SampleAt(onward, network.JoinToM(slot)).PositionM;
                if (network.TailOf(edge) != WalkingNetwork.NoTurn)
                {
                    // A lane that carries its corner is the corner: what has to meet the next lane is the
                    // end of the line itself, and its other ways are laid off an end it no longer has.
                    if (slot != network.TailOf(edge)) continue;

                    corners++;
                    var carriedM = (Spline.SampleAt(arriving, arrivingM).PositionM - joins).Length();
                    worstM = MathF.Max(worstM, carriedM);
                    Assert.True(
                        carriedM < ToleranceM,
                        $"{map}: stretch {edge} carries its corner into {turns[turn]} and ends {carriedM:F2} m " +
                        "from where that lane is walked from");
                    continue;
                }

                var join = network.JoinArcs(slot);
                var leaves = Spline.SampleAt(arriving, arrivingM - network.JoinFromM(slot)).PositionM;

                corners++;
                if (join.Length == 0)
                {
                    // Nothing to lay: the stretch arrives at the very point the next one sets off from,
                    // which is what a split in a straight run looks like.
                    straightOn++;
                    var throughM = (joins - leaves).Length();
                    worstM = MathF.Max(worstM, throughM);
                    Assert.True(
                        throughM < ToleranceM,
                        $"{map}: stretch {edge} into {turns[turn]} has no mitre and its two lanes stand {throughM:F2} m apart");
                    continue;
                }

                var openM = (join[0].StartM - leaves).Length();
                var shutM = (Spline.SampleAt(join, network.JoinLengthM(slot)).PositionM - joins).Length();
                worstM = MathF.Max(worstM, MathF.Max(openM, shutM));
                Assert.True(
                    openM < ToleranceM && shutM < ToleranceM,
                    $"{map}: the mitre from {foot.KindOf(edge)} {edge} into {foot.KindOf(turns[turn])} " +
                    $"{turns[turn]} stands {openM:F2} m off the lane it leaves and {shutM:F2} m off the one it joins");

                foreach (var arc in join)
                {
                    if (MathF.Abs(arc.Curvature) < 1e-6f) continue;

                    var radiusM = 1f / MathF.Abs(arc.Curvature);
                    if (radiusM >= tightestM) continue;

                    tightestM = radiusM;
                    tightest = $"{foot.KindOf(edge)} {edge} into {foot.KindOf(turns[turn])} {turns[turn]}";
                }

                // A corner nothing can walk: a body aiming at the far side of it turns as hard as it can
                // and goes round rather than across, and the clock that decides a walk has been given up
                // then runs out on it.
                Assert.False(
                    Tighter(join),
                    $"{map}: the mitre from {foot.KindOf(edge)} {edge} into {foot.KindOf(turns[turn])} " +
                    $"{turns[turn]} bends tighter than the {TightestFeetM:F2} m the feet hold");

                totalM += network.JoinLengthM(slot);
                if (network.JoinLengthM(slot) > longestM)
                {
                    longestM = network.JoinLengthM(slot);
                    longest = $"{foot.KindOf(edge)} {edge} into {foot.KindOf(turns[turn])} {turns[turn]} " +
                              $"at node {foot.ToNode(edge)}, {(joins - leaves).Length():F2} m apart, " +
                              $"turning {MathF.Abs(Spline.WrapRad(Spline.SampleAt(onward, 0f).HeadingRad - Spline.SampleAt(arriving, arrivingM).HeadingRad)) * 180f / MathF.PI:F0}°";
                }
            }
        }

        var mitred = corners - straightOn;
        output.WriteLine(
            $"{map}: {corners} corners, {straightOn} run straight on through their node, {mitred} are mitred — " +
            $"mean {(mitred == 0 ? 0f : totalM / mitred):F2} m long, longest {longestM:F2} m ({longest}); " +
            $"tightest arc {tightestM:F2} m ({tightest}) against the {TightestFeetM:F2} m the feet hold; " +
            $"worst end stands {worstM:F3} m off its lane");
    }

    /// <summary>
    /// <b>A stretch hands over at a place, not at a place per way off it.</b> Every corner leaving one
    /// leaves it at the same point, and every corner arriving on one lands at the same point — the walking
    /// side of the rule the road side keeps for a lane end (`TER-5d`).
    /// </summary>
    /// <remarks>
    /// <b>And a crossing gives up nothing at either end</b>: a walk over a road is the shortest line across
    /// it and starts at the kerb, because ground given up on the paint is a body turning in the
    /// carriageway. The corner onto it is taken on the pavement, which has room for it and nothing coming.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryCornerThroughAStretchEndUsesTheSamePoint(string map)
    {
        var (foot, network) = Of(map);
        var leavesAtM = new float[foot.EdgeCount];
        var landsAtM = new float[foot.EdgeCount];
        Array.Fill(leavesAtM, float.NaN);
        Array.Fill(landsAtM, float.NaN);

        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            var turns = network.TurnsFrom(edge);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                var slot = network.TurnSlotAt(edge, turn);
                if (network.JoinLengthM(slot) <= 0f) continue;

                var onto = turns[turn];
                Assert.True(
                    float.IsNaN(leavesAtM[edge]) || MathF.Abs(leavesAtM[edge] - network.JoinFromM(slot)) < ToleranceM,
                    $"{map}: stretch {edge} hands over {leavesAtM[edge]:F2} m before its end to one way off it and "
                    + $"{network.JoinFromM(slot):F2} m before it to {onto}");
                Assert.True(
                    float.IsNaN(landsAtM[onto]) || MathF.Abs(landsAtM[onto] - network.JoinToM(slot)) < ToleranceM,
                    $"{map}: stretch {onto} is landed on {landsAtM[onto]:F2} m along by one corner and "
                    + $"{network.JoinToM(slot):F2} m along by the one from {edge}");

                leavesAtM[edge] = network.JoinFromM(slot);
                landsAtM[onto] = network.JoinToM(slot);

                if (foot.KindOf(edge) == FootEdgeKind.Crossing)
                {
                    AtTheKerb(map, foot, network, edge, onto, network.JoinFromM(slot));
                }
                else if (foot.KindOf(onto) == FootEdgeKind.Crossing)
                {
                    // And the pavement's own half of that box: it stops at the zebra's mouth, which is half
                    // the crossing's band back from the node the two of them share.
                    AtTheMouth(map, foot, network, edge, onto, network.JoinFromM(slot));
                }

                if (foot.KindOf(onto) == FootEdgeKind.Crossing)
                {
                    AtTheKerb(map, foot, network, onto, edge, network.JoinToM(slot));
                }
                else if (foot.KindOf(edge) == FootEdgeKind.Crossing)
                {
                    AtTheMouth(map, foot, network, onto, edge, network.JoinToM(slot));
                }
            }
        }
    }

    /// <summary>
    /// <b>A lane carries only the corner nothing chooses at</b>: the node it arrives at offers one way on,
    /// and neither end of that corner is paint. Carried where a walk could go two ways it would be one of
    /// them silently folded into the stretch; carried onto a crossing it would put a body on the zebra
    /// before it had asked the road about it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ALaneCarriesOnlyTheCornerNothingChoosesAt(string map)
    {
        var (foot, network) = Of(map);

        var carried = 0;
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            var tail = network.TailOf(edge);
            if (tail == WalkingNetwork.NoTurn) continue;

            carried++;
            var turns = network.TurnsFrom(edge);
            var ways = 0;
            var onto = -1;
            for (var turn = 0; turn < turns.Length; turn++)
            {
                if (turns[turn] == foot.Reverse(edge)) continue;

                ways++;
                onto = turns[turn];
            }

            Assert.True(ways == 1, $"{map}: stretch {edge} carries a corner and its end offers {ways} ways on");
            Assert.True(
                foot.KindOf(edge) != FootEdgeKind.Crossing && foot.KindOf(onto) != FootEdgeKind.Crossing,
                $"{map}: {foot.KindOf(edge)} {edge} carries its corner onto {foot.KindOf(onto)} {onto}");
            Assert.True(
                network.TailLengthM(edge) > 0f,
                $"{map}: stretch {edge} carries a corner of no length at all");
        }

        Assert.True(carried > 0, $"{map}: not one lane carries its own corner");
    }

    /// <summary>
    /// <b>A corner is turned and not cut.</b> Where a walk leaves one lane for the next its heading carries
    /// on into the corner and out of it — a step in heading is a kink, and a kink is a body swerving at a
    /// place where the ground does not bend.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The kinks this catches were the two lanes of one straight pavement failing to meet</b>: the
    /// ground lets neighbouring stretches keep different offsets, so their lanes stand a few centimetres
    /// apart, and bridged end to end that step was a chord across the walk with a kink at each end of it.
    /// 946 of Odesa's 2590 carried corners were laid that way before the step was given the room to be
    /// rounded over.
    /// </para>
    /// <para>
    /// <b>A handful of corners are still cut</b>, because <c>StraightBetween</c> is the last resort where
    /// the two poses defeat the arc construction and a stretch too short to give the corner room is a real
    /// thing in a town. What is asserted is that they stay a handful.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryCornerIsTurnedAndNotCut(string map)
    {
        var (foot, network) = Of(map);

        var corners = 0;
        var cut = 0;
        var worstDeg = 0f;
        var worst = string.Empty;

        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            var lane = network.LaneOf(edge);
            if (lane.Length == 0) continue;

            var turns = network.TurnsFrom(edge);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                if (turns[turn] == foot.Reverse(edge)) continue;

                var slot = network.TurnSlotAt(edge, turn);
                var join = network.JoinArcs(slot);
                var onward = network.LaneOf(turns[turn]);
                if (join.Length == 0 || onward.Length == 0) continue;

                // A carried corner is the end of the lane itself, so what it has to meet is the lane a tail
                // before its end; any other is laid off the setback the end gives up.
                var carried = network.TailOf(edge) == slot;
                var leavesAtM = carried
                    ? network.LaneLengthM(edge) - network.TailLengthM(edge)
                    : network.LaneLengthM(edge) - network.JoinFromM(slot);

                corners++;
                var intoDeg = StepDeg(Spline.SampleAt(lane, leavesAtM).HeadingRad, join[0].HeadingRad);
                var outOfDeg = StepDeg(
                    join[^1].HeadingAtRad(join[^1].LengthM), Spline.SampleAt(onward, network.JoinToM(slot)).HeadingRad);

                var kinkDeg = MathF.Max(intoDeg, outOfDeg);
                if (kinkDeg <= KinkDeg) continue;

                cut++;
                if (kinkDeg <= worstDeg) continue;

                worstDeg = kinkDeg;
                worst = $"{foot.KindOf(edge)} {edge} into {foot.KindOf(turns[turn])} {turns[turn]}";
            }
        }

        Assert.True(corners > 0, $"{map}: not one corner was laid");
        Assert.True(
            cut * 100 <= corners,
            $"{map}: {cut} of {corners} corners are cut rather than turned, worst {worstDeg:F0}° at {worst}");
    }

    /// <summary>How far a heading steps at a seam, in degrees, which is nought where one line carries on into the next.</summary>
    static float StepDeg(float fromRad, float toRad) => MathF.Abs(Spline.WrapRad(toRad - fromRad)) * 180f / MathF.PI;

    /// <summary>Under this a seam is the arc arithmetic and not a swerve. A degree is a centimetre in a metre.</summary>
    const float KinkDeg = 1f;

    /// <summary>
    /// <b>A crossing's own lane is the paint and no more of it.</b> Its edge is laid from one pavement's
    /// line to the other's, so half a pavement's band at each end of it is pavement — that half band is
    /// what the junction takes back, and what is left starts at the kerb. Less than that only where the
    /// pavement it meets is too short to give it, which the box is bounded by.
    /// </summary>
    static void AtTheKerb(string map, FootGraph foot, WalkingNetwork network, int crossing, int pavement, float takenM)
    {
        var kerbM = foot.BandM(pavement) * 0.5f;
        Assert.True(
            takenM <= kerbM + ToleranceM,
            $"{map}: crossing {crossing} gives up {takenM:F2} m to the corner off {pavement}, past the "
            + $"{kerbM:F2} m of pavement its own edge is laid over");

        // And short of it only by what a stretch sharing the same end could not afford, which is bounded by
        // half of the shortest of them: the start stays in the kerb-side half of the band whatever meets
        // there, and never back at the pavement's own line where the crossing's edge begins.
        var affordedM = MathF.Min(
            kerbM, MathF.Min(network.LaneLengthM(pavement), network.LaneLengthM(crossing)) * 0.5f);
        Assert.True(
            takenM >= MathF.Min(affordedM, kerbM * 0.5f) - ToleranceM,
            $"{map}: crossing {crossing} is walked from {takenM:F2} m in rather than from the kerb at "
            + $"{affordedM:F2} m");
    }

    /// <summary>
    /// <b>And the pavement stops at the zebra's mouth.</b> The other half of the same box: what a stretch
    /// gives up at an end is the half-band of what runs across it, so a pavement meeting a crossing keeps
    /// its lane clear of the paint's own width — or as much of it as the two of them can afford.
    /// </summary>
    static void AtTheMouth(string map, FootGraph foot, WalkingNetwork network, int pavement, int crossing, float takenM)
    {
        var mouthM = MathF.Min(
            foot.BandM(crossing) * 0.5f,
            MathF.Min(network.LaneLengthM(pavement), network.LaneLengthM(crossing)) * 0.5f);

        Assert.True(
            takenM >= MathF.Min(mouthM, foot.BandM(crossing) * 0.25f) - ToleranceM,
            $"{map}: pavement {pavement} gives up {takenM:F2} m at the crossing {crossing} it meets, inside the "
            + $"{mouthM:F2} m of paint its own end stands in");
    }

    /// <summary>A centimetre, which is the arc arithmetic's and not the mitre's — the lanes it joins are its own endpoints by construction.</summary>
    const float ToleranceM = 0.01f;

    /// <summary>The tightest circle the feet can hold at walking pace: the speed over the turn rate, and a corner tighter than it is a corner nothing can walk.</summary>
    static float TightestFeetM =>
        SimConfig.Shipped().PersonWalkSpeedMps / (SimConfig.Shipped().PersonTurnRateDegPerS * MathF.PI / 180f);

    static bool Tighter(ReadOnlySpan<ArcSeg> join)
    {
        foreach (var arc in join)
        {
            if (MathF.Abs(arc.Curvature) > 1e-6f && 1f / MathF.Abs(arc.Curvature) < TightestFeetM) return true;
        }

        return false;
    }

    static int MostArcs(FootGraph foot)
    {
        var most = 1;
        for (var edge = 0; edge < foot.EdgeCount; edge++) most = Math.Max(most, foot.ArcsOf(edge).Length);

        return most;
    }

    /// <summary>
    /// A walk from where a body stands to somewhere it can get: the chain is contiguous, it starts on a
    /// link the body was standing on, and it finishes on the link the destination stands on.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AWalkToSomewhereReachableIsAContiguousChain(string map)
    {
        var (foot, network) = Of(map);
        var planner = new RoutePlanner(network.Graph);
        var route = new int[network.Graph.LinkCount];
        Span<RouteEntry> entries = stackalloc RouteEntry[2];
        Span<RouteGoal> goals = stackalloc RouteGoal[2];

        var planned = 0;
        for (var edge = 0; edge < foot.EdgeCount; edge += 2 + 2 * (foot.EdgeCount / 40))
        {
            var fromM = Spline.SampleAt(foot.ArcsOf(edge), foot.LengthM(edge) * 0.5f).PositionM;
            var toEdge = Onward(foot, edge, splits: 3);
            var toM = Spline.SampleAt(foot.ArcsOf(toEdge), foot.LengthM(toEdge) * 0.5f).PositionM;
            if ((toM - fromM).Length() < 1f) continue;

            var entryCount = network.EntriesNear(fromM, entries);
            var goalCount = network.GoalsAt(toM, goals);
            var written = planner.Plan(
                entries[..entryCount], goals[..goalCount], toM, null, route, out var costM, out var goalSlot);

            Assert.True(written > 0, $"{map}: no walk from stretch {edge} to stretch {toEdge}, which is three splits away");
            Assert.InRange(goalSlot, 0, goalCount - 1);
            Assert.Equal(goals[goalSlot].Link, route[written - 1]);
            Assert.True(costM > 0f);

            for (var step = 1; step < written; step++)
            {
                Assert.Equal(network.Graph.ToNode(route[step - 1]), network.Graph.FromNode(route[step]));
            }

            planned++;
        }

        Assert.True(planned > 0, $"{map}: not one walk was asked for");
    }

    /// <summary>
    /// <b>A lane is walked exactly as far as the last mitre off it leaves, and from exactly where the
    /// first mitre onto it rejoins.</b> Further is ground no walk covers — the spur that ran past every
    /// corner into its node until 2026-08-19, 10.4 km of it on Odesa. Less far is a hole between the lane
    /// and a corner that leaves it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ways off one end share one margin</b> — the box that end stands in — so the lane is walked to
    /// that point and every way off it hands over there, the one running straight on included. A spur past
    /// it is ground no walk covers.
    /// </para>
    /// <para>
    /// <b>A lane that carries its own corner is walked to the end of it</b>, because that end is where the
    /// next lane's walk begins — there is no mitre off it to leave at.
    /// </para>
    /// <para>
    /// <b>Turning round on the spot is not a way off a lane</b>, exactly as it is not one on the road side
    /// (`TER-5d`). Counted among them it decided how far a lane was walked, and a lane whose turn-around
    /// gave up more ground than its corner was walked from behind the point that corner lands a body on.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ALaneIsWalkedExactlyAsFarAsTheOutermostMitreAtEachEnd(string map)
    {
        var (foot, network) = Of(map);
        var lane = new ArcSeg[MostArcs(foot)];

        // The first mitre onto each stretch to rejoin it, which is what its own walked span starts at.
        // Turning round on the spot is not one of them: it decides nothing about how far a lane is walked,
        // and counted it pulls the start back behind the corner that lands a body there.
        var arrivesAtM = new float[foot.EdgeCount];
        Array.Fill(arrivesAtM, float.PositiveInfinity);
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            var turns = network.TurnsFrom(edge);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                if (turns[turn] == foot.Reverse(edge)) continue;

                var slot = network.TurnSlotAt(edge, turn);
                arrivesAtM[turns[turn]] = MathF.Min(arrivesAtM[turns[turn]], network.JoinToM(slot));
            }
        }

        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            var walked = network.LaneOf(edge);
            if (walked.Length == 0) continue;

            var laneLengthM = Spline.TotalLengthM(walked);
            var fromM = network.WalkedFromM(edge);
            var toM = network.WalkedToM(edge);

            Assert.True(
                fromM >= 0f && toM <= laneLengthM + ToleranceM && toM >= fromM,
                $"{map}: stretch {edge} is {laneLengthM:F2} m of lane and its walked span is {fromM:F2}–{toM:F2} m");

            var turns = network.TurnsFrom(edge);
            var ways = 0;
            for (var turn = 0; turn < turns.Length; turn++)
            {
                if (turns[turn] != foot.Reverse(edge)) ways++;
            }

            // A stretch with nowhere to go but back keeps the whole of its lane: there is no corner off it
            // to give ground up to.
            var leavesAtM = ways == 0 ? laneLengthM : 0f;
            if (network.TailOf(edge) != WalkingNetwork.NoTurn)
            {
                // The corner is in the lane rather than off the end of it, so the lane is walked whole —
                // and the mitres of its other ways are laid off a lane end this one no longer has.
                leavesAtM = laneLengthM;
            }
            else
            {
                for (var turn = 0; turn < turns.Length; turn++)
                {
                    if (turns[turn] == foot.Reverse(edge)) continue;

                    leavesAtM = MathF.Max(leavesAtM, laneLengthM - network.JoinFromM(network.TurnSlotAt(edge, turn)));
                }
            }

            Assert.True(
                MathF.Abs(toM - leavesAtM) < ToleranceM,
                $"{map}: stretch {edge} is walked to {toM:F2} m of its {laneLengthM:F2} m lane and the last " +
                $"mitre off it leaves at {leavesAtM:F2} m");
            Assert.True(
                MathF.Abs(fromM - (float.IsFinite(arrivesAtM[edge]) ? arrivesAtM[edge] : 0f)) < ToleranceM,
                $"{map}: stretch {edge} is walked from {fromM:F2} m and the first mitre onto it rejoins at " +
                $"{arrivesAtM[edge]:F2} m");
        }
    }

    /// <summary>A stretch a body on this one can certainly reach: the first way on, taken a few times over.</summary>
    static int Onward(FootGraph foot, int edge, int splits)
    {
        for (var split = 0; split < splits; split++)
        {
            var leaving = foot.EdgesOut(foot.ToNode(edge));
            if (leaving.Length == 0) break;

            edge = leaving[split % leaving.Length];
        }

        return edge;
    }
}
