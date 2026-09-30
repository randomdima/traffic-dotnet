using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>What a shipped city is asked, when somebody asks it</b> — the shallow bar every town is held to, that
/// it can be driven round, and that a minute of it leaves nobody stuck inside anybody.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the whole of what the suite has to say about a city, and it is not part of the suite</b>
/// (<see cref="Tier.Maps"/>). A city is a brief, a seed and whatever the generator made of them: the
/// generator's own properties are asked over four seeds of a town the suite lays
/// (<see cref="GeneratorTests"/>), and a build may ship any number of cities at any number of seeds without
/// that being a change to this engine. What it is for is the moment a city is added or its brief retuned —
/// <c>qq tests maps</c>, deliberately, before it ships.
/// </para>
/// <para>
/// <b>It is the bar and not a second opinion.</b> Every assertion here is <see cref="Conformance"/>'s, the
/// same machine the town tier holds the maps this build lays to, so a city cannot pass one reading and fail
/// the other.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Maps)]
[Trait(Priority.Key, Priority.P3)]
public class ShippedCityTests
{
    public static TheoryData<string> Cities => Towns.EveryShippedCity();

    [Theory]
    [MemberData(nameof(Cities))]
    public void ACityCanBeDrivenRound(string map) => Conformance.ACityCanBeDrivenRound(map);

    [Theory]
    [MemberData(nameof(Cities))]
    public void ItsJunctionsAreJunctions(string map) => Conformance.ItsJunctionsAreJunctions(map);

    [Theory]
    [MemberData(nameof(Cities))]
    public void ALitJunctionIsStaggeredInsideItsOwnCycle(string map) =>
        Conformance.ALitJunctionIsStaggeredInsideItsOwnCycle(map);

    [Theory]
    [MemberData(nameof(Cities))]
    public void ItIsFurnished(string map) => Conformance.ItIsFurnished(map);

    [Theory]
    [MemberData(nameof(Cities))]
    public void NothingIsLaidOnItsWater(string map) => Conformance.NothingIsLaidOnItsWater(map);

    [Theory]
    [MemberData(nameof(Cities))]
    public void NothingItCarriesStandsOffIt(string map) => Conformance.NothingItCarriesStandsOffIt(map);

    [Theory]
    [MemberData(nameof(Cities))]
    public void ItsBoundaryCloses(string map) => Conformance.ItsBoundaryCloses(map);

    /// <summary>
    /// A minute of the city, against the claim the town itself keeps (PHY-1). <b>A city nobody can drive
    /// through without piling up is a city that was authored wrong</b>, which is exactly the finding this
    /// tier exists to make — and it is the same watch <c>--bench soak</c> and the panel read, so a run and a
    /// gate cannot disagree about what being stuck is.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cities))]
    public void AMinuteOfItLeavesNobodyInsideAnybody(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        var loop = new SimLoop<TownWorld>(world, config);

        var watch = new TownWatch(world, config);
        for (var tick = 0; tick < 3_600; tick++)
        {
            loop.Advance();
            watch.Saw(world);
        }

        Assert.InRange(watch.LongestStuckTicks, 0, SoakProbe.StuckAfterTicks);
        Assert.Equal(ClaimVerdict.Kept, watch.Verdict(TownWatch.NothingInsideAnything));
    }
}
