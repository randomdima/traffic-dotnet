using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// CAR-13 as the road actually meets it: <b>the habit is what moves the counts, and it moves the two it
/// names and no others</b>. Every claim here is a difference between the same lit town driven at two
/// shares, because a share is the only thing that varies between the runs and therefore the only thing
/// the difference can be about.
/// </summary>
/// <remarks>
/// <b>It is deliberately not asked as "and otherwise zero".</b> A town whose every driver keeps the rules
/// still crosses a handful of painted bars in a minute — a shunt puts a car over a line, and a car that
/// was committed when the phase turned is past it — so a claim of zero would be a claim about that
/// behaviour rather than about this one, and would fail for reasons nothing here has touched.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class RecklessDriverTests
{
    /// <summary>The map with the most lit junctions on it, which is the only kind of town that can answer this.</summary>
    const string LitTown = "Odesa";

    const int Ticks = 3_600;

    /// <summary>
    /// <b>A town of them runs more reds than a town of nobody</b> (CAR-13.1), which is the whole of what
    /// the habit does to a signal. Asked as a difference and not as a count: the count on its own is a
    /// figure about how many junctions this map lights.
    /// </summary>
    [Fact]
    public void RecklessDriversCrossMoreRedsThanDriversWhoKeepTheRules()
    {
        var keeping = Of(share: 0f);
        var reckless = Of(share: 1f);

        Assert.Equal(0, keeping.RecklessDrivers);
        Assert.True(
            reckless.RedBarCrossings > keeping.RedBarCrossings,
            $"a town of reckless drivers crossed {reckless.RedBarCrossings} painted bars and a town of "
            + $"drivers who keep the rules crossed {keeping.RedBarCrossings}");
    }

    /// <summary>
    /// <b>And at the shipped share the town has some of them</b>, so the behaviour is something a run
    /// exercises rather than something only this file has ever seen.
    /// </summary>
    [Fact]
    public void TheShippedTownHasRecklessDriversInIt() =>
        Assert.True(
            Of(SimConfig.Shipped().Driving.RecklessShare).RecklessDrivers > 0,
            $"nobody in {LitTown} was drawn reckless at the shipped share");

    /// <summary>
    /// <b>The courtesy is the only thing they drop.</b> A town of nothing but reckless drivers still gives
    /// way at a kerb no more often than never — and still stops for bodies, which is what leaves anybody
    /// alive to be given way to (CAR-13.2).
    /// </summary>
    [Fact]
    public void ATownOfRecklessDriversGivesWayToNobodyWaiting()
    {
        var run = Of(share: 1f);

        Assert.Equal(0L, run.GaveWayAtAKerb);
        Assert.True(run.KerbWaitsBegun > 0, $"nobody in {LitTown} waited at a kerb at all, so nothing was refused");
    }

    /// <summary>
    /// And the same town, driven by nobody reckless, does give way — so the count above is the habit and
    /// not a map with no uncontrolled crossing on it.
    /// </summary>
    [Fact]
    public void TheSameTownGivesWayWhenItsDriversKeepTheRules() =>
        Assert.True(
            Of(share: 0f).GaveWayAtAKerb > 0,
            $"no driver in {LitTown} gave way to somebody standing at an uncontrolled crossing");

    /// <summary>A minute of the lit town at one share of reckless drivers, taken once per share.</summary>
    static Watched Of(float share) => Runs.GetOrAdd(share, Watch);

    static readonly System.Collections.Concurrent.ConcurrentDictionary<float, Watched> Runs = new();

    static Watched Watch(float share)
    {
        var config = new SimConfig { Driving = new DrivingFigures { RecklessShare = share } };
        using var world = new TownWorld(Towns.Of(LitTown), config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(Ticks);

        return new Watched(
            world.RecklessDrivers, world.RedBarCrossings, world.GaveWayAtAKerb, world.KerbWaitsBegun);
    }

    readonly record struct Watched(int RecklessDrivers, long RedBarCrossings, long GaveWayAtAKerb, long KerbWaitsBegun);
}
