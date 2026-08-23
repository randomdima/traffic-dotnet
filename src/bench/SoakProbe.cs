using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

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

    public static void Run(SimConfig config)
    {
        // Nothing here is a time, so a cold process would not make a figure wrong — it would make sixty
        // seconds of four towns take several minutes of somebody's, which is its own kind of untrue.
        Warmup.TheProcess(config);
        Console.WriteLine(
            $"soak probe — {WarmupTicks} warm-up ticks, {MeasuredTicks} measured ({MeasuredTicks / config.Sim.TickRateHz} s), " +
            $"{config.Solver.VelocityIterations} velocity and {config.Solver.PositionIterations} position iterations");
        Console.WriteLine(
            $"{"map",-10}{"walkers",9}{"cars",6}{"dead",6}{"wrecked",9}{"walks done",12}{"gave up",9}" +
            $"{"drives done",13}{"touches",9}{"peak mm",10}{"peak body",12}{"stuck ticks",13}{"stuck body",12}");

        foreach (var map in ProjectPaths.ShippedMaps())
        {
            var sample = Sample(map, config);
            Console.WriteLine(
                $"{map,-10}{sample.Walkers,9}{sample.Cars,6}{sample.Dead,6}{sample.Wrecked,9}{sample.WalksDone,12}" +
                $"{sample.WalksGivenUp,9}{sample.DrivesDone,13}{sample.Touches,9}" +
                $"{sample.DeepestOverlapM * 1_000f,10:F1}{sample.DeepestBody,12}" +
                $"{sample.LongestStuckTicks,13}{sample.StuckBody,12}");
        }

        Console.WriteLine(
            $"PHY-1 is kept while no one body stays more than {OverlapAllowanceM * 1_000f:F0} mm inside another for " +
            $"{StuckAfterTicks} ticks: a peak is one tick's approach, and a long run of them is a body nothing pushed back out.");
    }

    /// <param name="LongestStuckTicks">
    /// The longest any single body stayed deeper than the allowance — the figure that separates a solver
    /// recovering from an approach from a body left inside another one.
    /// </param>
    /// <param name="StuckBody">Which body that was, so the finding has somewhere to be looked at.</param>
    /// <param name="DrivesDone">
    /// How many cars came to rest in the bay they were driven to, which is what a drive leg ending
    /// looks like now that a car's destination is its driver's (CAR-8).
    /// </param>
    /// <param name="WalksDone">
    /// How many walks and drives ended where they were going. <b>The other half of the casualty
    /// columns</b>: a town where nothing arrives anywhere overlaps by nothing at all and kills nobody,
    /// and would pass every column to the right of these while modelling none of it.
    /// </param>
    public readonly record struct SoakSample(
        int Walkers, int Cars, int Dead, int Wrecked, long WalksDone, long WalksGivenUp, long DrivesDone, long Touches,
        float DeepestOverlapM, string DeepestBody, int LongestStuckTicks, string StuckBody);

    public static SoakSample Sample(string map, SimConfig config)
    {
        using var world = new TownWorld(TownReader.ReadFile(ProjectPaths.TownFile(map)), config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(WarmupTicks);

        var walksBefore = world.WalkArrivals;
        var gaveUpBefore = world.WalksGivenUp;
        var drivesBefore = world.BaysParkedIn;
        var bodies = world.People.Count + world.Cars.Count;
        var overlapM = new float[bodies];
        var stuckForTicks = new int[bodies];
        var deepestM = 0f;
        var deepestBody = -1;
        var longestStuckTicks = 0;
        var stuckBody = -1;

        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance();
            SweepOverlaps(world, overlapM);
            for (var body = 0; body < bodies; body++)
            {
                // Which body it was and not only how deep: a walker a car swept and a car that drove into
                // another are the same millimetres and different findings.
                if (overlapM[body] > deepestM)
                {
                    deepestM = overlapM[body];
                    deepestBody = body;
                }

                stuckForTicks[body] = overlapM[body] > OverlapAllowanceM ? stuckForTicks[body] + 1 : 0;
                if (stuckForTicks[body] <= longestStuckTicks) continue;

                longestStuckTicks = stuckForTicks[body];
                stuckBody = body;
            }
        }

        var dead = 0;
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.Dead[person]) dead++;
        }

        var wrecked = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Broken[car]) wrecked++;
        }

        return new SoakSample(
            world.People.Count, world.Cars.Count, dead, wrecked, world.WalkArrivals - walksBefore,
            world.WalksGivenUp - gaveUpBefore, world.BaysParkedIn - drivesBefore, world.Touches, deepestM,
            Named(world, deepestBody), longestStuckTicks, Named(world, stuckBody));
    }

    /// <summary>What a body's place in the sweep is called, which is the roster it falls in and its own index there.</summary>
    static string Named(TownWorld world, int body) =>
        body < 0 ? "—"
        : body < world.People.Count ? $"walker {body}"
        : $"car {body - world.People.Count}";

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
