using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.Bench;
using Xunit;
using Xunit.Abstractions;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>What a car actually turns against what its steering says it must</b>, taken off the skidpad
/// (<see cref="SkidpadPlan"/>) by the instrument that measures it: every look the fleet ships, on full left
/// lock, under six pedals from a crawl to the floor in both gears.
/// </summary>
/// <remarks>
/// <para>
/// <b>The claims are <see cref="SkidpadWatch"/>'s and this only asserts them.</b> The same watch answers
/// <c>--bench skidpad</c> and the panel on a run of <c>--map Skidpad</c>, so a circle cannot pass here and
/// read as broken there.
/// </para>
/// <para>
/// <b>What is gated is the crawl and what is quoted is the rest.</b> A turn circle is geometry only while
/// the tyres are keeping up with the wheel; what a car loses at the floor is the rubber paying for the
/// acceleration, and it is a reading rather than a bound — the whole run's figures are written into the
/// test's own output below either way.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class SkidpadFiguresTests(ITestOutputHelper output)
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly SkidpadWatch Ran = SkidpadProbe.Measure(Config);

    /// <summary>
    /// <b>Every claim the pad makes about itself is kept.</b> A claim still waiting fails as loudly as a
    /// broken one: the probe chooses how long it watches, so a claim it never got round to answering is a
    /// run too short to be quoting anything.
    /// </summary>
    [Fact]
    public void EveryClaimTheSkidpadMakesIsKept()
    {
        Claims.AssertKept(Ran, output);
    }
}
