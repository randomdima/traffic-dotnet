using System.Numerics;
using TrafficSimulation.CityGen.Gen;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Gen;

/// <summary>
/// <b>Where the open ground grows thick and where it lies bare</b> (GEN-6g, <see cref="Thickets"/>): a field that leaves the
/// zone's share as it is when it is not stretched, and never reaches past nought or twice the share however hard it is.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class ThicketsTests
{
    static IEnumerable<Vector2> Places()
    {
        for (var y = 0f; y < 400f; y += 3.7f)
        {
            for (var x = 0f; x < 400f; x += 3.7f) yield return new(x, y);
        }
    }

    /// <summary><b>Unstretched, the field is even</b>: the ground grows everywhere at its zone's own share.</summary>
    [Fact]
    public void UnstretchedTheFieldIsEven()
    {
        var thickets = new Thickets(7, 40f, 0f);

        Assert.All(Places(), atM => Assert.Equal(1f, thickets.At(atM)));
    }

    /// <summary>
    /// <b>Stretched hard, it bares its clearings and fills its thickets</b>, and no place grows thinner than nothing or
    /// thicker than the most.
    /// </summary>
    [Fact]
    public void StretchedHardItBaresItsClearingsAndFillsItsThickets()
    {
        var thickets = new Thickets(7, 40f, 100f);
        var at = Places().Select(thickets.At).ToArray();

        Assert.All(at, thickness => Assert.InRange(thickness, 0f, Thickets.Thickest));
        Assert.Contains(0f, at);
        Assert.Contains(Thickets.Thickest, at);
    }
}
