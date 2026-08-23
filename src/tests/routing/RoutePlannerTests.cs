using System.Numerics;
using TrafficSimulation.World.Routing;
using Xunit;

namespace TrafficSimulation.Tests.Routing;

/// <summary>
/// The one A* both agent kinds search with, asked on hand-laid graphs where every weight and every turn
/// price is known by inspection. <b>Nothing here is a town</b> — the whole point of the global tier is
/// that it could not tell a boulevard from a zebra, so a test that needed a town would be testing the
/// wrong thing.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class RoutePlannerTests
{
    /// <summary>
    /// The relation the search's bound rests on: a link is never priced below the span between its own
    /// two anchors, enforced where a link is laid rather than asserted afterwards.
    /// </summary>
    [Fact]
    public void ALinkIsNeverPricedBelowTheSpanBetweenItsAnchors()
    {
        var builder = new TravelGraph.Builder();
        var from = builder.AddNode(Vector2.Zero);
        var to = builder.AddNode(new Vector2(100f, 0f));
        builder.AddLink(from, to, 1f);

        var graph = builder.Build(new FreeTurns());

        Assert.Equal(100f, graph.WeightM(0), 3);
    }

    /// <summary>
    /// <b>What a link costs as the last one is not its weight.</b> The route stops part-way along it and
    /// pays for the run into the destination and no more — charge the whole link and the two directions
    /// of one stretch cost the same, and the search has no reason to prefer the one that gets there first.
    /// </summary>
    [Fact]
    public void TheLastLinkIsPricedAtTheRunIntoTheDestination()
    {
        var town = Line();
        var planner = new RoutePlanner(town.Graph);
        Span<int> route = stackalloc int[8];

        var written = planner.Plan(
            [new RouteEntry(town.IntoStart, 0f, 50f)], [new RouteGoal(town.Out, 10f)], new Vector2(10f, 0f),
            null, route, out var costM, out var goalSlot);

        Assert.Equal(2, written);
        Assert.Equal(town.IntoStart, route[0]);
        Assert.Equal(town.Out, route[1]);
        Assert.Equal(0, goalSlot);
        Assert.Equal(60f, costM, 3);
    }

    /// <summary>
    /// <b>A goal on the link a body is already committed to is still a search.</b> A link runs one way, so
    /// a destination behind the body is round the block and down this link again — and the goal link,
    /// settled cheaply the moment the search starts, has to stay reachable afterwards.
    /// </summary>
    [Fact]
    public void AGoalBehindTheBodyOnItsOwnLinkIsRoutedRoundTheBlock()
    {
        var block = Block(turnAroundM: 200f);
        var planner = new RoutePlanner(block.Graph);
        Span<int> route = stackalloc int[8];

        var written = planner.Plan(
            [new RouteEntry(block.South, 50f, 50f)], [new RouteGoal(block.South, 20f)], new Vector2(20f, 0f),
            null, route, out var costM, out _);

        Assert.Equal(5, written);
        Assert.Equal([block.South, block.East, block.North, block.West, block.South], route[..written].ToArray());
        Assert.Equal(370f, costM, 3);
    }

    /// <summary>
    /// The same goal in front of the body is the one thing the search must <em>not</em> go round for:
    /// both costs are real and which is cheaper is exactly what the search decides.
    /// </summary>
    [Fact]
    public void AGoalAheadOnTheSameLinkIsReachedWithoutLeavingIt()
    {
        var block = Block(turnAroundM: 200f);
        var planner = new RoutePlanner(block.Graph);
        Span<int> route = stackalloc int[8];

        var written = planner.Plan(
            [new RouteEntry(block.South, 20f, 80f)], [new RouteGoal(block.South, 50f)], new Vector2(50f, 0f),
            null, route, out var costM, out _);

        Assert.Equal(1, written);
        Assert.Equal(block.South, route[0]);
        Assert.Equal(30f, costM, 3);
    }

    /// <summary>
    /// <b>Turn prices are the substance of the graph.</b> The same two routes, with the expensive turn
    /// priced and then not, and the answer changes — a planner blind to this hands drivers routes made of
    /// the most expensive manoeuvres available.
    /// </summary>
    [Theory]
    [InlineData(100f, new[] { 0, 2, 3 })]
    [InlineData(0f, new[] { 0, 1 })]
    public void ATurnPriceDecidesBetweenTwoRoutesOfSimilarLength(float acrossM, int[] expected)
    {
        var fork = Fork(acrossM);
        var planner = new RoutePlanner(fork.Graph);
        Span<int> route = stackalloc int[8];
        Span<RouteGoal> goals = [new RouteGoal(fork.Straight, 50f), new RouteGoal(fork.Rejoin, ForkTown.DiagonalM)];

        var written = planner.Plan(
            [new RouteEntry(fork.Approach, 0f, 50f)], goals, new Vector2(100f, 0f), null, route, out _, out _);

        Assert.Equal(expected, route[..written].ToArray());
    }

    /// <summary>
    /// The one thing that changes about a network while the town runs: a way somebody gave up on, priced
    /// up and never banned — and the route comes back the moment the mark's life ends.
    /// </summary>
    [Fact]
    public void ASurchargeDivertsTheRouteAndGivingItUpRestoresIt()
    {
        var fork = Fork(acrossM: 0f);
        var planner = new RoutePlanner(fork.Graph);
        var marks = new LinkSurcharges(4);
        Span<int> route = stackalloc int[8];
        Span<RouteGoal> goals = [new RouteGoal(fork.Straight, 50f), new RouteGoal(fork.Rejoin, ForkTown.DiagonalM)];

        marks.Advance(0f);
        marks.Mark(fork.Straight, priceM: 500f, forS: 10f);
        var diverted = planner.Plan(
            [new RouteEntry(fork.Approach, 0f, 50f)], goals, new Vector2(100f, 0f), marks, route, out _, out _);
        Assert.Equal([fork.Approach, fork.Turn, fork.Rejoin], route[..diverted].ToArray());

        var marked = marks.Generation;
        marks.Advance(11f);
        Assert.NotEqual(marked, marks.Generation);

        var restored = planner.Plan(
            [new RouteEntry(fork.Approach, 0f, 50f)], goals, new Vector2(100f, 0f), marks, route, out _, out _);
        Assert.Equal([fork.Approach, fork.Straight], route[..restored].ToArray());
    }

    /// <summary>A goal on another piece of the network is refused rather than answered with a route that does not reach it.</summary>
    [Fact]
    public void AGoalOnAnotherPieceOfTheNetworkIsRefused()
    {
        var builder = new TravelGraph.Builder();
        var here = builder.AddNode(Vector2.Zero);
        var alsoHere = builder.AddNode(new Vector2(50f, 0f));
        var away = builder.AddNode(new Vector2(900f, 0f));
        var farther = builder.AddNode(new Vector2(950f, 0f));
        var near = builder.AddLink(here, alsoHere, 50f);
        var far = builder.AddLink(away, farther, 50f);

        var planner = new RoutePlanner(builder.Build(new FreeTurns()));
        Span<int> route = stackalloc int[8];

        var written = planner.Plan(
            [new RouteEntry(near, 0f, 50f)], [new RouteGoal(far, 10f)], new Vector2(910f, 0f), null, route,
            out var costM, out var goalSlot);

        Assert.Equal(0, written);
        Assert.Equal(-1, goalSlot);
        Assert.Equal(float.PositiveInfinity, costM);
    }

    /// <summary>
    /// A chain that will not fit is refused outright rather than truncated: half a route is a body driven
    /// somewhere nobody chose.
    /// </summary>
    [Fact]
    public void ARouteTooLongForTheCallersBufferIsRefused()
    {
        var block = Block(turnAroundM: 200f);
        var planner = new RoutePlanner(block.Graph);
        Span<int> route = stackalloc int[3];

        var written = planner.Plan(
            [new RouteEntry(block.South, 50f, 50f)], [new RouteGoal(block.South, 20f)], new Vector2(20f, 0f),
            null, route, out _, out var goalSlot);

        Assert.Equal(0, written);
        Assert.Equal(-1, goalSlot);
    }

    /// <summary>Rule 2: a search over a laid network allocates nothing, however many of them are run.</summary>
    [Fact]
    public void PlanningAllocatesNothing()
    {
        var block = Block(turnAroundM: 200f);
        var planner = new RoutePlanner(block.Graph);
        var route = new int[8];
        var entries = new[] { new RouteEntry(block.South, 50f, 50f) };
        var goals = new[] { new RouteGoal(block.South, 20f) };
        var goalPointM = new Vector2(20f, 0f);

        for (var warmUp = 0; warmUp < 32; warmUp++)
        {
            planner.Plan(entries, goals, goalPointM, null, route, out _, out _);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var search = 0; search < 500; search++)
        {
            planner.Plan(entries, goals, goalPointM, null, route, out _, out _);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>A graph where nothing costs anything to turn out of, which is what the walking side is.</summary>
    readonly struct FreeTurns : ITurnPricer
    {
        public float PriceM(int fromLink, int toLink) => 0f;
    }

    /// <summary>A price on named pairs and nothing on the rest.</summary>
    readonly struct PricedTurns(Dictionary<(int From, int To), float> priceM) : ITurnPricer
    {
        public float PriceM(int fromLink, int toLink) => priceM.GetValueOrDefault((fromLink, toLink), 0f);
    }

    /// <summary>Two nodes, one stretch each way, and one link arriving at the first of them.</summary>
    readonly record struct LineTown(TravelGraph Graph, int IntoStart, int Out);

    static LineTown Line()
    {
        var builder = new TravelGraph.Builder();
        var behind = builder.AddNode(new Vector2(-50f, 0f));
        var start = builder.AddNode(Vector2.Zero);
        var end = builder.AddNode(new Vector2(100f, 0f));
        var into = builder.AddLink(behind, start, 50f);
        var onward = builder.AddLink(start, end, 100f);
        builder.AddLink(end, start, 100f);

        return new LineTown(builder.Build(new FreeTurns()), into, onward);
    }

    /// <summary>A hundred-metre block, one way round, with the way back priced as a turn-around.</summary>
    readonly record struct BlockTown(TravelGraph Graph, int South, int East, int North, int West);

    static BlockTown Block(float turnAroundM)
    {
        var builder = new TravelGraph.Builder();
        var southWest = builder.AddNode(Vector2.Zero);
        var southEast = builder.AddNode(new Vector2(100f, 0f));
        var northEast = builder.AddNode(new Vector2(100f, 100f));
        var northWest = builder.AddNode(new Vector2(0f, 100f));

        var south = builder.AddLink(southWest, southEast, 100f);
        var back = builder.AddLink(southEast, southWest, 100f);
        var east = builder.AddLink(southEast, northEast, 100f);
        var north = builder.AddLink(northEast, northWest, 100f);
        var west = builder.AddLink(northWest, southWest, 100f);

        var prices = new Dictionary<(int, int), float>
        {
            [(south, back)] = turnAroundM,
            [(back, south)] = turnAroundM,
        };

        return new BlockTown(builder.Build(new PricedTurns(prices)), south, east, north, west);
    }

    /// <summary>An approach that may go straight on or turn off and rejoin, with the straight-on priced.</summary>
    readonly record struct ForkTown(TravelGraph Graph, int Approach, int Straight, int Turn, int Rejoin)
    {
        public const float DiagonalM = 70.71068f;
    }

    static ForkTown Fork(float acrossM)
    {
        var builder = new TravelGraph.Builder();
        var start = builder.AddNode(Vector2.Zero);
        var middle = builder.AddNode(new Vector2(50f, 0f));
        var end = builder.AddNode(new Vector2(100f, 0f));
        var off = builder.AddNode(new Vector2(50f, 50f));

        var approach = builder.AddLink(start, middle, 50f);
        var straight = builder.AddLink(middle, end, 50f);
        var turn = builder.AddLink(middle, off, 50f);
        var rejoin = builder.AddLink(off, end, ForkTown.DiagonalM);

        var prices = new Dictionary<(int, int), float> { [(approach, straight)] = acrossM };
        return new ForkTown(builder.Build(new PricedTurns(prices)), approach, straight, turn, rejoin);
    }
}
