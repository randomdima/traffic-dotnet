using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.World.Rail;
using Xunit;

namespace TrafficSimulation.Tests.World.Rail;

/// <summary>
/// <b>The railway as a network</b> (RAIL-2, RAIL-9, TRN-9): a main line with a branch ending on it, the switch inferred
/// where the branch meets it, and a train's lanes and route through it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class RailGraphTests
{
    static readonly Vector2 WorldSizeM = new(1000f, 1000f);

    /// <summary>
    /// A main line run both ways across the map at y = 500, and a branch run both ways from a point on it at x = 400 off
    /// past the map's top-right corner, leaving it the way trains run east.
    /// </summary>
    static CityPlan.RailArrays Fork(TownMap.TrackRuns branchRuns = TownMap.TrackRuns.Both) => new()
    {
        Line = [0, 0],
        Runs = [TownMap.TrackRuns.Both, branchRuns],
        PointOffsets = [0, 2, 5],
        PointM = [new(-600f, 500f), new(1600f, 500f), new(400f, 500f), new(600f, 520f), new(1600f, 1600f)],
        Level = new byte[5],
        Ballast = CityPlan.RingArrays.None,
    };

    [Fact]
    public void ABranchEndingOnALineCutsItAndStandsASwitchThere()
    {
        var graph = RailGraph.Of(Fork(), WorldSizeM);

        Assert.Equal(3, graph.SegmentCount);
        Assert.Equal(1, graph.SwitchCount);
        var node = graph.NodeOfSwitch[0];
        Assert.True(Vector2.Distance(new Vector2(400f, 500f), graph.NodeM[node]) <= RailGraph.MeetM, $"the switch stands at {graph.NodeM[node]}");

        // The common end is the main line's west segment: the one leading away from both the others.
        var common = graph.Ends[graph.EndOffsets[node]];
        Assert.True(graph.Segments[common / 2].PointM[0].X < 0f, "the switch's common end is not the west of the main line");
        Assert.Equal(3, graph.PortalCount());
    }

    /// <summary>A wheel running into the switch from its common end takes the branch it is set to; out of either branch, the common end.</summary>
    [Fact]
    public void AWheelFacingTheSwitchTakesItsSettingAndOneTrailingTakesTheCommonEnd()
    {
        var graph = RailGraph.Of(Fork(), WorldSizeM);
        var node = graph.NodeOfSwitch[0];
        var common = graph.Ends[graph.EndOffsets[node]];
        var first = graph.Ends[graph.EndOffsets[node] + 1];
        var second = graph.Ends[graph.EndOffsets[node] + 2];

        Assert.Equal(first, graph.NextEnd(common, [0]));
        Assert.Equal(second, graph.NextEnd(common, [1]));
        foreach (byte position in (ReadOnlySpan<byte>)[0, 1])
        {
            Assert.Equal(common, graph.NextEnd(first, [position]));
            Assert.Equal(common, graph.NextEnd(second, [position]));
        }
    }

    /// <summary>A train's leg from the west portal to the branch's runs through the switch, and the switch is named on the way.</summary>
    [Fact]
    public void ALegRunsFromAPortalThroughTheSwitchToAnother()
    {
        var graph = RailGraph.Of(Fork(), WorldSizeM);
        var westNode = Enumerable.Range(0, graph.NodeCount).Single(node => graph.IsPortal[node] && graph.NodeM[node].X < 0f);
        var branchNode = Enumerable.Range(0, graph.NodeCount).Single(node => graph.IsPortal[node] && graph.NodeM[node].Y > 1000f);
        var entry = Enumerable.Range(0, graph.LaneCount).Single(lane => graph.NodeBehind(lane) == westNode);

        Span<int> leg = stackalloc int[8];
        var count = graph.Route(entry, branchNode, leg);

        Assert.Equal(2, count);
        Assert.Equal(graph.NodeOfSwitch[0], graph.NodeAhead(leg[0]));
        Assert.Equal(branchNode, graph.NodeAhead(leg[1]));
    }

    /// <summary>A branch run one way alone offers no leg against it: nothing reverses, and nothing runs a lane it has none of.</summary>
    [Fact]
    public void NoLegRunsAgainstAOneWayBranch()
    {
        var graph = RailGraph.Of(Fork(TownMap.TrackRuns.Backward), WorldSizeM);
        var westNode = Enumerable.Range(0, graph.NodeCount).Single(node => graph.IsPortal[node] && graph.NodeM[node].X < 0f);
        var branchNode = Enumerable.Range(0, graph.NodeCount).Single(node => graph.IsPortal[node] && graph.NodeM[node].Y > 1000f);
        var entry = Enumerable.Range(0, graph.LaneCount).Single(lane => graph.NodeBehind(lane) == westNode);

        Span<int> leg = stackalloc int[8];
        Assert.Equal(0, graph.Route(entry, branchNode, leg));
    }
}

static class RailGraphCounts
{
    public static int PortalCount(this RailGraph graph) => graph.IsPortal.Count(portal => portal);
}
