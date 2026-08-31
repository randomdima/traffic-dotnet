using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.Routing;

/// <summary>
/// The fine foot graph swept against the cells on every shipped map. <b>A foot edge is a nominal line and
/// nothing else in the town reads it</b>, so a line over water or through a wall is silent until a walker
/// goes into the river — which makes this sweep the only thing standing between a derivation and a town
/// that quietly walks people off the pavement.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class FootGraphTests
{
    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>One map's foot graph, built once and read by every claim about it.</summary>
    static FootGraph Of(string map) => Built.GetOrAdd(map, at => FootGraph.Build(Towns.Of(at), SimConfig.Shipped()));

    static readonly ConcurrentDictionary<string, FootGraph> Built = new();

    /// <summary>Every edge's own line stands on ground a person may stand on, sampled the whole way along it.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryStretchIsWalkedOnGroundAPersonMayStandOn(string map)
    {
        var plan = Towns.Of(map);
        var terrain = new TerrainGrid(plan, SimConfig.Shipped());
        var foot = Of(map);

        var offFoot = 0;
        var sampled = 0;
        var where = new SortedDictionary<string, (int Count, Vector2 First)>();
        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            foreach (var pointM in Along(foot, edge, plan.CellSizeM))
            {
                sampled++;
                if (terrain.At(pointM).Walkable) continue;

                offFoot++;
                var key = $"{foot.KindOf(edge)} on {terrain.GroundAt(pointM)} " +
                          $"[{foot.LengthM(edge):F1} m from {foot.AnchorM(foot.FromNode(edge))} to {foot.AnchorM(foot.ToNode(edge))}]";
                where[key] = where.TryGetValue(key, out var seen) ? (seen.Count + 1, seen.First) : (1, pointM);
            }
        }

        Assert.True(offFoot == 0, $"{map}: {offFoot} of {sampled} samples are off the pavement — {Breakdown(where)}");
    }

    /// <summary>
    /// <b>A walker enters a parking lot only to reach or leave a car parked in it</b>, and that is a fact
    /// about which edges exist rather than a price: no edge enters a lot, ever.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoStretchIsWalkedAcrossAParkingLot(string map)
    {
        var plan = Towns.Of(map);
        var terrain = new TerrainGrid(plan, SimConfig.Shipped());
        var foot = Of(map);

        var onLot = 0;
        var worst = string.Empty;
        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            foreach (var pointM in Along(foot, edge, plan.CellSizeM))
            {
                if (terrain.GroundAt(pointM) != Ground.Parking) continue;

                onLot++;
                if (worst.Length == 0) worst = $"a {foot.KindOf(edge)} stretch at {pointM}";
            }
        }

        Assert.True(onLot == 0, $"{map}: {onLot} samples stand on a lot — {worst}");
    }

    /// <summary>
    /// Every crossing the town painted is an edge of the network, because <b>a crossing is the only edge
    /// that touches a carriageway</b> — one the graph never heard of is a road nobody can lawfully cross.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryCrossingThePlanCarriesIsAnEdge(string map)
    {
        var plan = Towns.Of(map);
        var foot = Of(map);

        var crossings = 0;
        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            if (foot.KindOf(edge) == FootEdgeKind.Crossing) crossings++;
        }

        Assert.Equal(plan.Crosswalks.Count, crossings);
    }

    /// <summary>
    /// Every crossing has pavement at both ends of it. A crossing spliced onto one bank and nothing at the
    /// other is a walk onto a road and a stop, and it reads from outside as a walker changing its mind.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryCrossingHasPavementAtBothEndsOfIt(string map)
    {
        var foot = Of(map);

        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            if (foot.KindOf(edge) != FootEdgeKind.Crossing) continue;

            foreach (var end in (ReadOnlySpan<int>)[foot.FromNode(edge), foot.ToNode(edge)])
            {
                var pavements = 0;
                foreach (var leaving in foot.EdgesOut(end))
                {
                    if (foot.KindOf(leaving) != FootEdgeKind.Crossing) pavements++;
                }

                Assert.True(pavements > 0, $"{map}: a crossing at {foot.AnchorM(end)} has nothing to step off onto");
            }
        }
    }

    /// <summary>
    /// No two nodes stand at one place. A curve can end a weld's width the wrong side of its own node, and
    /// a line laid through those stations steps backwards and crosses itself.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoTwoNodesStandAtOnePlace(string map)
    {
        var foot = Of(map);
        var weldM = SimConfig.Shipped().Network.FootGraphNodeWeldM;

        for (var node = 0; node < foot.NodeCount; node++)
        {
            for (var other = node + 1; other < foot.NodeCount; other++)
            {
                var apartM = (foot.AnchorM(node) - foot.AnchorM(other)).Length();
                Assert.True(apartM > weldM, $"{map}: nodes {node} and {other} stand {apartM:F3} m apart at {foot.AnchorM(node)}");
            }
        }
    }

    /// <summary>
    /// <b>A crossing is the only stretch that stands on ground a car drives on</b>, which is the whole of
    /// what makes crossing at a crossing structural rather than priced.
    /// </summary>
    /// <remarks>
    /// Walkable ground is not the same question: a crossing's own paint is walkable, and so is a lot, so a
    /// band laid over either passes the sweep above and is still a way over a road that is not a crossing.
    /// This is the sweep that stands over anything laid between two arms of a junction, where the ground is
    /// the only thing refusing the carriageway between them.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void OnlyACrossingStandsOnGroundACarDrivesOn(string map)
    {
        var plan = Towns.Of(map);
        var terrain = new TerrainGrid(plan, SimConfig.Shipped());
        var foot = Of(map);

        var onRoad = 0;
        var sampled = 0;
        var where = new SortedDictionary<string, (int Count, Vector2 First)>();
        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            if (foot.KindOf(edge) == FootEdgeKind.Crossing) continue;

            foreach (var pointM in Along(foot, edge, plan.CellSizeM))
            {
                sampled++;
                if (!terrain.At(pointM).Drivable) continue;

                onRoad++;
                var key = $"{foot.KindOf(edge)} on {terrain.GroundAt(pointM)} " +
                          $"[{foot.LengthM(edge):F1} m from {foot.AnchorM(foot.FromNode(edge))} to {foot.AnchorM(foot.ToNode(edge))}]";
                where[key] = where.TryGetValue(key, out var seen) ? (seen.Count + 1, seen.First) : (1, pointM);
            }
        }

        Assert.True(onRoad == 0, $"{map}: {onRoad} of {sampled} samples stand on a carriageway — {Breakdown(where)}");
    }

    /// <summary>The two directions of one stretch are the same line walked the other way, station for station.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AnOutAndBackOverOneStretchIsItsOwnReverse(string map)
    {
        var foot = Of(map);

        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            var back = foot.Reverse(edge);
            Assert.Equal(foot.FromNode(edge), foot.ToNode(back));
            Assert.Equal(foot.ToNode(edge), foot.FromNode(back));
            Assert.Equal(foot.LengthM(edge), foot.LengthM(back), 3);

            var lengthM = foot.LengthM(edge);
            for (var step = 0; step <= 8; step++)
            {
                var out_ = Spline.SampleAt(foot.ArcsOf(edge), lengthM * step / 8f).PositionM;
                var home = Spline.SampleAt(foot.ArcsOf(back), lengthM * (8 - step) / 8f).PositionM;
                Assert.True((out_ - home).Length() < 0.01f, $"{map}: stretch {edge} and its reverse part by {(out_ - home).Length():F3} m");
            }
        }
    }

    /// <summary>What went wrong and where, so a sweep failure names the shape at fault rather than one point of it.</summary>
    static string Breakdown(SortedDictionary<string, (int Count, Vector2 First)> where) =>
        string.Join("; ", where.Select(row => $"{row.Value.Count}× {row.Key} (first at {row.Value.First})"));

    /// <summary>Every sample down one edge's own line, a cell apart, so nothing between two stations is missed.</summary>
    static IEnumerable<Vector2> Along(FootGraph foot, int edge, float cellSizeM)
    {
        var arcs = foot.ArcsOf(edge).ToArray();
        var lengthM = foot.LengthM(edge);
        var steps = Math.Max(2, (int)MathF.Ceiling(lengthM / cellSizeM));
        for (var step = 0; step <= steps; step++) yield return Spline.SampleAt(arcs, lengthM * step / steps).PositionM;
    }
}
