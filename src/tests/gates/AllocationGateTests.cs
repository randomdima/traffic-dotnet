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
/// a figure nobody can quote. The gate is taken on the loop alone, on the query the tick leans on
/// hardest — the classifier, asked across a whole city — and on the suite's own towns standing
/// (<see cref="Towns.EveryMapWorthAGate"/>).
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
        // work is per part of the town, so a warm-up that crosses the map once leaves whole squares of it
        // cold and the figure short of zero by the ground being reached for the first time, not by the
        // query allocating.
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
                sum += (int)ground.At(new Vector2(step.X * (probe % 1_000), step.Y * (probe % 997))).Rules;
            }

            return sum;
        }
    }

    /// <summary>
    /// <b>A frame's scenery is made into instances in the buffer it is handed and nowhere else</b>
    /// (<see cref="App.Render.ScenerySprites"/>): a view inside the budget and one thinned to it, panned across the
    /// town, without a byte of the heap.
    /// </summary>
    [Fact]
    public void FillingAFramesSceneryAllocatesNothing()
    {
        var scenery = Render.ScenerySpriteTests.Lattice(100);
        var sprites = Render.ScenerySpriteTests.Lay(scenery, mostDrawn: 1_000);
        var into = new App.Render.SpriteInstance[1_000];
        var drawn = Pan();

        var before = GC.GetAllocatedBytesForCurrentThread();
        drawn += Pan();

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.True(drawn > 0);

        int Pan()
        {
            var sum = 0;
            for (var step = 0; step < 100; step++)
            {
                sum += sprites.Fill(new Vector2(step * 10f, 500f), new Vector2(200f), into);
                sum += sprites.Fill(new Vector2(step * 10f, 500f), new Vector2(2_000f), into);
            }

            return sum;
        }
    }

    /// <summary>
    /// <b>And the interface a window draws every frame</b>, as it opens with nothing toggled: the tick's gates
    /// never draw it, and at a few thousand frames a second a few hundred bytes a frame is megabytes a second.
    /// </summary>
    [Fact]
    public void DrawingTheInterfaceAllocatesNothing()
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(Towns.Fixture), config);
        var uiPx = new Vector2(1600f, 900f);
        var ui = new App.Hud.Interface(config.Trim);
        var quads = new App.Screen.OverlayQuad[App.Render.TownRenderer.OverlayCapacity];
        var ground = new App.Screen.OverlayQuad[App.Render.TownRenderer.UnderlayCapacity];
        var groundAbove = new App.Screen.OverlayQuad[App.Render.TownRenderer.UnderlayAboveCapacity];
        var frame = new App.Hud.InterfaceFrame
        {
            World = world,
            Ground = App.Render.GroundMesh.Build(world.Plan, config),
            Config = config,
            Camera = new App.Camera.Camera2D(config, world.Plan.WorldSizeM, uiPx),
            UiPx = uiPx,
            PointerPx = uiPx * 0.5f,
            MapName = world.Plan.Name,
        };

        // Warmed until tier 1, so a recompilation is not charged to the measured passes.
        for (var pass = 0; pass < 256; pass++) ui.Draw(quads, ground, groundAbove, frame, out _, out _);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 100; pass++) ui.Draw(quads, ground, groundAbove, frame, out _, out _);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    /// <summary>
    /// The rule, asked of the thing it is actually about: a standing town, on each of the suite's own.
    /// <b>Zero, and not nearly zero</b> — the phases this engine wrote touch no allocator at all.
    /// </summary>
    /// <remarks>
    /// This gate runs phases 1–3 by hand and steps nothing, so it cannot reach the solver: a narrower
    /// question than <see cref="AWholeTickOfAWholeTownAllocatesNothing"/> below, which asks the whole rule
    /// of the whole tick. Both are kept: a gate that fails should name which half it was.
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
    /// Rule 2 asked of the whole tick — the five phases and the solver's step together, on each of the
    /// suite's own towns.
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
        // town whose traffic piles up does not have its worst pile-up in the first ten seconds and a town
        // whose traffic disperses reaches its in a minute.
        //
        // **A failure here of a few hundred bytes is that and not a leak** — a leak allocates every tick and
        // grows with the window, so the two are told apart by lengthening the warm-up rather than by reading
        // the figure. What would retire the chase is pre-sizing the arrays that grow.
        loop.Advance(Towns.SettleTicks(map));

        // The world steps on a thread of its own (ManagedWorld), and what that thread allocates is counted with this one's.
        var before = GC.GetAllocatedBytesForCurrentThread() + world.AllocatedAsideBytes;
        loop.Advance(600);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread() + world.AllocatedAsideBytes);
    }

    /// <summary>
    /// <b>And with a train crossing the town</b> (TRN-7, RAIL-1): no map the gates above are asked of carries a railway, so
    /// this one lays the suite's city with two tracks and a train sent at once and every few seconds after, and measures
    /// from the moment a crossing first closes — the wagons, the crossings' holds and their gear all running.
    /// </summary>
    [Fact]
    public void AWholeTickWithATrainCrossingTheTownAllocatesNothing()
    {
        var config = new SimConfig { Train = new TrainFigures { FirstLeastS = 1f, FirstMostS = 1f, GapLeastS = 5f, GapMostS = 5f } };
        var brief = Towns.Brief(Towns.CitySeed, buildings: Towns.BuildingsBuilt, people: Towns.PeopleBuilt, railTracks: 2);
        using var world = new TownWorld(Towns.LayFresh(brief), config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(Towns.SettleTicks(Towns.City));
        for (var tick = 0; tick < Towns.SettleTicks(Towns.City) && world.CrossingStretchesHeld == 0; tick++) loop.Advance();

        var before = GC.GetAllocatedBytesForCurrentThread() + world.AllocatedAsideBytes;
        loop.Advance(600);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread() + world.AllocatedAsideBytes);
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
    /// <b>Asked of the city with enough traffic on it to crowd, which is a fact about the town rather than
    /// about the arbiter.</b> What answers this is a town whose own traffic queues into itself, and <b>how many
    /// cars that takes is a reading of the town and moves when the town does</b>: the shared town's sixty touch
    /// nothing over five minutes, and at <see cref="CrowdedCars"/> the first touch is between the fourth minute
    /// and the fifth.
    /// </remarks>
    [Fact]
    public void ATownWithTrafficInItActuallyProducesContacts()
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.LayFresh(Towns.Brief(Towns.CitySeed, cars: CrowdedCars)), config);
        var loop = new SimLoop<TownWorld>(world, config);

        // Stopped at the first touch: the count only ever grows, so the ticks after it cannot change the answer.
        for (var tick = 0; tick < 21_600 && world.Touches == 0; tick++) loop.Advance();

        Assert.True(world.Touches > 0);
    }

    /// <summary>
    /// How many cars it takes to crowd the suite's own town. <b>Not a figure anything behaves on</b>: it is
    /// how much traffic this one question needs to have something to ask about, and the reading it is
    /// chosen against is the one in the remark above.
    /// </summary>
    /// <remarks>
    /// <b>It is an ask and the town is the bound</b>: a car is stood on a lane and no lane holds two
    /// (<c>SpawnStage</c>), so what this really says is <em>every lane the town affords</em> — which is how a
    /// figure chosen against one arrangement of the generator goes on meaning the same thing under the next.
    /// </remarks>
    const int CrowdedCars = 800;
}
