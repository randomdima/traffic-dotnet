using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>What the body is at the start of a tick: the one snapshot every wheel reads.</summary>
/// <remarks>
/// All four wheels read this and never the live body. The solver applies
/// an impulse the moment it is given, so a wheel that read the body back would be reading three
/// wheels' work, and the order the wheels happened to be stepped in would break the cancellation
/// between the two ends of an axle.
internal readonly record struct CarPose(
    Vector2 PositionM, float HeadingRad, Vector2 VelocityMps, float YawRateRadPerS, float MassKg, Vector2 AccelerationMps2)
{
    /// <summary>
    /// The way the car is pointing, taken once when the pose is read off the body. Five places in a
    /// driver's tick want it and each used to work it out again, which is a sine and a cosine apiece on
    /// the hottest path this engine has.
    /// </summary>
    /// <remarks>
    /// Derived at construction, so a <c>with</c> expression that changed the heading would carry the old
    /// direction. Build a new pose instead.
    /// </remarks>
    public Vector2 Forward { get; } = Heading.Unit(HeadingRad);

    /// <summary>The driver's right, which with <c>+y</c> down is the heading turned a quarter turn.</summary>
    public Vector2 Right => Heading.RightOf(Forward);
}

/// <summary>One wheel's share of the tick: what it spends, and the point it spends it at.</summary>
internal readonly record struct WheelImpulse(Vector2 ImpulseNs, Vector2 AtM);

/// <summary>
/// What the ground under one patch does to it: grip, drag and the mark threshold, the last already in
/// the units the scrub is measured in.
/// </summary>
internal readonly record struct SurfaceUnderWheel(float Coefficient, float DragMps2, float MarkThresholdM2S3, bool Ploughs);

/// <summary>
/// What one wheel did to the ground it stood on, as friction <b>power per kg</b> of the load that
/// corner carries — so it compares across wheels and across cars — plus the speed the patch is
/// dragging at, so a caller can tell a scrub that lasted an instant from one that is laying rubber.
/// </summary>
/// <param name="Ploughing">
/// Whether this wheel is <em>crossing</em> ground soft enough to be displaced rather than standing on
/// it. Answered here because the two lengths that answer it are only ever taken where the ground can be
/// ploughed at all.
/// </param>
/// <remarks>
/// <para>
/// Power and not force: force says how hard the patch pushes, power says how hard it pushes times how
/// fast it drags, and only the second wears a surface. A tyre held past its limit through a corner
/// pushes at full force and creeps sideways at centimetres a second, polishing nothing; the same tyre
/// locked at 40 m/s scours the road. On force alone the two are identical and every corner paints the
/// road black.
/// </para>
/// <para>
/// The two powers are kept apart. <see cref="SlidePowerM2S3"/> is the tyre losing its fight with the
/// ground; <see cref="PloughPowerM2S3"/> is rolling resistance working the ground at road speed, which
/// on a hard surface displaces nothing and must never mark. Which a surface gets is the surface's
/// business (<see cref="SurfaceUnderWheel.Ploughs"/>), and a hard surface is handed a plough power of
/// zero rather than an unread figure, so nothing downstream has to remember the rule.
/// </para>
/// </remarks>
internal readonly record struct TyreScrub(
    float SlidePowerM2S3, float PloughPowerM2S3, float SlideSpeedMps, bool Ploughing, bool Sliding);
