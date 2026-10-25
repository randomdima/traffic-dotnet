using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>What the body is at the start of a tick: the one snapshot every wheel reads.</summary>
/// <remarks>
/// All four wheels read this and never the live body. The solver applies
/// an impulse the moment it is given, so a wheel that read the body back would be reading three
/// wheels' work, and the order the wheels happened to be stepped in would break the cancellation
/// between the two ends of an axle.
/// </remarks>
internal readonly record struct CarPose
{
    public CarPose(
        Vector2 positionM, float headingRad, Vector2 velocityMps, float yawRateRadPerS, float massKg, SprungBody sprung = default)
        : this(positionM, headingRad, Heading.Unit(headingRad), velocityMps, yawRateRadPerS, massKg, sprung)
    {
    }

    /// <summary>A pose whose <paramref name="forward"/> is already in hand as the unit vector of <paramref name="headingRad"/>.</summary>
    /// <remarks>
    /// <b>Only for a forward that is <c>Heading.Unit(headingRad)</c> to the bit</b> — the solver's own rotation, which it
    /// reduces the heading to with that very function (<c>Shape.Rotation</c>) — so a pose read off a body costs no
    /// <c>sincos</c>.
    /// </remarks>
    public CarPose(
        Vector2 positionM, float headingRad, Vector2 forward, Vector2 velocityMps, float yawRateRadPerS, float massKg,
        SprungBody sprung = default)
    {
        PositionM = positionM;
        HeadingRad = headingRad;
        Forward = forward;
        VelocityMps = velocityMps;
        YawRateRadPerS = yawRateRadPerS;
        MassKg = massKg;
        Sprung = sprung;
    }

    public Vector2 PositionM { get; init; }

    public float HeadingRad { get; init; }

    public Vector2 VelocityMps { get; init; }

    public float YawRateRadPerS { get; init; }

    public float MassKg { get; init; }

    /// <summary>
    /// <b>The body on its springs</b> (<see cref="SprungBody"/>): what each wheel carries is read off it
    /// (<see cref="Suspension.Loads"/>), and so is the corner a driven axle has already paid for.
    /// </summary>
    public SprungBody Sprung { get; init; }

    /// <summary>
    /// The way the car is pointing, taken once when the pose is read off the body. Five places in a
    /// driver's tick want it, and working it out in each is a sine and a cosine apiece on the hottest
    /// path this engine has.
    /// </summary>
    /// <remarks>
    /// Derived at construction, so a <c>with</c> expression that changed the heading would carry the old
    /// direction. Build a new pose instead.
    /// </remarks>
    public Vector2 Forward { get; }

    /// <summary>The driver's right, which with <c>+y</c> down is the heading turned a quarter turn.</summary>
    public Vector2 Right => Heading.RightOf(Forward);
}

/// <summary>One wheel's share of the tick: what it spends, and the point it spends it at.</summary>
internal readonly record struct WheelImpulse(Vector2 ImpulseNs, Vector2 AtM);

/// <summary>
/// One stretch of mark a wheel laid this tick (<see cref="TyrePass"/>): from where it was begun to where the wheel
/// stands, at the wheel's width and how dark it was, and whether the ground was ploughed rather than scored.
/// </summary>
/// <param name="Wheel">Which of the car's four laid it, which is the order a car's marks are filed in.</param>
internal readonly record struct TyreMark(Vector2 FromM, Vector2 ToM, float WidthM, float Intensity, bool Ploughed, int Wheel);

/// <summary>
/// What the ground under one patch does to it: grip, drag and the mark threshold, the last already in
/// the units the scrub is measured in.
/// </summary>
internal readonly record struct SurfaceUnderWheel(float Coefficient, float DragMps2, float MarkThresholdM2S3, bool Ploughs)
{
    public static SurfaceUnderWheel Of(SimConfig config, in GroundEffect effect) =>
        new(effect.Coefficient, effect.DragMps2, config.Marks.PowerM2S3 * effect.MarkFactor, effect.Ploughs);
}

/// <summary>
/// <b>The only two grounds a wheel stands on</b> (TER-7b): the carriageway's where it stood on a way when its car's body
/// was last laid — a bit a wheel, which the world keeps with the body — the grass's anywhere else — worked out once for the figures as
/// they stand, and picked by the wheel's bit every tick.
/// </summary>
internal readonly record struct WheelGrounds(SurfaceUnderWheel OffAWay, SurfaceUnderWheel OnAWay)
{
    public static WheelGrounds Of(SimConfig config)
    {
        var catalog = new GroundCatalog(config);
        return new(SurfaceUnderWheel.Of(config, catalog.Grass), SurfaceUnderWheel.Of(config, catalog.Paved));
    }

    /// <summary>Under one wheel of a car whose body was last laid with these of its wheels on a way.</summary>
    public SurfaceUnderWheel Under(byte wheelsOnAWay, int wheel) => ((wheelsOnAWay >> wheel) & 1) != 0 ? OnAWay : OffAWay;
}

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
