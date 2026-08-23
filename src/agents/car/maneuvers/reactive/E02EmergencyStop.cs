using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`E-2` — emergency stop.</b> Something is in the path within braking distance and the ordinary
/// speed profile will not stop the car in time. See <c>docs/e02-emergency-stop.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>It outranks everything</b> (§1.5 row 1) and is the one entry the standing rules detect for
/// themselves, because by the time a procedure noticed it would be too late — and it is asked on every
/// tick whatever the decision clock says. What it costs to ask is arithmetic on the headway already read
/// and two speeds, with no query anywhere in it.
/// </para>
/// <para>
/// <b>Braking rather than swerving, always.</b> A swerve wants a lane verified clear, and verifying one
/// is `E-4`'s job and takes time this does not have. The profile plans against a <em>margin</em> of the
/// grip; this is the tick where that margin is no longer enough and what is left of the tyre is spent at
/// once. The wheel is left exactly where it was, and the handbrake is not touched — locking wheels at
/// road speed is a skid.
/// </para>
/// <para>
/// <b>Frequent use of this is a planning failure and not a safety feature</b>, which is why the trace
/// counts it: constant flat-out braking means the profile or the looking is wrong upstream.
/// </para>
/// </remarks>
internal static class E02EmergencyStop
{
    public const bool ThinksEveryTick = true;

    /// <summary>A car braking as hard as it can is not stuck, and it stops naming itself the moment it is stopped.</summary>
    public const bool Watched = false;

    /// <summary>
    /// The trigger, and the whole of it. <b>The closing speed and not this car's own</b>: a queue moving
    /// at the same pace is not an emergency however close it is, and that difference is the second thing
    /// the reading carries.
    /// </summary>
    public static bool IsAHazard(in DriveScene scene) =>
        IsAHazard(scene.Config, scene.AlongMps, scene.Context);

    /// <summary>
    /// The same question from the sensing half of the tick, which holds the two readings but not a scene:
    /// row 1 is asked before any procedure runs and on every tick, so it may not wait for one to be built.
    /// </summary>
    public static bool IsAHazard(SimConfig config, float alongMps, in DriveContext context)
    {
        if (alongMps <= config.Driving.StopSpeedMps || float.IsPositiveInfinity(context.HeadwayM))
        {
            return false;
        }

        var closingMps = alongMps - MathF.Max(0f, context.HeadwaySpeedMps);
        if (closingMps <= config.Driving.StopSpeedMps) return false;

        var gapM = context.HeadwayM - config.Car.LengthM * 0.5f;
        if (gapM <= 0f) return true;

        // <b>What is left when the margin the profile kept back is spent</b>, and never a figure of this
        // entry's own (SIM-7). The profile plans every stop at <c>BrakingMargin</c> of this, so a threshold
        // below it is an entry that fires on the profile's own ordinary braking and takes the pedal off it:
        // read at <c>GripMargin</c> it stood at three quarters of what the profile was already planning
        // with, and `E-2` was 16 % of every car-tick on the proving ground.
        return closingMps * closingMps / (2f * gapM) > config.CarUtmostBrakingMps2(context.GroundCoefficient);
    }

    /// <summary><c>Sa</c>: none. Row 1 of the arbitration binds everywhere, including inside a recovery.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject) => ManeuverStart.Yes;

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (IsAHazard(in scene))
        {
            limits = DriveLimits.Hold with { SpendTheTyre = true };
            return ManeuverOutcome.Running;
        }

        // <b>A reflex keeps its name for a beat after it fires</b>, and imposes nothing while it does. A
        // car braking hard in stop-start traffic drops below the closing speed that triggered this, is
        // let go, accelerates into the same gap and triggers it again — which is one emergency stop and
        // not twenty, and counting it as twenty buries the reading this entry exists to give.
        return scene.InManeuverS < scene.Config.Ladder.ReflexHoldS
            ? ManeuverOutcome.Running
            : ManeuverOutcome.Resume(ManeuverReason.WayIsClear);
    }
}
