using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary><b>A rectangle turned to any heading</b> (<see cref="OrientedBox"/>), asked of a strip along +x.</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class OrientedBoxTests
{
    /// <summary>A strip a metre either side of the x axis.</summary>
    const float StripHalfWidthM = 1f;

    /// <summary>
    /// <b>A box turned across a strip covers as far along it as the part of it over the strip</b>: a metre wide, four long,
    /// turned a half quarter about (2, 2), its near end's corners over the strip at x 0.232 and 0.939 and its long sides
    /// crossing the strip's far edge at x 0.293 and 1.707 — and not out to its far end's 3.768, which is off it.
    /// </summary>
    [Fact]
    public void ABoxAcrossAStripCoversOnlyThePartOverIt()
    {
        var box = new OrientedBox(new Vector2(2f, 2f), Vector2.Normalize(Vector2.One), HalfLengthM: 2f, HalfWidthM: 0.5f);

        Assert.True(box.CoversAlong(Vector2.Zero, Vector2.UnitX, StripHalfWidthM, out var fromM, out var toM));
        Assert.Equal(0.232f, fromM, 3);
        Assert.Equal(1.707f, toM, 3);
    }

    /// <summary><b>And a box clear of the strip covers none of it.</b></summary>
    [Fact]
    public void ABoxClearOfAStripCoversNone()
    {
        var box = new OrientedBox(new Vector2(0f, 3f), Vector2.UnitX, HalfLengthM: 2f, HalfWidthM: 1f);

        Assert.False(box.CoversAlong(Vector2.Zero, Vector2.UnitX, StripHalfWidthM, out _, out _));
    }
}
