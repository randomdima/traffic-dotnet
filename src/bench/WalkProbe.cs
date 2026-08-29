using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;

namespace TrafficSimulation.Bench;

/// <summary>
/// How far a walker slides and how far it crabs, over the grounds it walks on, with the figure
/// printed <b>beside the body's own diameter</b>.
/// </summary>
/// <remarks>
/// <para>
/// This is the instrument the person's whole movement model is judged by, and it exists because the
/// requirement is a <em>relation</em> and not a number: a walker
/// reaches its pace, and loses it, inside a fifth of its own body, and says outright that whatever the
/// walk speed is set to, the grip is whatever makes that true. A complaint about how a walker moves is
/// a measurement, not a matter of taste.
/// </para>
/// <para>
/// <b>One walker, no town.</b> Every figure taken in a town is an average over crowds, kerbs and
/// whatever the walker last collided with; what is being measured here is the model, so the world is
/// empty and the only thing in it is the body and the ground's own factor.
/// </para>
/// </remarks>
internal static class WalkProbe
{
    /// <summary>Never reached exactly — an exponential approach — so "at pace" and "stopped" are named fractions of it.</summary>
    const float AtPaceFraction = 0.99f;

    const float StoppedFraction = 0.01f;

    /// <summary>A guard on a loop: at 60 Hz this is a minute of walking, and nothing here takes a second.</summary>
    const int MostTicks = 3_600;

    public static void Run(SimConfig config)
    {
        var bodyM = config.PersonDiameterM;
        Console.WriteLine($"walk probe — one walker, no town, a {bodyM:F2} m body at {config.Person.MassKg:F0} kg, " +
                          $"{config.Person.WalkSpeedMps:F2} m/s on grip {config.Person.FootGripMps2:F0} m/s²");
        Console.WriteLine($"{"ground",-10}{"coefficient",12}{"pace m/s",10}{"start m",10}{"stop m",9}{"v²/2a m",10}{"of a body",11}{"crab m/s",10}");

        foreach (var (name, coefficient) in (ReadOnlySpan<(string, float)>)
                 [("paved", config.Terrain.PavedCoefficient), ("grass", config.Terrain.GrassCoefficient), ("water", config.Terrain.WaterCoefficient)])
        {
            Report(name, Measure(config, coefficient, onFeet: true), coefficient);
        }

        Report("off feet", Measure(config, config.Terrain.PavedCoefficient, onFeet: false), config.Terrain.PavedCoefficient);

        Console.WriteLine($"The requirement is the relation, not the number: a walker reaches its pace and loses it inside " +
                          $"a fifth of its own body — {bodyM / 5f:F2} m here, which is what v²/2a answers.");
        Console.WriteLine("Start and stop are not the same distance, and the model is not why: a semi-implicit step " +
                          "integrates position with the velocity the tick ended at, so starting spends the whole of the " +
                          "last tick already at pace and stopping spends it at nothing.");

        void Report(string name, WalkRun run, float coefficient)
        {
            Console.WriteLine($"{name,-10}{coefficient,12:F2}{run.PaceMps,10:F2}{run.StartM,10:F3}{run.StopM,9:F3}" +
                              $"{run.ContinuousM,10:F3}{run.StopM / bodyM,11:F2}{run.CrabMps,10:F4}");
        }
    }

    /// <summary>
    /// One ground's answer. <see cref="ContinuousM"/> is <c>v²/2a</c> — what the same start and the
    /// same stop would cost an integrator with no tick in it, and the figure the requirement's own
    /// arithmetic is written in.
    /// </summary>
    public readonly record struct WalkRun(float PaceMps, float StartM, float StopM, float ContinuousM, float CrabMps);

    /// <summary>
    /// Walk one body up to pace and then ask it to stand, on one ground. The walker is driven through
    /// exactly the same follower the town runs it through — a probe with a movement model of its own
    /// measures the probe.
    /// </summary>
    public static WalkRun Measure(SimConfig config, float terrainCoefficient, bool onFeet)
    {
        var physics = new PhysicsWorld(config);

        var body = physics.AddPerson(Vector2.Zero);
        var massKg = physics.MassOf(body);
        var dt = config.TickSeconds;
        var pace = config.Person.WalkSpeedMps * terrainCoefficient;

        var positionM = Vector2.Zero;
        var velocityMps = Vector2.Zero;
        var headingRad = 0f;
        var crabMps = 0f;

        var startM = Walk(moving: true, until: speed => speed >= pace * AtPaceFraction);
        var paceReachedMps = velocityMps.Length();
        var stopM = Walk(moving: false, until: speed => speed <= pace * StoppedFraction);

        var gripMps2 = (onFeet ? config.Person.FootGripMps2 : config.PersonSlidingGripMps2) * terrainCoefficient;
        return new WalkRun(paceReachedMps, startM, stopM, pace * pace / (2f * gripMps2), crabMps);

        float Walk(bool moving, Func<float, bool> until)
        {
            var travelledM = 0f;
            for (var tick = 0; tick < MostTicks; tick++)
            {
                var step = WalkerFollower.Step(
                    config, headingRad, positionM, velocityMps, positionM + Vector2.UnitX, moving,
                    terrainCoefficient, onFeet, massKg, dt);
                headingRad = step.HeadingRad;
                physics.ApplyCentralImpulse(body, step.ImpulseNs);
                physics.Step(dt);

                var now = physics.PositionOf(body);
                travelledM += (now - positionM).Length();
                positionM = now;
                velocityMps = physics.VelocityOf(body);

                // Velocity across the heading while moving: what is left of it is contacts and the
                // last tick of a turn, and no grip figure will touch it.
                var along = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
                crabMps = MathF.Max(crabMps, MathF.Abs(velocityMps.X * -along.Y + velocityMps.Y * along.X));

                if (until(velocityMps.Length())) break;
            }

            return travelledM;
        }
    }
}
