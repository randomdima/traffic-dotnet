using System.Numerics;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// The one command a driver issues in a tick, and the whole of what a body may be asked for (CAR-3):
/// a steering angle, a pedal each way, the handbrake, and which gear it is in.
/// </summary>
/// <remarks>
/// The pedals are what is <em>asked</em> for, in m/s² of the whole car; what the car gets is
/// traction-limited at the wheels, which is why a hard stop is a locked-wheel skid rather than the
/// braking figure. A hand at the wheel fills this in exactly as a follower does.
/// </remarks>
internal readonly record struct DriveCommand(float SteerRad, float ThrottleMps2, float BrakeMps2, bool Handbrake, bool Reverse)
{
    public static DriveCommand Idle => new(0f, 0f, 0f, false, false);

    /// <summary>An unmanned car holds its handbrake, so a parked car stays parked.</summary>
    public static DriveCommand Parked => new(0f, 0f, 0f, true, false);

    /// <summary>
    /// Everything the car has, against the way it is going. The handbrake alone is what holds a car
    /// that has already stopped; a car still moving that is asked to stop needs the brake pedal too,
    /// or the locked rear pair is the only thing slowing it and it slides the length of a street.
    /// </summary>
    public static DriveCommand Stopping(float brakeMps2) => new(0f, 0f, brakeMps2, true, false);

    /// <summary>
    /// A wreck: nobody at the pedals and all four wheels locked, so a shunted one skids as a block
    /// rather than rolling away. The infinity is not a pedal figure — it is what a locked wheel
    /// <em>is</em>, every wheel asked to cancel the whole of what it carries. What it actually gets is
    /// still the ellipse's, which is why this leaves a skid and not a stop.
    /// </summary>
    /// <remarks>
    /// <b>The wheels lock where they were pointing</b> (PHY-5): a car is wrecked mid-corner with its rack
    /// wound over, and nothing afterwards is turning it back. The angle is therefore carried rather than
    /// zeroed — it is what the four patches skid along and what the four tyres are drawn at, and a wreck
    /// whose wheels snapped straight on the tick it broke is one the crash visibly tidied up.
    /// </remarks>
    public static DriveCommand LockedAt(float steerRad) => new(steerRad, 0f, float.PositiveInfinity, true, false);

    /// <summary>The same, for a wreck whose wheels were straight — and the shape of the state in a test.</summary>
    public static DriveCommand Locked => LockedAt(0f);

    public float GearSign => Reverse ? -1f : 1f;

    /// <summary>
    /// Whether this command locks the <em>front</em> pair as well, which is the difference between a
    /// handbrake and a wreck. It is read off the infinite brake <see cref="Locked"/> asks for rather
    /// than carried as a flag of its own: a brake that big is not a pedal figure, it is what a locked
    /// wheel is, and nothing else in this engine ever asks for one.
    /// </summary>
    public bool LocksEveryWheel => float.IsPositiveInfinity(BrakeMps2);
}
