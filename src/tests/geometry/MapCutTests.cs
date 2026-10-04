using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>A shell's line cut to the map</b> (<see cref="MapCut"/>, GEN-2b): what stands on the map kept, what stands off it
/// dropped, and a ring running off it closed along the edge — the ground still on every ring's right — each asked of a
/// map 100 m square and rings laid by hand.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class MapCutTests
{
    static readonly Vector2 MapM = new(100f, 100f);

    [Fact]
    public void ARingOnTheMapIsKeptAsItIs()
    {
        Vector2[] ring = [new(10, 10), new(20, 10), new(20, 20), new(10, 20)];

        var cut = MapCut.Rings([ring], MapM);

        Assert.Equal([ring], cut);
    }

    [Fact]
    public void ARingOffTheMapIsDropped()
    {
        Assert.Empty(MapCut.Rings([[new(-30, 10), new(-20, 10), new(-20, 20), new(-30, 20)]], MapM));
    }

    /// <summary>
    /// <b>A ring across the edge is closed along it</b>: a square standing half off the map's west edge comes back as its
    /// half on the map, the same way round.
    /// </summary>
    [Fact]
    public void ARingAcrossTheEdgeIsClosedAlongIt()
    {
        var cut = MapCut.Rings([[new(-10, 10), new(10, 10), new(10, 20), new(-10, 20)]], MapM);

        var ring = Assert.Single(cut);
        Assert.Equal(100f, AreaM2(ring));
        Assert.All(ring, pointM => Assert.InRange(pointM.X, 0f, 10f));
    }

    /// <summary>
    /// <b>A shell joined only off the map is two pieces on it</b>: two bars reaching off the west edge, joined by a third
    /// off the map, come back as each bar's part on the map.
    /// </summary>
    [Fact]
    public void AShellJoinedOffTheMapIsTwoPiecesOnIt()
    {
        Vector2[] ring = [new(-10, 10), new(20, 10), new(20, 20), new(-5, 20), new(-5, 30), new(20, 30), new(20, 40), new(-10, 40)];

        var cut = MapCut.Rings([ring], MapM);

        Assert.Equal([200f, 200f], cut.Select(AreaM2));
    }

    /// <summary><b>A ring round the corner keeps the corner</b>: a square over the map's north-west corner is the quarter of it on the map.</summary>
    [Fact]
    public void ARingRoundTheCornerKeepsTheCorner()
    {
        var cut = MapCut.Rings([[new(-10, -10), new(10, -10), new(10, 10), new(-10, 10)]], MapM);

        Assert.Equal(100f, AreaM2(Assert.Single(cut)));
    }

    /// <summary><b>The pieces the cut laid along the edge are the ones said to lie along it</b>, and no other.</summary>
    [Fact]
    public void ThePiecesAlongTheEdgeAreTheCutsOwn()
    {
        var ring = Assert.Single(MapCut.Rings([[new(-10, 10), new(10, 10), new(10, 20), new(-10, 20)]], MapM));

        var along = Enumerable.Range(0, ring.Length).Where(at => MapCut.AlongTheEdge(ring[at], ring[(at + 1) % ring.Length], MapM));
        Assert.Equal([new Vector2(0, 20)], along.Select(at => ring[at]));
    }

    static float AreaM2(Vector2[] ring)
    {
        var twice = 0f;
        for (int at = 0, before = ring.Length - 1; at < ring.Length; before = at++) twice += (ring[before].X * ring[at].Y) - (ring[at].X * ring[before].Y);
        return twice * 0.5f;
    }
}
