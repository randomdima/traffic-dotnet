using System.Numerics;
using TrafficSimulation.CityGen;
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
[Trait(Priority.Key, Priority.P4)]
public class RoadGraphTests
{
    /// <summary>How far off one another two headings may be and still be the same line: a degree.</summary>
    const float StraightThroughRad = MathF.PI / 180f;

    /// <summary>A centimetre, which is the arc arithmetic's and not a geometry anybody drew.</summary>
    const float ToleranceM = 0.01f;

    public static TheoryData<string> Maps => Towns.EveryTown();

    static RoadGraph GraphOf(string map) => RoadGraph.Build(Towns.Of(map), SimConfig.Shipped());

    /// <summary>
    /// Every lane runs between two nodes the graph has — a junction the plan named, or a place cut into a
    /// road for the car park hanging off it (GEN-4h) — and the plan's own junctions are the first of them,
    /// because nothing is renumbered.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryLaneRunsBetweenTwoPlacesAndNamesAJunctionOnlyWhereItEndsAtOne(string map)
    {
        var plan = Towns.Of(map);
        var graph = GraphOf(map);

        Assert.NotEqual(0, graph.LaneCount);
        Assert.Equal(plan.Junctions.Count, graph.JunctionCount);
        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            Assert.InRange(graph.Places.Starting(lane), 0, graph.Places.Count - 1);
            Assert.InRange(graph.Places.Arriving(lane), 0, graph.Places.Count - 1);
            Assert.Equal(graph.LaneEndsAtAPlace[lane], graph.LaneToJunction[lane] == CityPlan.NoRecord);
            Assert.InRange(graph.LaneFromJunction[lane], CityPlan.NoRecord, graph.JunctionCount - 1);
            Assert.InRange(graph.LaneToJunction[lane], CityPlan.NoRecord, graph.JunctionCount - 1);
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
            if (!graph.LaneEndsAtAPlace[lane]) continue;

            foreach (var onward in graph.LanesFrom(lane))
            {
                if (onward == graph.LaneReverse[lane]) continue;

                var slot = graph.ConnectorBetween(lane, onward);
                Assert.Equal(0f, graph.ConnectorLengthM(slot), 3);
                Assert.True(
                    (graph.EndOf(lane).PositionM - graph.StartOf(onward).PositionM).Length() < 1e-3f,
                    $"{map}: lane {lane} ends away from lane {onward} at the place they share");
            }
        }
    }

    /// <summary>
    /// TER-4a's other half: the two lanes of a stretch are the same road driven both ways, so each is
    /// the other's reverse and they run between the same pair of nodes in opposite directions.
    /// <b>A stretch of a one-way road has one lane and no reverse at all</b> (TER-4d).
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryLaneHasTheOneRunningTheOtherWayUnlessItsRoadRunsOneWay(string map)
    {
        var plan = Towns.Of(map);
        var graph = GraphOf(map);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var back = graph.LaneReverse[lane];
            if (plan.Roads.Flow[graph.LaneRoad[lane]] != RoadFlow.BothWays)
            {
                Assert.Equal(RoadGraph.NoLane, back);
                continue;
            }

            Assert.Equal(lane, graph.LaneReverse[back]);
            Assert.Equal(graph.LaneRoad[lane], graph.LaneRoad[back]);
            Assert.Equal(graph.LaneFromJunction[lane], graph.LaneToJunction[back]);
            Assert.Equal(graph.LaneToJunction[back], graph.LaneFromJunction[lane]);
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
        var terrain = new GroundLocator(plan, SimConfig.Shipped());

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

            // Read in the road's own frame, so a backward lane is the negative of a forward one — and a
            // one-way road's own lane is the middle of it, there being no other lane to keep off (TER-4d).
            var expectedM = plan.Roads.Flow[road] == RoadFlow.BothWays
                ? plan.Roads.WidthM[road] * 0.25f * config.RoadSideSign * (graph.LaneForward[lane] ? 1f : -1f)
                : 0f;
            Assert.True(
                MathF.Abs(acrossM - expectedM) < 0.1f,
                $"{map}: lane {lane} sits {acrossM:F2} m across its road's centreline, not {expectedM:F2} m");
        }
    }

    /// <summary>
    /// <b>A lane runs on into the lane opposite it without stepping sideways to do it</b> (TER-4d): where a
    /// car leaves one lane on the heading it arrived on, the two lanes stand on one line. It is what a
    /// one-way street standing on the half of the carriageway it is driven buys, and what the same street
    /// laid down the middle of that ground cannot give — there the lane it runs into is half a lane over,
    /// and every car through the junction is steered across the gap.
    /// </summary>
    /// <remarks>
    /// Asked of the movements that are straight through and of no others, because a lane leaving at an
    /// angle is a turn and moving a car across the junction is what a turn is for. <b>What a straight pair
    /// may still be apart is what the ground between them carries</b>: the two lanes hand over a junction's
    /// width apart, so the angle between them over that gap is the step it is allowed and the rest is a
    /// millimetre of arithmetic.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ALaneRunsOnIntoTheOneOppositeWithoutSteppingSideways(string map)
    {
        var graph = GraphOf(map);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var end = graph.EndOf(lane);
            foreach (var onto in graph.LanesFrom(lane))
            {
                var start = graph.StartOf(onto);
                var apartRad = MathF.Acos(Math.Clamp(Vector2.Dot(end.Direction, start.Direction), -1f, 1f));
                if (apartRad > StraightThroughRad) continue;

                // What the pair may be apart: what the angle carries over the ground between them, and what
                // two roads laid at different lane widths put between their middles whatever else is true.
                var overM = start.PositionM - end.PositionM;
                var acrossM = MathF.Abs(Vector2.Dot(overM, end.Right));
                var carriedM = (overM.Length() * MathF.Sin(apartRad))
                               + (MathF.Abs(graph.LaneWidthM[lane] - graph.LaneWidthM[onto]) * 0.5f)
                               + ToleranceM;
                Assert.True(
                    acrossM <= carriedM,
                    $"{map}: lane {lane} hands over to lane {onto} {acrossM:F2} m to one side of its own line, "
                    + $"which {apartRad * 180f / MathF.PI:F2} degrees over {overM.Length():F2} m between lanes "
                    + $"{graph.LaneWidthM[lane]:F2} m and {graph.LaneWidthM[onto]:F2} m wide does not carry");
            }
        }
    }

    /// <summary>
    /// A lane is as wide as the ground it was cut out of: the share of the carriageway its road declared
    /// that its own direction has — half of it both ways and the whole of it one way (TER-4d) — and exactly
    /// twice the distance its own line was moved off the centreline.
    /// </summary>
    /// <remarks>
    /// <b>The width is the model's and not a picture's.</b> It is the number the follower is held to a
    /// quarter of (<see cref="SimConfig.CarOffPathM"/>), the number the pavement band starts at the edge
    /// of, and the number the tarmac is laid to — so anything that draws a lane draws this and never a
    /// figure of its own.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ALaneIsItsShareOfTheCarriagewayAndTwiceItsOwnOffset(string map)
    {
        var plan = Towns.Of(map);
        var graph = GraphOf(map);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var road = graph.LaneRoad[lane];
            var declaredM = plan.Roads.WidthM[road] / plan.Roads.LanesOn(road);
            Assert.Equal(declaredM, graph.LaneWidthM[lane], tolerance: 1e-4f);

            var centreline = plan.Roads.SegmentsOf(graph.LaneRoad[lane]);
            var start = graph.StartOf(lane);
            var sample = Spline.SampleAt(centreline, Spline.ProjectM(centreline, start.PositionM, 0f, float.MaxValue));
            var acrossM = MathF.Abs(Vector2.Dot(start.PositionM - sample.PositionM, sample.Right));

            // Half a lane off the middle where the road is shared with the oncoming traffic, and down the
            // middle where there is none of it (TER-4d).
            var offsetM = plan.Roads.LanesOn(road) == 2 ? graph.LaneWidthM[lane] * 0.5f : 0f;
            Assert.True(
                MathF.Abs(acrossM - offsetM) < 0.1f,
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
            var leaving = graph.Places.LanesLeaving(graph.Places.Arriving(lane));
            var reverse = graph.LaneReverse[lane];

            // Every lane out of the place is a connector out of this one but the ones that face back: its
            // own reverse always, and anything else within the straight tolerance of head-on.
            Assert.InRange(graph.LanesFrom(lane).Length, 0, leaving.Length - (leaving.Contains(reverse) ? 1 : 0));
            Assert.Null(graph.TurnBetween(lane, reverse));

            foreach (var lane2 in graph.LanesFrom(lane))
            {
                Assert.Equal(graph.Places.Arriving(lane), graph.Places.Starting(lane2));
                Assert.NotEqual(reverse, lane2);
            }
        }
    }

    /// <summary>
    /// <b>Every turn carries the line across the box that goes with it, and that line runs from one lane's
    /// own last point to the next lane's own first</b> (TER-5d). A lane is cut back to the points its
    /// movements hand over at, so a junction is a set of connection points and the joins are what run
    /// between them: a join starting or finishing anywhere else would be a break in every line assembled
    /// through it, and a lane running on past one would be ground held twice with a spur nobody drives.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryJoinRunsFromOneLanesEndToTheNextLanesStart(string map)
    {
        var graph = GraphOf(map);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var turns = graph.LanesFrom(lane);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                var slot = graph.ConnectorsFrom(lane)[turn];
                var join = graph.ConnectorArcs(slot);
                var leaves = graph.EndOf(lane);
                var arrives = graph.StartOf(turns[turn]);

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
                var endM = (Spline.SampleAt(join, graph.ConnectorLengthM(slot)).PositionM - arrives.PositionM).Length();
                Assert.True(startM < JoinToleranceM, $"{map}: lane {lane} onto {turns[turn]} starts {startM:F3} m off its own lane");
                Assert.True(endM < JoinToleranceM, $"{map}: lane {lane} onto {turns[turn]} ends {endM:F3} m off the lane it joins");
            }
        }
    }

    /// <summary>
    /// <b>A lane is cut back only as far as it takes for its joins to reach the junction's own corner
    /// radius, and no further</b> (TER-5, TER-5d). The turn that does not reach it is the one the town has
    /// no room for — both its lanes have already given up everything they can spare — and that is a fact
    /// about the junction rather than about the line drawn through it.
    /// </summary>
    /// <remarks>
    /// The pair no cut back would ever help — two opposing lanes a lane's width apart, a semicircle however
    /// far back it is drawn from — is not a movement and is not in the table (TER-5f).
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryJoinIsAsWideAsItNeedsToBeOrAsWideAsTheTownAllows(string map)
    {
        var config = SimConfig.Shipped();
        var graph = GraphOf(map);

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var turns = graph.LanesFrom(lane);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                var onto = turns[turn];
                var slot = graph.ConnectorsFrom(lane)[turn];

                // Measured against the stretches the cut back was settled on, which is what each lane still
                // had when the widening asked how much it could spare.
                var wholeM = MathF.Min(
                    graph.LaneLengthM[lane] + graph.LaneCutBackM[lane],
                    graph.LaneLengthM[onto] + graph.LaneCutBackM[onto]);
                var capM = MathF.Min(
                    config.IntersectionCornerRadiusM,
                    MathF.Max(0f, wholeM - config.LaneShortestStretchM) * 0.5f);

                var bend = 0f;
                foreach (var arc in graph.ConnectorArcs(slot)) bend = MathF.Max(bend, MathF.Abs(arc.Curvature));

                var holdable = bend <= 1e-6f || 1f / bend >= config.IntersectionCornerRadiusM;
                Assert.True(
                    holdable || (graph.LaneCutBackM[lane] >= capM - 1e-3f && graph.LaneCutBackM[onto] >= capM - 1e-3f),
                    $"{map}: lane {lane} onto {onto} bends to {1f / bend:F2} m where the two of them gave up " +
                    $"{graph.LaneCutBackM[lane]:F2} m and {graph.LaneCutBackM[onto]:F2} m of the {capM:F2} m " +
                    "the town allows an end");
            }
        }
    }

    /// <summary>Five centimetres, which is <see cref="ArcSeg"/>'s own arithmetic and not the join's geometry.</summary>
    const float JoinToleranceM = 0.05f;
}
