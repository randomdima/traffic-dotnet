using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The lane graph asked of every shipped map: <b>a lane is a line a car may actually drive</b>. Every
/// assertion here is the plan's own ground put to the graph's own geometry — never one derived figure
/// compared with another, which would only prove the derivation consistent with itself.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class RoadGraphTests
{
    public static TheoryData<string> Maps => Towns.EveryTown();

    static RoadGraph GraphOf(string map) => RoadGraph.Build(Towns.Of(map), SimConfig.Shipped());

    /// <summary>
    /// Every lane runs between two nodes the graph has — a junction the plan named, or a place cut into a
    /// road for the car park hanging off it (GEN-4h) — and the plan's own junctions are the first of them,
    /// because nothing is renumbered.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryLaneRunsBetweenTwoNodesItNames(string map)
    {
        var plan = Towns.Of(map);
        var graph = GraphOf(map);

        Assert.NotEqual(0, graph.LaneCount);
        Assert.Equal(plan.Junctions.Count, graph.JunctionCount);
        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            Assert.InRange(graph.LaneFromNode[lane], 0, graph.NodeCount - 1);
            Assert.InRange(graph.LaneToNode[lane], 0, graph.NodeCount - 1);
            Assert.True(graph.LaneLengthM[lane] > 0f, $"{map}: lane {lane} has no length");
        }
    }

    /// <summary>
    /// <b>A place is a cut and not a disc</b> (GEN-4h): the two lanes it makes of one meet at a point, so
    /// the movement between them is a join of no length and no ground is lost to it. Every other node takes
    /// its own bite, which is what a junction disc is.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void APlaceCutIntoARoadTakesNoGroundOffIt(string map)
    {
        var graph = GraphOf(map);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            if (!graph.IsAPlace(graph.LaneToNode[lane])) continue;

            foreach (var onward in graph.TurnsFrom(lane))
            {
                if (onward == graph.LaneReverse[lane]) continue;

                var slot = graph.TurnSlot(lane, onward);
                Assert.Equal(0f, graph.JoinLengthM(slot), 3);
                Assert.True(
                    (graph.EndOf(lane).PositionM - graph.StartOf(onward).PositionM).Length() < 1e-3f,
                    $"{map}: lane {lane} ends away from lane {onward} at the place they share");
            }
        }
    }

    /// <summary>
    /// TER-4a's other half: the two lanes of a stretch are the same road driven both ways, so each is
    /// the other's reverse and they run between the same pair of nodes in opposite directions.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryLaneHasTheOneRunningTheOtherWay(string map)
    {
        var graph = GraphOf(map);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var back = graph.LaneReverse[lane];
            Assert.Equal(lane, graph.LaneReverse[back]);
            Assert.Equal(graph.LaneRoad[lane], graph.LaneRoad[back]);
            Assert.Equal(graph.LaneFromNode[lane], graph.LaneToNode[back]);
            Assert.Equal(graph.LaneToNode[back], graph.LaneFromNode[lane]);
        }
    }

    /// <summary>
    /// The one that matters to a driver: <b>the line a lane is driven on lies on ground a car may be
    /// on</b>. A lane offset to the wrong side, taken from the wrong width, or cut at the wrong place
    /// puts the line on the pavement — where a car following it perfectly is a car on the footway, and
    /// no amount of lane discipline will show it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryLaneIsDrivenOnGroundACarMayBeOn(string map)
    {
        var plan = Towns.Of(map);
        var graph = GraphOf(map);
        var terrain = new TerrainGrid(plan, SimConfig.Shipped());

        var offRoad = 0;
        var worst = string.Empty;
        var laneCount = 0;
        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var arcs = graph.ArcsOf(lane);
            var lengthM = graph.LaneLengthM[lane];
            var steps = Math.Max(2, (int)MathF.Ceiling(lengthM));
            var off = 0;
            for (var step = 0; step <= steps; step++)
            {
                var pointM = Spline.SampleAt(arcs, lengthM * step / steps).PositionM;
                if (terrain.At(pointM).Drivable) continue;

                off++;
                if (worst.Length == 0)
                {
                    // The road as well as the lane: a lane number alone says nothing about which piece of
                    // the map to go and look at, and a generated town is laid again rather than opened.
                    var road = graph.LaneRoad[lane];
                    worst = $"lane {lane} of road {road}, junctions {plan.Roads.FromJunction[road]} to "
                            + $"{plan.Roads.ToJunction[road]} over {plan.Roads.SegmentsOf(road).Length} arc(s), "
                            + $"at {pointM} stands on {terrain.GroundAt(pointM)}";
                }
            }

            laneCount++;
            if (off > 0) offRoad++;
        }

        Assert.True(offRoad == 0, $"{map}: {offRoad} of {laneCount} lanes are driven over ground a car may not be on — {worst}");
    }

    /// <summary>
    /// TER-4a: the lane is the one to the <em>right</em> of the centreline in the direction of travel,
    /// which is what makes two cars meeting pass each other rather than through each other.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryLaneKeepsToItsOwnSideOfTheRoad(string map)
    {
        var plan = Towns.Of(map);
        var graph = GraphOf(map);
        var config = SimConfig.Shipped();

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var road = graph.LaneRoad[lane];
            var centreline = plan.Roads.SegmentsOf(road);
            var start = graph.StartOf(lane);
            var onCentreline = Spline.ProjectM(centreline, start.PositionM, 0f, float.MaxValue);
            var sample = Spline.SampleAt(centreline, onCentreline);
            var acrossM = Vector2.Dot(start.PositionM - sample.PositionM, sample.Right);

            // Read in the road's own frame, so a backward lane is the negative of a forward one.
            var expectedM = plan.Roads.WidthM[road] * 0.25f * config.RoadSideSign * (graph.LaneForward[lane] ? 1f : -1f);
            Assert.True(
                MathF.Abs(acrossM - expectedM) < 0.1f,
                $"{map}: lane {lane} sits {acrossM:F2} m across its road's centreline, not {expectedM:F2} m");
        }
    }

    /// <summary>
    /// A lane is as wide as the ground it was cut out of: half the carriageway its road declared, and
    /// exactly twice the distance its own line was moved off the centreline.
    /// </summary>
    /// <remarks>
    /// <b>The width is the model's and not a picture's.</b> It is the number the follower is held to a
    /// quarter of (<see cref="SimConfig.CarOffPathM"/>), the number the pavement band starts at the edge
    /// of, and the number the tarmac is laid to — so anything that draws a lane draws this and never a
    /// figure of its own.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ALaneIsHalfTheCarriagewayAndTwiceItsOwnOffset(string map)
    {
        var plan = Towns.Of(map);
        var graph = GraphOf(map);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var declaredM = plan.Roads.WidthM[graph.LaneRoad[lane]];
            Assert.Equal(declaredM * 0.5f, graph.LaneWidthM[lane], tolerance: 1e-4f);

            var centreline = plan.Roads.SegmentsOf(graph.LaneRoad[lane]);
            var start = graph.StartOf(lane);
            var sample = Spline.SampleAt(centreline, Spline.ProjectM(centreline, start.PositionM, 0f, float.MaxValue));
            var acrossM = MathF.Abs(Vector2.Dot(start.PositionM - sample.PositionM, sample.Right));
            Assert.True(
                MathF.Abs(acrossM - graph.LaneWidthM[lane] * 0.5f) < 0.1f,
                $"{map}: lane {lane} is {graph.LaneWidthM[lane]:F2} m wide but its line was laid {acrossM:F2} m " +
                "off the centreline");
        }
    }

    /// <summary>
    /// A turn is a fact about the road, and the three kinds are exhaustive: every lane leaving the node a
    /// lane arrives at is joined to it by exactly one of them — <b>except the one that goes back the way it
    /// came</b>, which is no movement at all (TER-5f) and is not in the table.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryTurnIsClassifiedAndTheReverseIsNoTurnAtAll(string map)
    {
        var graph = GraphOf(map);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var leaving = graph.LanesOut(graph.LaneToNode[lane]);
            var reverse = graph.LaneReverse[lane];

            // Every lane out of the node is a turn out of this one but the ones that face back: its own
            // reverse always, and anything else within the straight tolerance of head-on.
            Assert.InRange(graph.TurnsFrom(lane).Length, 0, leaving.Length - (leaving.Contains(reverse) ? 1 : 0));
            Assert.Null(graph.TurnBetween(lane, reverse));

            foreach (var lane2 in graph.TurnsFrom(lane))
            {
                Assert.Equal(graph.LaneToNode[lane], graph.LaneFromNode[lane2]);
                Assert.NotEqual(reverse, lane2);
            }
        }
    }

    /// <summary>
    /// <b>A lane has one end, whatever is driven off it.</b> Every movement out of a lane leaves it at the
    /// same point and every movement into one arrives at the same point, so the boundary between a lane and
    /// the box it runs into is a place and not a property of the turn being taken — which is what lets
    /// anything reading the pair name it without naming a movement.
    /// </summary>
    /// <remarks>
    /// There is no movement through a box that reverses the direction of travel (TER-5f), so there is
    /// nothing here to leave out: every turn in the table is one a setback helps.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryMovementThroughALaneEndUsesTheSamePoint(string map)
    {
        var graph = GraphOf(map);
        var arrivesAtM = new float[graph.LaneCount];
        Array.Fill(arrivesAtM, float.NaN);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var turns = graph.TurnsFrom(lane);
            var leavesAtM = float.NaN;
            for (var turn = 0; turn < turns.Length; turn++)
            {
                var slot = graph.TurnSlotAt(lane, turn);
                if (float.IsNaN(leavesAtM)) leavesAtM = graph.JoinFromM(slot);
                if (float.IsNaN(arrivesAtM[turns[turn]])) arrivesAtM[turns[turn]] = graph.JoinToM(slot);

                Assert.True(
                    MathF.Abs(graph.JoinFromM(slot) - leavesAtM) < JoinToleranceM,
                    $"{map}: lane {lane} is left at {graph.JoinFromM(slot):F2} m for {turns[turn]} and at "
                    + $"{leavesAtM:F2} m for its other turns");
                Assert.True(
                    MathF.Abs(graph.JoinToM(slot) - arrivesAtM[turns[turn]]) < JoinToleranceM,
                    $"{map}: lane {turns[turn]} is joined at {graph.JoinToM(slot):F2} m from {lane} and at "
                    + $"{arrivesAtM[turns[turn]]:F2} m from another lane");

                // And the lane carries both of its own points, which is what says where its own metres begin
                // under a line assembled through the junction behind it, and where anything drawing it
                // stops rather than running a spur on into the box.
                Assert.True(
                    MathF.Abs(graph.JoinedAtM(turns[turn]) - graph.JoinToM(slot)) < JoinToleranceM,
                    $"{map}: lane {turns[turn]} says it is joined at {graph.JoinedAtM(turns[turn]):F2} m and the "
                    + $"turn from {lane} joins it at {graph.JoinToM(slot):F2} m");
                Assert.True(
                    MathF.Abs(graph.LeftAtM(lane) - graph.JoinFromM(slot)) < JoinToleranceM,
                    $"{map}: lane {lane} says it is left at {graph.LeftAtM(lane):F2} m from its end and the "
                    + $"turn onto {turns[turn]} leaves it at {graph.JoinFromM(slot):F2} m");
            }
        }
    }

    /// <summary>
    /// <b>Every turn carries the line across the box that goes with it</b>, and that line meets the two
    /// lanes it joins exactly where their own setbacks say it does. A join that started or finished
    /// anywhere else would be a break in every line assembled through it and a movement drawn beside the
    /// road rather than onto it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryJoinMeetsTheTwoLanesItJoins(string map)
    {
        var graph = GraphOf(map);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var turns = graph.TurnsFrom(lane);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                var slot = graph.TurnSlotAt(lane, turn);
                var join = graph.JoinArcs(slot);
                var leaves = Spline.SampleAt(graph.ArcsOf(lane), graph.LaneLengthM[lane] - graph.JoinFromM(slot));
                var arrives = Spline.SampleAt(graph.ArcsOf(turns[turn]), graph.JoinToM(slot));

                // A pair of lanes that already meet needs no line between them, which is the one case
                // with nothing to check.
                if (join.Length == 0)
                {
                    Assert.True(
                        (arrives.PositionM - leaves.PositionM).Length() < JoinToleranceM,
                        $"{map}: lane {lane} onto {turns[turn]} has no join and its two lanes do not meet");
                    continue;
                }

                var startM = (join[0].StartM - leaves.PositionM).Length();
                var endM = (Spline.SampleAt(join, graph.JoinLengthM(slot)).PositionM - arrives.PositionM).Length();
                Assert.True(startM < JoinToleranceM, $"{map}: lane {lane} onto {turns[turn]} starts {startM:F3} m off its own lane");
                Assert.True(endM < JoinToleranceM, $"{map}: lane {lane} onto {turns[turn]} ends {endM:F3} m off the lane it joins");
            }
        }
    }

    /// <summary>
    /// <b>A join is widened only as far as it takes to reach the junction's own corner radius, and no
    /// further.</b> The turn that does not reach it is the one the town has no room to widen — with the
    /// whole of both lanes already taken into the turn — and that is a fact about the junction rather
    /// than about the line drawn through it.
    /// </summary>
    /// <remarks>
    /// The pair no setback ever helps — two opposing lanes a lane's width apart, a semicircle however far
    /// back it is taken — is not a movement and is not in the table (TER-5f).
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryJoinIsAsWideAsItNeedsToBeOrAsWideAsTheTownAllows(string map)
    {
        var config = SimConfig.Shipped();
        var graph = GraphOf(map);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var turns = graph.TurnsFrom(lane);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                var slot = graph.TurnSlotAt(lane, turn);
                var capM = MathF.Min(
                    config.IntersectionCornerRadiusM,
                    MathF.Min(graph.LaneLengthM[lane], graph.LaneLengthM[turns[turn]]) * 0.5f);

                var bend = 0f;
                foreach (var arc in graph.JoinArcs(slot)) bend = MathF.Max(bend, MathF.Abs(arc.Curvature));

                var holdable = bend <= 1e-6f || 1f / bend >= config.IntersectionCornerRadiusM;
                Assert.True(
                    holdable || graph.JoinFromM(slot) >= capM - 1e-3f,
                    $"{map}: lane {lane} onto {turns[turn]} bends to {1f / bend:F2} m at a setback of " +
                    $"{graph.JoinFromM(slot):F2} m of the {capM:F2} m the town allows");
            }
        }
    }

    /// <summary>Five centimetres, which is <see cref="ArcSeg"/>'s own arithmetic and not the join's geometry.</summary>
    const float JoinToleranceM = 0.05f;
}
