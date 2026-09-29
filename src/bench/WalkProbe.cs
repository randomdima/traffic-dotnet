using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;

namespace TrafficSimulation.Bench;

/// <summary>
/// How far a walker takes to get going and to stop, how far a shove carries it, and how far it slides off
/// its feet, with the figure printed <b>beside the body's own diameter</b>.
/// </summary>
/// <remarks>
/// <para>
/// This is the instrument the person's whole movement model is judged by, and it exists because the
/// requirement is a <em>relation</em> and not a number: a walker has no acceleration of its own, so it
/// reaches its pace and loses it inside a tick, and a shove at walking pace is taken back inside a fifth of
/// its own body — whatever the walk speed is set to, the grip is whatever makes that true (PER-3). A
/// complaint about how a walker moves is a measurement, not a matter of taste.
/// </para>
/// <para>
/// <b>One walker, no town.</b> Every figure taken in a town is an average over crowds, kerbs and
/// whatever the walker last collided with; what is being measured here is the model, so the world is
/// empty and the only thing in it is the body. <b>No ground either</b>: a walker's pace and grip are its
/// own on every surface (TER-2).
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
                          $"{config.PersonWalkSpeedMps:F2} m/s, a shove taken back on grip {config.PersonFootGripMps2:F0} m/s²");
        Console.WriteLine($"{"",-10}{"pace m/s",10}{"start m",10}{"stop m",9}{"v²/2a m",10}{"of a body",11}{"crab m/s",10}");

        Report("walking", Measure(config, WalkCase.Walking));
        Report("shoved", Measure(config, WalkCase.Shoved));
        Report("off feet", Measure(config, WalkCase.OffItsFeet));

        Console.WriteLine($"The requirement is the relation, not the number: a walker reaches its pace and loses it inside a " +
                          $"tick ({config.PersonStepM:F2} m), and takes back a shove at its pace inside a fifth of its own " +
                          $"body — {bodyM / 5f:F2} m here, which is what v²/2a answers.");
        Console.WriteLine("Off its feet start and stop are not the same distance, and the model is not why: a semi-implicit " +
                          "step integrates position with the velocity the tick ended at, so starting spends the whole of the " +
                          "last tick already at pace and stopping spends it at nothing.");

        void Report(string name, WalkRun run)
        {
            Console.WriteLine($"{name,-10}{run.PaceMps,10:F2}{run.StartM,10:F3}{run.StopM,9:F3}" +
                              $"{run.ContinuousM,10:F3}{run.StopM / bodyM,11:F2}{run.CrabMps,10:F4}");
        }
    }

    /// <summary>What is done to the body: walked up to pace and stood, set going at pace by something else and left to stand, or the walk off its feet.</summary>
    public enum WalkCase
    {
        Walking,
        Shoved,
        OffItsFeet,
    }

    /// <summary>
    /// One run's answer. <see cref="ContinuousM"/> is <c>v²/2a</c> — what the same stop would cost an
    /// integrator with no tick in it, and the figure the requirement's own arithmetic is written in. Nothing
    /// for a walker's own stop, which spends no grip.
    /// </summary>
    public readonly record struct WalkRun(float PaceMps, float StartM, float StopM, float ContinuousM, float CrabMps);

    /// <summary>
    /// Get one body to pace and then ask it to stand. The walker is driven through exactly the same
    /// follower the town runs it through — a probe with a movement model of its own measures the probe.
    /// </summary>
    public static WalkRun Measure(SimConfig config, WalkCase walk)
    {
        var physics = new PhysicsWorld(config);

        var body = physics.AddPerson(Vector2.Zero);
        var massKg = physics.MassOf(body);
        var dt = config.TickSeconds;
        var pace = config.PersonWalkSpeedMps;
        var onFeet = walk != WalkCase.OffItsFeet;

        var positionM = Vector2.Zero;
        var velocityMps = Vector2.Zero;
        var declaredMps = Vector2.Zero;
        var headingRad = 0f;
        var crabMps = 0f;

        var startM = 0f;
        if (walk == WalkCase.Shoved)
        {
            // What a contact does: the body moves and the walker declared none of it.
            physics.ApplyCentralImpulse(body, Vector2.UnitX * pace * massKg);
            velocityMps = Vector2.UnitX * pace;
        }
        else
        {
            startM = Walk(moving: true, until: speed => speed >= pace * AtPaceFraction);
        }

        var paceReachedMps = velocityMps.Length();
        var stopM = Walk(moving: false, until: speed => speed <= pace * StoppedFraction);

        var continuousM = walk switch
        {
            WalkCase.Shoved => pace * pace / (2f * config.PersonFootGripMps2),
            WalkCase.OffItsFeet => pace * pace / (2f * config.PersonSlidingGripMps2),
            _ => 0f,
        };
        return new WalkRun(paceReachedMps, startM, stopM, continuousM, crabMps);

        float Walk(bool moving, Func<float, bool> until)
        {
            var travelledM = 0f;
            for (var tick = 0; tick < MostTicks; tick++)
            {
                var step = WalkerFollower.Step(
                    config, headingRad, positionM, velocityMps, declaredMps, positionM + Vector2.UnitX, moving,
                    onFeet, massKg, dt);
                headingRad = step.HeadingRad;
                declaredMps = step.DesiredMps;
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
