using System.Numerics;
using TrafficSimulation.Multiplayer.Wire;
using Xunit;

namespace TrafficSimulation.Tests.Multiplayer.Wire;

/// <summary><b>A figure is sent to its quantum</b>, and a heading the whole way round is the same heading.</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class WireShapeTests
{
    static readonly WireShape Shape = new(0.01f, 0.1f, 0.5f, new Vector2(1_000f, 400f), new MoverIds(10, 10, 0), 8f);

    [Fact]
    public void APositionComesBackWithinHalfItsQuantum()
    {
        const float at = 123.456_7f;

        Assert.InRange(Shape.FromPosition(Shape.ToPosition(at)), at - 0.005f, at + 0.005f);
    }

    [Fact]
    public void APositionOffTheTownIsSentAtItsEdge()
    {
        Assert.Equal(0, Shape.ToPosition(-3f));
        Assert.Equal(Shape.MostPosition, Shape.ToPosition(1_000.5f));
    }

    [Fact]
    public void AHeadingAWholeTurnRoundIsTheSameHeading()
    {
        Assert.Equal(Shape.ToHeading(0.5f), Shape.ToHeading(0.5f + 2f * MathF.PI));
        Assert.Equal(Shape.ToHeading(2f * MathF.PI - 0.5f), Shape.ToHeading(-0.5f));
    }

    [Fact]
    public void ASmallAnglePastARightAngleIsSentAtIt()
    {
        Assert.Equal(Shape.MostSmallAngle, Shape.ToSmallAngle(MathF.PI));
    }
}
