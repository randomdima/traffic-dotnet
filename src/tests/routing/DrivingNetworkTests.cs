using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;
using Xunit;

namespace TrafficSimulation.Tests.Routing;

/// <summary>
/// The driving network asked of every shipped map. <b>What a contraction can quietly lose is the whole
/// subject</b>: a run that dropped a lane, a station that disagrees with its own weight, or a junction
/// with a choice at it that no route can ever plan through are all invisible from outside — the town
/// still drives, it merely drives somewhere nobody chose.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class DrivingNetworkTests
{
    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    static (RoadGraph Roads, DrivingNetwork Network) Of(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var roads = RoadGraph.Build(plan, config);
        return (roads, DrivingNetwork.Build(roads, BayWays.WhereALegMayTurn(roads, BayWays.Build(plan, roads, config)), plan, config));
    }

    /// <summary>Every road still belongs to some run, exactly once, in one place along it.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryLaneBelongsToExactlyOneRun(string map)
    {
        var (roads, network) = Of(map);
        var runs = network.Runs;
        var timesTravelled = new int[roads.LaneCount];

        for (var link = 0; link < runs.LinkCount; link++)
        {
            foreach (var lane in runs.PiecesOf(link)) timesTravelled[lane]++;
        }

        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            Assert.True(timesTravelled[lane] == 1, $"{map}: lane {lane} is in {timesTravelled[lane]} runs, not one");
            Assert.Equal(lane, runs.PiecesOf(network.LinkOfLane(lane))[network.SlotOfLane(lane)]);
        }
    }

    /// <summary>
    /// <b>A node is a place a driver can go more than one way or a place a leg can be sent to, and nothing
    /// else is a node.</b> Asked both ways round, because each direction catches a different fault: a bend
    /// on the network is a decision nobody makes, and a junction off it is a turn no route could ever plan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ends of a parking section are the second clause</b> (GEN-4h). Nothing is decided at one — a
    /// driver arriving has one way on — and it is on the network anyway, because a leg aimed at a bay of
    /// that car park is routed to it and a place the search cannot name is a place no route can end at.
    /// </para>
    /// <para>
    /// <b>The one bend that is a node is a ring's own anchor.</b> A closed run nothing splits — the band a
    /// car park is wrapped in, a circuit of the test track — would contract to nothing at all, so
    /// <see cref="RunNetwork"/> promotes one of its bends and the ring becomes two links leaving and
    /// returning to that one place. Exactly one, and only where the ring really has no choice on it.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ANetworkNodeIsExactlyAJunctionWithAChoiceAtItOrAPlaceALegIsAimedAt(string map)
    {
        var (roads, network) = Of(map);
        var runs = network.Runs;
        var onNetwork = new bool[roads.NodeCount];
        var ringOf = Rings(roads, out var hasAChoice);
        var anchors = new int[roads.NodeCount];

        for (var node = 0; node < runs.Graph.NodeCount; node++)
        {
            var junction = runs.FineNodeOf(node);
            onNetwork[junction] = true;
            if (roads.LanesOut(junction).Length != 2 || roads.IsAPlace(junction)) continue;

            Assert.False(
                hasAChoice[ringOf[junction]],
                $"{map}: junction {junction} is a bend on a road with a choice on it, and is on the network");
            anchors[ringOf[junction]]++;
        }

        for (var junction = 0; junction < roads.NodeCount; junction++)
        {
            var ways = roads.LanesOut(junction).Length;
            if (ways == 0) continue;

            if (ways != 2 || roads.IsAPlace(junction))
            {
                Assert.True(onNetwork[junction], $"{map}: junction {junction} has {ways} ways on and is off the network");
                continue;
            }

            Assert.True(
                hasAChoice[ringOf[junction]] || anchors[ringOf[junction]] == 1,
                $"{map}: the ring through junction {junction} carries {anchors[ringOf[junction]]} anchors, not one");
        }
    }

    /// <summary>
    /// Which junctions are joined to which by lanes, and whether anything in each such stretch of network
    /// is a place with a choice at it. It is what tells a ring nothing splits from an ordinary street.
    /// </summary>
    static int[] Rings(RoadGraph roads, out bool[] hasAChoice)
    {
        var ringOf = new int[roads.NodeCount];
        for (var node = 0; node < ringOf.Length; node++) ringOf[node] = node;

        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var from = Ring(ringOf, roads.LaneFromNode[lane]);
            var to = Ring(ringOf, roads.LaneToNode[lane]);
            if (from >= 0 && to >= 0) ringOf[from] = to;
        }

        for (var node = 0; node < ringOf.Length; node++) ringOf[node] = Ring(ringOf, node);

        hasAChoice = new bool[roads.NodeCount];
        for (var junction = 0; junction < roads.NodeCount; junction++)
        {
            var ways = roads.LanesOut(junction).Length;
            if (ways is not (0 or 2)) hasAChoice[ringOf[junction]] = true;
        }

        return ringOf;
    }

    static int Ring(int[] ringOf, int node)
    {
        while (node >= 0 && ringOf[node] != node) node = ringOf[node];

        return node;
    }

    /// <summary>
    /// A run's stations agree with its own weight: monotone from one end to the other, starting at zero,
    /// ending at the run's own length, and <b>never priced below the span between its two anchors</b> —
    /// which is what makes the straight line an admissible bound and therefore what lets the search be
    /// A* rather than a flood.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ARunsStationsAgreeWithItsOwnWeight(string map)
    {
        var (roads, network) = Of(map);
        var runs = network.Runs;

        for (var link = 0; link < runs.LinkCount; link++)
        {
            var lanes = runs.PiecesOf(link);
            var stations = runs.StationsOf(link);

            Assert.False(lanes.IsEmpty, $"{map}: run {link} is made of no lanes");
            Assert.Equal(0f, stations[0], 4);
            for (var slot = 1; slot < lanes.Length; slot++)
            {
                Assert.True(
                    stations[slot] > stations[slot - 1],
                    $"{map}: run {link} steps backwards at piece {slot}");
                Assert.Equal(stations[slot - 1] + roads.LaneLengthM[lanes[slot - 1]], stations[slot], 3);
            }

            Assert.Equal(stations[^1] + roads.LaneLengthM[lanes[^1]], runs.LengthM(link), 3);

            var spanM = (runs.Graph.EndAnchorM(link) - runs.Graph.StartAnchorM(link)).Length();
            Assert.True(
                runs.Graph.WeightM(link) >= spanM - 1e-3f,
                $"{map}: run {link} is priced at {runs.Graph.WeightM(link):F2} m over a {spanM:F2} m span");
        }
    }

    /// <summary>
    /// The pieces of a run are travelled in the order they are laid: each ends where the next begins, so
    /// a driver handed a run drives one line and not a set of them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ARunsPiecesAreTravelledEndToEnd(string map)
    {
        var (roads, network) = Of(map);
        var runs = network.Runs;

        for (var link = 0; link < runs.LinkCount; link++)
        {
            var lanes = runs.PiecesOf(link);
            for (var slot = 1; slot < lanes.Length; slot++)
            {
                Assert.Equal(roads.LaneToNode[lanes[slot - 1]], roads.LaneFromNode[lanes[slot]]);
            }

            Assert.Equal(runs.FineNodeOf(runs.Graph.FromNode(link)), roads.LaneFromNode[lanes[0]]);
            Assert.Equal(runs.FineNodeOf(runs.Graph.ToNode(link)), roads.LaneToNode[lanes[^1]]);
        }
    }

    /// <summary>Where a place on a lane stands in its own run, read back through the run's own bisection.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void APlaceOnALaneIsFoundAgainInItsOwnRun(string map)
    {
        var (roads, network) = Of(map);
        var runs = network.Runs;

        for (var lane = 0; lane < roads.LaneCount; lane += 1 + roads.LaneCount / 200)
        {
            var alongLaneM = roads.LaneLengthM[lane] * 0.5f;
            var link = network.LinkOfLane(lane);
            var slot = runs.PieceAt(link, network.PlaceOfM(lane, alongLaneM), out var backM);

            Assert.Equal(network.SlotOfLane(lane), slot);

            // As a distance and not as a number of decimal places: a lane whose half-length lands on the
            // boundary of one rounds to either side of it, and the two answers differ by a twentieth of
            // the tolerance being asked for.
            Assert.Equal(alongLaneM, backM, ReadBackToleranceM);
        }
    }

    /// <summary>A millimetre, which is the arc arithmetic's own and not the network's.</summary>
    const float ReadBackToleranceM = 1e-3f;

    /// <summary>
    /// A route from where a driver stands to somewhere it can get: the chain is contiguous, it starts on
    /// the link the driver was already committed to, and it finishes on the link the destination stands on.
    /// </summary>
    /// <remarks>
    /// The destination is a few turns downstream rather than half a town away, because <b>a scenario map
    /// is deliberately in pieces</b> — five isolated streets — and a route between two of them not existing
    /// is the map being what it is rather than the search failing.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ARouteToSomewhereReachableIsAContiguousChain(string map)
    {
        var (roads, network) = Of(map);
        var planner = new RoutePlanner(network.Graph);
        var route = new int[network.Graph.LinkCount];
        Span<RouteGoal> goals = stackalloc RouteGoal[2];

        var planned = 0;
        for (var lane = 0; lane < roads.LaneCount; lane += 1 + roads.LaneCount / 40)
        {
            var toLane = Downstream(roads, lane, turns: 3);
            var toM = Spline.SampleAt(roads.ArcsOf(toLane), roads.LaneLengthM[toLane] * 0.5f).PositionM;

            var goalCount = network.GoalsAt(toM, goals);
            var written = planner.Plan(
                [network.EntryOnLane(lane, 0f)], goals[..goalCount], toM, null, route, out var costM, out var goalSlot);

            Assert.True(written > 0, $"{map}: no route from lane {lane} to lane {toLane}, which is three turns away");
            Assert.InRange(goalSlot, 0, goalCount - 1);
            Assert.Equal(network.LinkOfLane(lane), route[0]);
            Assert.Equal(goals[goalSlot].Link, route[written - 1]);
            Assert.True(costM > 0f);

            for (var step = 1; step < written; step++)
            {
                Assert.Equal(network.Graph.ToNode(route[step - 1]), network.Graph.FromNode(route[step]));
            }

            planned++;
        }

        Assert.True(planned > 0, $"{map}: not one route was asked for");
    }

    /// <summary>A lane a driver on this one can certainly get to: the first turn out, taken a few times over.</summary>
    /// <remarks>
    /// <b>Every way out of a lane is one a car may drive</b> (TER-5f), so the walk needs no rule of its
    /// own about which of them to decline: a lane with no turn out is a dead end, and the walk stops
    /// there rather than turning the car round in the road.
    /// </remarks>
    static int Downstream(RoadGraph roads, int lane, int turns)
    {
        for (var turn = 0; turn < turns; turn++)
        {
            var onward = roads.TurnsFrom(lane);
            if (onward.Length == 0) break;

            lane = onward[turn % onward.Length];
        }

        return lane;
    }
}
