using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;

namespace TrafficSimulation.Bench;

/// <summary>
/// What a step of this engine's own solver allocates, per body count and with the contacts churning.
/// <b>Rule 2 says the steady state allocates nothing, and <c>SOL-20</c> says nothing means nothing</b> —
/// not on a standing town and not while cars are touching and separating.
/// </summary>
/// <remarks>
/// <para>
/// This is the measurement the solver is held to and it is the one the incumbent failed: a package that
/// grew a heap object per new contact slot put several hundred bytes a step under a tick that was
/// otherwise flat, which is the row the decision log carried as owed for four milestones.
/// </para>
/// <para>
/// <b>The churning half of the table is the half that matters.</b> A world whose bodies never meet
/// allocates nothing in almost any solver; what finds a growing array is a world where the contact set
/// turns over, so the tight rows pack cars into each other and keep pushing them together.
/// </para>
/// </remarks>
internal static class SolverProbe
{
    public const int WarmupSteps = 1_200;
    public const int MeasuredSteps = 900;

    /// <summary>Bodies in the world the process is warmed on — a town's worth, not a rig's.</summary>
    public const int WarmBodyCount = 1_000;

    public static void Run(SimConfig config)
    {
        Console.WriteLine(
            $"solver probe — {WarmupSteps} warm-up steps, {MeasuredSteps} measured, " +
            $"{config.Car.LengthM:F1}x{config.Car.WidthM:F1} m bodies at {config.Car.MassKg:F0} kg, " +
            $"{config.Solver.VelocityIterations} velocity and {config.Solver.PositionIterations} position iterations");
        Console.WriteLine($"{"bodies",9}{"spread",10}{"B/step",12}{"gen0",8}{"points",9}");

        WarmTheProcess(config);

        foreach (var packed in (ReadOnlySpan<bool>)[false, true])
        {
            foreach (var bodies in (ReadOnlySpan<int>)[0, 1, 100, 1_000])
            {
                var sample = Sample(config, bodies, packed);
                Console.WriteLine(
                    $"{bodies,9}{(packed ? "packed" : "apart"),10}{sample.BytesPerStep,12:F1}" +
                    $"{sample.Gen0Collections,8}{sample.ContactPoints,9}");
            }
        }

        Console.WriteLine("Rule 2 wants 0, and the contact points column is the census that says the packed " +
                          "rows were actually solving contacts rather than measuring an empty world twice.");
    }

    /// <summary>
    /// One big world stepped and thrown away, before any figure is taken. What it warms is the runtime
    /// rather than the solver: tiered compilation and dynamic PGO reach the steady state this engine
    /// measures only after the hot paths have been run at the size they will be run at.
    /// </summary>
    public static void WarmTheProcess(SimConfig config) => Sample(config, WarmBodyCount, packed: false);

    /// <param name="ContactPoints">
    /// How many points the last step solved. <b>A figure with no census is not a figure</b>: a solver that
    /// allocates nothing because it found nothing has not been measured (<c>SOL-10</c>).
    /// </param>
    public readonly record struct StepSample(double BytesPerStep, int Gen0Collections, int ContactPoints);

    public static double AllocatedBytesPerStep(SimConfig config, int bodyCount, bool packed = false) =>
        Sample(config, bodyCount, packed).BytesPerStep;

    /// <param name="packed">
    /// Whether the bodies are laid close enough to grind against each other for the whole window, which
    /// is what turns the contact set over. Apart, they are spread far enough that the figure is the
    /// solver's own and not a pile-up's.
    /// </param>
    public static StepSample Sample(SimConfig config, int bodyCount, bool packed)
    {
        var world = new PhysicsWorld(config);
        var acrossM = config.Car.LengthM * (packed ? 0.9f : 2f);
        var downM = config.Car.WidthM * (packed ? 0.9f : 4f);

        const int PerRow = 32;
        var fleet = new BodyId[bodyCount];
        var homeM = new Vector2[bodyCount];
        for (var body = 0; body < bodyCount; body++)
        {
            homeM[body] = new Vector2(body % PerRow * acrossM, body / PerRow * downM);
            fleet[body] = world.AddCar(homeM[body], 0f);
        }

        world.SettleStatics();

        var dtS = config.TickSeconds;
        for (var step = 0; step < WarmupSteps; step++) Step(world, config, fleet, homeM, step, packed, dtS);

        var gen0 = GC.CollectionCount(0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < MeasuredSteps; step++) Step(world, config, fleet, homeM, WarmupSteps + step, packed, dtS);

        return new StepSample(
            (GC.GetAllocatedBytesForCurrentThread() - before) / (double)MeasuredSteps,
            GC.CollectionCount(0) - gen0,
            world.ContactPointCount);
    }

    /// <summary>
    /// One step, with the packed rows shoved against their own neighbours so the contact set never
    /// settles. Driven by an impulse rather than placed at a velocity: nothing in this town is ever
    /// handed a motion no impulse produced.
    /// </summary>
    /// <remarks>
    /// <b>Each body is held to its own place and leaned half a car's length off it, one way and then the
    /// other</b>, so neighbours meet and part twice a second for as long as the rig is run and nothing
    /// drifts anywhere. Two rigs that seemed simpler are both wrong and both were measured to be: leaning
    /// the whole fleet at one point is a collapsing pile, and a shove that alternates with the step is a
    /// slow diffusion — in either the roster's extent grows for as long as it is run, so the arrays never
    /// reach the size they will stay at, and what the measurement finds is a world still growing rather
    /// than a steady state that allocates.
    /// </remarks>
    static void Step(PhysicsWorld world, SimConfig config, BodyId[] fleet, Vector2[] homeM, int step, bool packed, float dtS)
    {
        if (packed)
        {
            var leanM = config.Car.LengthM * 0.5f * ((step / LeanSteps & 1) == 0 ? 1f : -1f);
            for (var body = 0; body < fleet.Length; body++)
            {
                var wantedM = homeM[body] + new Vector2((body & 1) == 0 ? leanM : -leanM, 0f);
                var wantedMps = Vector2.Clamp(
                    (wantedM - world.PositionOf(fleet[body])) * HoldPerS,
                    new Vector2(-HoldMps), new Vector2(HoldMps));
                world.ApplyCentralImpulse(fleet[body], (wantedMps - world.VelocityOf(fleet[body])) * config.Car.MassKg * HoldShare);
            }
        }

        world.Step(dtS);
    }

    /// <summary>How long the packed rows lean one way before leaning the other: a quarter of a second, so they meet and part twice a second.</summary>
    const int LeanSteps = 15;

    const float HoldPerS = 4f;
    const float HoldMps = 3f;

    /// <summary>How much of the correction is spent in one step. Well under one, or the hold is a rigid constraint and the rig rings.</summary>
    const float HoldShare = 0.2f;
}
