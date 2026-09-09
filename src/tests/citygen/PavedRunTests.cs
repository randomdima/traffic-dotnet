using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>The runs the pavement is cut into.</b> The band is everything within half a walk of the line the
/// town's outline was cut at (TER-3c.3), so what a run is decides the ground answered.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class PavedRunTests
{
    public static TheoryData<string> Maps => Towns.EveryMapWithAFootway();

    /// <summary>
    /// <b>No run is shorter than its own two ends can be told apart</b> (<see cref="Kerbs.OnePlaceM"/>).
    /// The outline is cut where nothing stands nearer than the offset, asked with a rounding's grace — and
    /// a wrapping line that grazes another runs that far past the point they cross before the grace runs
    /// out. Kept, each of those spans is a run whose two ends are one place: a walk-wide round of pavement
    /// struck off a few centimetres of line, answered as ground and drawn as a circle standing in the
    /// middle of the band.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoRunIsShorterThanItsOwnEndsStandApart(string map)
    {
        var paving = Towns.Of(map).Paving(SimConfig.Shipped());

        foreach (var run in paving.Walk)
        {
            Assert.True(
                run.LengthM > Kerbs.OnePlaceM,
                $"{map}: a run of {run.LengthM:F3} m stands at {run.Line[0].StartM}");
        }
    }
}
