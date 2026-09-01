using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// The tier-2 soak this milestone exits on: a whole town left to run, and then the one question PHY-1
/// asks of it — <b>is anything inside anything else</b> — beside the casualties it produced getting there.
/// </summary>
/// <remarks>
/// <para>
/// <b>Overlap is measured by the solver and not against it.</b> The figure is the deepest negative
/// separation in the manifolds the step itself acted on: a sweep written here would be a second opinion
/// about geometry the solver already holds, and it would disagree with it precisely where the answer
/// mattered.
/// </para>
/// <para>
/// <b>And it is measured per body over time, never as one maximum over the town.</b> A maximum across a
/// thousand bodies is a report on the unluckiest one of them and says nothing at all: in a city there is
/// always something, somewhere, in the tick between arriving at a body and being pushed off it. What
/// PHY-1 forbids is a body let <em>inside</em> another and left there, so what is counted is how long
/// one body stays deeper than the allowance — a solver recovering is a handful of ticks, and a body
/// sunk in is every tick until something moves.
/// </para>
/// <para>
/// The casualty columns are the reason the overlap columns can be trusted. A town where nothing ever
/// touches anything overlaps by nothing at all, and would pass this while modelling none of it.
/// </para>
/// </remarks>
internal static class SoakProbe
{
    public const int WarmupTicks = 600;

    public const int MeasuredTicks = 3_600;

    /// <summary>
    /// What a soft-step solver leaves and then recovers, rather than a threshold anybody chose: a
    /// centimetre, which is a two-hundredth of a car's width and a fiftieth of a walker's own body.
    /// </summary>
    public const float OverlapAllowanceM = 0.01f;

    /// <summary>
    /// How long a body may go on overlapping before it is stuck rather than recovering: a second. It is
    /// deliberately not a tight figure — a body actually left inside another stays there for thousands
    /// of ticks, and the deepest recovery measured on a city is twenty-seven.
    /// </summary>
    public const int StuckAfterTicks = 60;

    /// <summary>
    /// How far past its grant a body may go without it being a body entering ground it was refused: a
    /// centimetre, the same figure the overlap is read at, because both are asking whether a body is where it
    /// was told it could be.
    /// </summary>
    public const float PastTheGrantAllowanceM = 0.01f;

    /// <summary>
    /// How long a body may go on getting <em>deeper</em> into ground it was refused before it is driving on
    /// rather than stopping. <b>Longer than any stop from town speed</b>: a car whose grant is taken from
    /// under it brakes as hard as its tyres allow and still travels while it does, so the ticks it spends
    /// arriving at rest are the mechanism working and not a body ignoring it. A body still going deeper two
    /// and a half seconds later has not been braking.
    /// </summary>
    public const int PastAfterTicks = 150;

    public static bool Run(SimConfig config)
    {
        // Nothing here is a time, so a cold process would not make a figure wrong — it would make sixty
        // seconds of four towns take several minutes of somebody's, which is its own kind of untrue.
        Warmup.TheProcess(config);
        Console.WriteLine(
            $"soak probe — {WarmupTicks} warm-up ticks, {MeasuredTicks} measured ({MeasuredTicks / config.Sim.TickRateHz} s), " +
            $"{config.Solver.VelocityIterations} velocity and {config.Solver.PositionIterations} position iterations");
        Console.WriteLine(
            $"{"map",-10}{"walkers",9}{"cars",6}{"down",6}{"wrecked",9}{"walks done",12}{"gave up",9}" +
            $"{"drives done",13}{"touches",9}{"peak mm",10}{"peak body",12}{"stuck ticks",13}{"stuck body",12}" +
            $"{"past mm",10}{"drove on",10}{"drove body",12}");

        var maps = Maps.Shipped();
        var watched = new TownWatch[maps.Length];
        for (var map = 0; map < maps.Length; map++)
        {
            var sample = watched[map] = Sample(maps[map], config);
            Console.WriteLine(
                $"{maps[map],-10}{sample.Walkers,9}{sample.Cars,6}{sample.Down,6}{sample.Wrecked,9}{sample.WalksDone,12}" +
                $"{sample.WalksGivenUp,9}{sample.DrivesDone,13}{sample.Touches,9}" +
                $"{sample.DeepestOverlapM * 1_000f,10:F1}{Named(sample, sample.DeepestBody),12}" +
                $"{sample.LongestStuckTicks,13}{Named(sample, sample.StuckBody),12}" +
                $"{sample.FurthestPastTheGrantM * 1_000f,10:F0}{sample.LongestPastTicks,10}" +
                $"{Named(sample, sample.PastBody),12}");
        }

        Console.WriteLine(
            $"PHY-1 is kept while no one body stays more than {OverlapAllowanceM * 1_000f:F0} mm inside another for " +
            $"{StuckAfterTicks} ticks: a peak is one tick's approach, and a long run of them is a body nothing pushed back out.");
        Console.WriteLine(
            $"TER-4c.1 is kept while nobody goes on getting deeper into ground the book refused it for " +
            $"{PastAfterTicks} ticks: the ticks a body spends arriving at rest are the stop, and a body still " +
            "going deeper after them never braked.");

        // The same soak said as claims, one map at a time, which is what a caller reads to decide whether
        // the town kept PHY-1 rather than reading the millimetres itself.
        var kept = true;
        for (var map = 0; map < maps.Length; map++)
        {
            kept &= ScenarioReport.Print(maps[map], [watched[map]], MeasuredTicks / config.Sim.TickRateHz);
        }

        return kept;
    }

