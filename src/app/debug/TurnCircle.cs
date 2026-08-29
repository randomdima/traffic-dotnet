using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>The circle a car's steering says it must be turning</b>: where the centre of that turn stands, how
/// far the nearest rear wheel is from it, and the four patches the construction is drawn through.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is worked out here and nowhere else, which is the point of it</b> (OBS-2j). Every other thing
/// this overlay draws is read off the producer that made it, because a second copy of a shape eventually
/// disagrees with the first. There is no producer for this one: nothing in the simulation ever computes a
/// turn centre — a car turns because four contact patches spend four impulses — so the only way to ask
/// <em>is the body going round what the geometry says it should</em> is to work the geometry out
/// independently and put it on the ground beside what the tyres actually wrote.
/// </para>
/// <para>
/// <b>The centre is where the axles say it is</b> (Ackermann's own construction): every wheel rolls about
/// a point square to its own axle, so the centre of the turn is where the rear axle's line crosses a front
/// wheel's. It is taken as that crossing rather than from <c>wheelbase / tan(steer)</c> — the two agree,
/// and the crossing is what can be drawn, so a reader sees the rule rather than a figure.
/// </para>
/// <para>
/// <b>What it is <em>not</em> is where the body is actually turning about.</b> That is the tyres' answer
/// and is written on the road by them; the whole instrument is the daylight between the two, and a slow
/// car closes it while a car being asked for more than its tyres hold does not
/// (<see cref="CityGen.SkidpadPlan"/>).
/// </para>
/// </remarks>
/// <param name="CentreM">Where the rear axle's line crosses the inner front wheel's.</param>
/// <param name="RadiusM">
/// How far the <b>nearest rear wheel</b> stands from it, which is the tightest arc anything on this car is
/// being asked to trace.
/// </param>
/// <param name="RearAxleRadiusM">
/// And how far the middle of the rear axle stands from it — the figure a bicycle model quotes, and the one
/// worth comparing a measured turn rate against.
/// </param>
internal readonly record struct TurnCircle(
    Vector2 CentreM, float RadiusM, float RearAxleRadiusM, Vector2 FrontInnerM, Vector2 FrontOuterM,
    Vector2 RearInnerM, Vector2 RearOuterM)
{
    /// <summary>
    /// Past this the steering is straight as far as any picture is concerned: the centre is off the far
    /// side of the town and the arc through the car is a line. It is a drawing bound rather than a fact
    /// about a car — nothing is measured out here, because nothing on any map turns this wide.
    /// </summary>
    public const float WidestM = 250f;

    /// <summary>
    /// The construction for one car, or false where the wheels are near enough straight for the centre to
    /// be nowhere worth drawing.
    /// </summary>
    /// <param name="steerRad">
    /// The angle the front wheels are actually at, which is the one the tyres are working at and the one
    /// the sprites are drawn at (<see cref="TyreModel.Ackermann"/>) — <b>and not what anything asked
    /// for</b>: the rack takes time to travel, and a circle drawn for the lock a driver wanted is a circle
    /// the car is not on yet.
    /// </param>
    public static bool Of(in CarBuild build, Vector2 positionM, float headingRad, float steerRad, out TurnCircle circle)
    {
        Heading.Frame(headingRad, out var forward, out var right);

        Span<float> wheelRad = stackalloc float[TyreModel.Wheels];
        TyreModel.Ackermann(build, steerRad, wheelRad);

        // The turn is to the driver's right for a positive angle, so the inside of it is the +y side of
        // the body — which is the side the pair with the even indices stand on.
        var toTheRight = steerRad > 0f;
        var frontInner = toTheRight ? 0 : 1;
        var rearInner = toTheRight ? 2 : 3;

        var frontInnerM = WheelM(build, positionM, forward, right, frontInner);
        var frontOuterM = WheelM(build, positionM, forward, right, frontInner ^ 1);
        var rearInnerM = WheelM(build, positionM, forward, right, rearInner);
        var rearOuterM = WheelM(build, positionM, forward, right, rearInner ^ 1);

        // Square to the inner front wheel, which is the line every point of that wheel's own circle stands
        // on. Where it is parallel to the rear axle the wheels are straight and there is no crossing.
        var frontAxis = Heading.RightOf(Heading.Unit(headingRad + wheelRad[frontInner]));
        var apart = Cross(right, frontAxis);
        if (MathF.Abs(apart) < Parallel)
        {
            circle = default;
            return false;
        }

        var alongAxleM = Cross(frontInnerM - rearInnerM, frontAxis) / apart;
        var centreM = rearInnerM + (right * alongAxleM);
        var radiusM = (centreM - rearInnerM).Length();
        if (radiusM > WidestM)
        {
            circle = default;
            return false;
        }

        var rearAxleM = positionM - (forward * build.CentreAheadOfAxleM);
        circle = new TurnCircle(
            centreM, radiusM, (centreM - rearAxleM).Length(), frontInnerM, frontOuterM, rearInnerM, rearOuterM);
        return true;
    }

    /// <summary>Where one patch stands in the town — the car's own axles under its own body (CAR-11).</summary>
    static Vector2 WheelM(in CarBuild build, Vector2 positionM, Vector2 forward, Vector2 right, int wheel)
    {
        var atBody = TyreModel.WheelAtM(build, wheel);
        return positionM + (forward * atBody.X) + (right * atBody.Y);
    }

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    /// <summary>How near parallel the two axles have to be before there is no crossing to speak of.</summary>
    const float Parallel = 1e-4f;
}
