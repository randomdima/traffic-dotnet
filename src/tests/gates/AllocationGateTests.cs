using System.Numerics;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Terrain;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Gates;

/// <summary>
/// Rule 2, as a test rather than a habit: the steady state allocates <em>nothing</em>. Not little.
/// </summary>
/// <remarks>
/// A GC pause is a measurement destroyed — the project's performance doctrine insists on CPU time,
/// a priced instrument and a warm-up counted in ticks, and a collection landing inside the sample is
/// a figure nobody can quote. The gate is re-taken on the largest town each milestone can open; at
/// M0 the largest town was no town, so what it held was the loop itself. At M1 the town is open as
/// data and nothing is stood up from it, so what is added is the query the tick will lean on
/// hardest — the classifier, asked across a whole city.
/// </remarks>
[Trait(Tier.Key, Tier.Perf)]
[Trait(Priority.Key, Priority.P0)]
[Collection(Simulation.SolverCollection.Name)]
public class AllocationGateTests
{
    const int Ticks = 1_000;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1_000)]
    public void ATickOfAStandingTownAllocatesNothing(int agents)
    {
        var perTick = TickProbe.AllocatedBytesPerTick(SimConfig.Shipped(), agents, Ticks);

        Assert.Equal(0d, perTick);
    }

    /// <summary>
    /// The terrain query is what every walking body and every driving body asks of the world several
    /// times a tick, and it is asked here over a whole town. A sample struct returned by value is the whole
    /// point: one that boxed, or that handed back a class, would put a hundred thousand allocations a
    /// second under the tick.
    /// </summary>
    [Fact]
    public void AskingTheGroundOfAWholeCityAllocatesNothing()
    {
        var plan = Towns.Of(Towns.City);
        var ground = new GroundLocator(plan, SimConfig.Shipped());
        var step = plan.WorldSizeM / 1_000f;

        // <b>Warmed on the walk that is measured and not on a diagonal of it.</b> The locator's own lazy
        // work is per part of the town, so a warm-up that crossed the map once left whole squares of it
        // cold and the figure came back a hundred and fifty bytes short of zero — which is the ground being
        // reached for the first time, not the query allocating.
        var walked = Sweep();

        var before = GC.GetAllocatedBytesForCurrentThread();
        walked += Sweep();

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.True(walked > 0f);

        float Sweep()
        {
            var sum = 0f;
            for (var probe = 0; probe < 1_000_000; probe++)
            {
                sum += ground.At(new Vector2(step.X * (probe % 1_000), step.Y * (probe % 997))).Coefficient;
            }

            return sum;
        }
    }

    /// <summary>
    /// The rule, asked of the thing it is actually about: a standing town, on every map this engine
    /// ships. <b>Zero, and not nearly zero</b> — the phases this engine wrote touch no allocator at
    /// all, on five walkers and on five hundred and twenty.
    /// </summary>
    /// <remarks>
    /// This gate runs phases 1–3 by hand and steps nothing, so it cannot reach the solver. That used to
    /// be the point — what phase 4 allocated was a package's and was not flat in the size of the town —
    /// and it is now only a narrower question than
    /// <see cref="AWholeTickOfAWholeTownAllocatesNothing"/> below, which asks the whole rule of the whole
    /// tick. Both are kept: a gate that fails should name which half it was.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void ThisEnginesOwnPhasesAllocateNothingOverAWholeTown(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        new SimLoop<TownWorld>(world, config).Advance(120);

        Assert.Equal(0d, TownProbe.OwnBytesPerTick(world, config));
    }

    /// <summary>
    /// Rule 2 asked of the whole tick — the five phases and the solver's step together, on every map this
    /// engine ships. <b>It is the claim that became true when the solver became this engine's own</b>: the
    /// figure was several hundred bytes a step for four milestones, and it was the one part of the rule
    /// nobody here could fix.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void AWholeTickOfAWholeTownAllocatesNothing(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        var loop = new SimLoop<TownWorld>(world, config);

        // Long enough for every array the tick leans on to have reached the size it stays at: a town
        // still growing its capacities is not the steady state the rule is about, and the size the contact
        // arrays settle at is the most contacts the solver has ever had at once. <b>How long that takes is
        // a fact about the map</b> and is the map's to state (<see cref="Towns.SettleTicks"/>), because a
        // lap with bodies reeling into the carriageway does not have its worst pile-up in the first ten
        // seconds and a town whose traffic disperses reaches its in a minute.
        //
        // **A failure here of a few hundred bytes is that and not a leak** — a leak allocates every tick and
        // grows with the window, so the two are told apart by lengthening the warm-up rather than by reading
        // the figure. What would retire the chase is pre-sizing the arrays that grow.
        loop.Advance(Towns.SettleTicks(map));

        var before = GC.GetAllocatedBytesForCurrentThread();
        loop.Advance(600);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    /// <summary>
    /// Phase 5, which the gate above cannot reach: it never steps, so the solver reports no contacts
    /// and the arbiter walks an empty list however long it is run for. This one steps, on the town whose
    /// cars actually queue against each other.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void ArbitratingAWholeTownsContactsAllocatesNothing(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        new SimLoop<TownWorld>(world, config).Advance(120);

        Assert.Equal(0d, TownProbe.ContactBytesPerTick(world, config, ticks: 600));
    }

    /// <summary>
    /// And the gate above has something to speak for. Zero bytes over an empty list is zero bytes, so
    /// the figure means nothing until a town has been shown to produce contacts at all.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the lap the bodies reel on and not of a town.</b> Whether a town's own traffic touches
    /// inside a minute is a fact about how crowded it is, and a fixture roomy enough for its cars to park
    /// squarely is one where nothing need collide — so the map that exists to put bodies in the carriageway
    /// is the one that can honestly answer this.
    /// </remarks>
    [Fact]
    public void ATownWithTrafficInItActuallyProducesContacts()
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(TrackPlan.NameOf(TrackLap.Drunk)), config);
        new SimLoop<TownWorld>(world, config).Advance(1_800);

        Assert.True(world.Touches > 0);
    }
}