    /// <summary>
    /// One town soaked and watched. <b>The arithmetic is <see cref="TownWatch"/>'s</b> — the same watch a
    /// run of the game keeps against the map on screen — so this table and that panel cannot disagree
    /// about how deep anything got or how long it stayed there.
    /// </summary>
    public static TownWatch Sample(string map, SimConfig config)
    {
        using var world = new TownWorld(Maps.Plan(map, config, BuildingCatalog.Shared.OrdinaryFootprintsM()), config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(WarmupTicks);

        // The warm-up is not the measurement: what a town does while its people are still walking to their
        // first car is not what it does once it is running.
        var watch = new TownWatch(world);
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance();
            watch.Saw(world);
        }

        return watch;
    }

    /// <summary>What a body's place in the sweep is called, which is the roster it falls in and its own index there.</summary>
    static string Named(TownWatch watch, int body) =>
        body < 0 ? "—"
        : body < watch.Walkers ? $"walker {body}"
        : $"car {body - watch.Walkers}";

    /// <summary>
    /// How deep every dynamic body is into anything, this instant — walkers first, then cars, in the
    /// roster's own order. Static geometry is not asked: it is on the other side of every contact a
    /// dynamic body has, so asking it would be asking twice.
    /// </summary>
    public static void SweepOverlaps(TownWorld world, Span<float> into)
    {
        var physics = world.PhysicsForTrace;

        for (var person = 0; person < world.People.Count; person++)
        {
            into[person] = physics.OverlapOf(world.People.Body[person]);
        }

        for (var car = 0; car < world.Cars.Count; car++)
        {
            into[world.People.Count + car] = physics.OverlapOf(world.Cars.Body[car]);
        }
    }

    /// <summary>
    /// <b>How far past the ground it was granted every dynamic body is</b>, this instant — walkers first,
    /// then cars, in the roster's own order, and zero for a body still inside what the book gave it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the town's own answer read back, not a second measurement of it.</b> A grant is what is left
    /// of a body's ask once everything already spoken for has been taken out of it, expressed from that
    /// body's own nose — so it goes negative exactly when the nose is past the place the book stopped it, and
    /// the figure is that overshoot. Measured any other way this would be a second arithmetic free to
    /// disagree with the one the drivers are actually held to.
    /// </para>
    /// <para>
    /// <b>A body nothing cut is left out</b> rather than counted as clear: an infinite grant is an empty road,
    /// a parked car or a wreck, and none of them is a body that was refused anything.
    /// </para>
    /// </remarks>
    public static void SweepPastTheGrant(TownWorld world, Span<float> into)
    {
        for (var person = 0; person < world.People.Count; person++)
        {
            into[person] = PastM(world.People.AuthorityM[person]);
        }

        for (var car = 0; car < world.Cars.Count; car++)
        {
            into[world.People.Count + car] = PastM(world.Cars.AuthorityM[car]);
        }

        static float PastM(float grantedM) => float.IsFinite(grantedM) ? MathF.Max(0f, -grantedM) : 0f;
    }

    /// <summary>The deepest anything is into anything, this instant. A report on the unluckiest body, and useful only beside the rest.</summary>
    public static float DeepestOverlapM(TownWorld world)
    {
        Span<float> overlapM = stackalloc float[world.People.Count + world.Cars.Count];
        SweepOverlaps(world, overlapM);

        var deepestM = 0f;
        foreach (var body in overlapM) deepestM = MathF.Max(deepestM, body);

        return deepestM;
    }
}
