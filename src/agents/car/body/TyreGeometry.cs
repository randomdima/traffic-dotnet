using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>Where the four patches are, what each carries, and how far each is steered — the arithmetic of the layout rather than of the rubber.</summary>
internal static partial class TyreModel
{
    /// <summary>
    /// Where the four patches stand in the town, which is where the ground under them is sampled and
    /// where their impulses are spent. Taken once and handed to both, since the two are the same four
    /// points and the body cannot move between them.
    /// </summary>
    public static void WheelPointsM(in CarBuild car, in CarPose pose, Span<Vector2> into)
    {
        var forward = pose.Forward;
        var right = pose.Right;
        for (var wheel = 0; wheel < Wheels; wheel++)
        {
            var atBody = WheelAtM(car, wheel);
            into[wheel] = pose.PositionM + (forward * atBody.X) + (right * atBody.Y);
        }
    }

    /// <summary>
    /// Where a wheel stands in the body's own frame: <c>+x</c> forward, <c>+y</c> the driver's side.
    /// <b>The car's own axles under its own body</b> (CAR-11) — a pickup carries its rear axle further back
    /// under a longer body than a hatchback does, and the four patches are where its picture puts them.
    /// </summary>
    public static Vector2 WheelAtM(in CarBuild car, int wheel) => new(
        wheel < 2 ? car.WheelbaseM - car.CentreAheadOfAxleM : -car.CentreAheadOfAxleM,
        wheel % 2 == 0 ? car.HalfTrackM : -car.HalfTrackM);

    /// <summary>
    /// What each corner carries as the car pitches and rolls: a quarter each at rest, moved front to
    /// back by what the pedals are doing and side to side by what the corner is doing.
    /// </summary>
    /// <remarks>
    /// A town seen from above has no down and the solver is given no gravity, but a tyre's load is a
    /// weight all the same — <c>a·h/(base·g)</c> is the share of it that moves, which is the whole
    /// reason there is a centre-of-gravity height at all.
    /// </remarks>
    public static void Loads(SimConfig config, in CarBuild car, in CarPose pose, Span<float> into)
    {
        var weight = config.Tyre.StandardGravityMps2;
        var floor = config.Tyre.MinCornerLoadFraction;
        var alongShare = pose.AccelerationMps2.X * car.CgHeightM / (car.WheelbaseM * weight);
        var acrossShare = pose.AccelerationMps2.Y * car.CgHeightM / (car.HalfTrackM * 2f * weight);

        // The floor is what keeps a shunted car on four tyres. The acceleration a transfer is read from
        // is already capped at what the tyres could plausibly have caused, but that cap is still enough
        // to empty an axle on this geometry — and a corner carrying nothing has a budget of nothing, so
        // it delivers no impulse at all until the load comes back, which is a car that spins after being
        // nudged. A twentieth of the mass is little enough to read as a wheel gone light and enough that
        // it is still a wheel.
        var front = Math.Clamp(0.5f - alongShare, floor, 1f - floor);
        var toTheSide = Math.Clamp(0.5f - acrossShare, floor, 1f - floor);
        into[0] = front * toTheSide;
        into[1] = front * (1f - toTheSide);
        into[2] = (1f - front) * toTheSide;
        into[3] = (1f - front) * (1f - toTheSide);
    }

    /// <summary>
    /// The two front wheels turned for the same circle: the inner one turns further than the outer,
    /// because they are turning about one centre at two radii.
    /// </summary>
    public static void Ackermann(in CarBuild car, float steerRad, Span<float> into)
    {
        into[2] = 0f;
        into[3] = 0f;
        if (MathF.Abs(steerRad) < 1e-4f)
        {
            into[0] = steerRad;
            into[1] = steerRad;
            return;
        }

        var radiusM = MathF.Abs(car.WheelbaseM / MathF.Tan(steerRad));
        var innerRad = MathF.Atan(car.WheelbaseM / MathF.Max(0.01f, radiusM - car.HalfTrackM));
        var outerRad = MathF.Atan(car.WheelbaseM / (radiusM + car.HalfTrackM));

        // Turning to the driver's right puts the right-hand wheels on the inside of the circle.
        var sign = MathF.Sign(steerRad);
        into[0] = sign * (sign > 0 ? innerRad : outerRad);
        into[1] = sign * (sign > 0 ? outerRad : innerRad);
    }

    // The marks a wheel leaves. All of it is arithmetic over what Step already reported, kept here
    // beside the model that reported it rather than in the town that draws the quads: what a mark
    // *is* is a fact about a tyre, and where it is drawn is not.
}
