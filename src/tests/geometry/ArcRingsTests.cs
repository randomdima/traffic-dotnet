using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>What shutting the runs a construction left open hands back</b> (<see cref="ArcRings.Shut"/>): the
/// ring the holes were in, and nothing for a run that encloses nothing.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class ArcRingsTests
{
    /// <summary>A ten-metre square broken into two runs, each with a metre of its side missing at its end.</summary>
    [Fact]
    public void TwoRunsWithAHoleAtEachEndAreShutIntoTheOneRingTheyAre()
    {
        ArcSeg[] one = [Straight(new(0f, 0f), new(10f, 0f)), Straight(new(10f, 0f), new(10f, 9f))];
        ArcSeg[] other = [Straight(new(10f, 10f), new(0f, 10f)), Straight(new(0f, 10f), new(0f, 1f))];

        var rings = ArcRings.Shut([one, other]);

        var ring = Assert.Single(rings);
        Assert.Equal(40f, Spline.TotalLengthM(ring), 3);
    }

    [Fact]
    public void ARunThatEnclosesNothingIsNoRing()
    {
        Assert.Empty(ArcRings.Shut([[Straight(new(0f, 0f), new(3f, 0f))]]));
    }

    static ArcSeg Straight(Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        return new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), runM.Length(), 0f);
    }
}
