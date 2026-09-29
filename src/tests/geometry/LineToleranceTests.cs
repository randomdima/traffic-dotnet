using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>A tolerance is the millimetre it says inside 8 192 m and grows past it</b> — so every town that fits
/// inside is answered exactly as it always was, and a town past it is answered to what a float can hold.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class LineToleranceTests
{
    [Fact]
    public void AMillimetreIsAMillimetreInside8192Metres()
    {
        Assert.Equal(LineTolerance.RoundingM, LineTolerance.At(LineTolerance.RoundingM, new Vector2(8191.9f, -8191.9f)));
    }

    [Fact]
    public void PastItATolerancePassesTwoOfAFloatsSteps()
    {
        foreach (var furthestM in (ReadOnlySpan<float>)[8192f, 16384f, 29999f])
        {
            var stepM = MathF.BitIncrement(furthestM) - furthestM;
            Assert.True(LineTolerance.At(LineTolerance.RoundingM, new Vector2(0f, furthestM)) >= 2f * stepM);
        }
    }
}
