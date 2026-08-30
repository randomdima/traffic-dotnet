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
    /// <para>
    /// A town seen from above has no down and the solver is given no gravity, but a tyre's load is a
    /// weight all the same — <c>a·h/(base·g)</c> is the share of it that moves, which is the whole
    /// reason there is a centre-of-gravity height at all.
    /// </para>
    /// <para>
    /// <b>The acceleration is the one the patches themselves caused</b> and not the body's, which is what
    /// the relation above is derived from: a force at the ground acting through a height. It is also why
    /// nothing here needs a ceiling — a tyre cannot push harder than it grips, so the transfer is bounded
    /// by the same coefficient it goes on to weigh. A collision is not a manoeuvre and moves no load.
    /// </para>
    /// <para>
    /// <b>What it moves is measured from where <em>this body</em> stands at rest</b>
    /// (<see cref="CarBuild.FrontWeightShare"/>, CAR-11), and that is the variant's own figure rather than
    /// a half: the axle a car carries its weight on is the axle that can put power down and the light one
    /// is the end that lets go. Across the car it <em>is</em> a half, and nothing here can make it anything
    /// else — no body in this fleet is heavier on one flank than the other.
    /// </para>
    /// </remarks>
    public static void Loads(SimConfig config, in CarBuild car, in CarPose pose, Span<float> into)
    {
        var weight = config.Tyre.StandardGravityMps2;
        var alongShare = pose.AccelerationMps2.X * car.CgHeightM / (car.WheelbaseM * weight);
        var acrossShare = pose.AccelerationMps2.Y * car.CgHeightM / (car.HalfTrackM * 2f * weight);

        // Nought and one, because that is what a load is: a wheel asked for more transfer than it stands
        // on lifts, and a lifted wheel carries nothing rather than pulling the car down. There is no
        // figure here to choose — a share below nothing is a wheel holding the road on from underneath.
        var front = Math.Clamp(car.FrontWeightShare - alongShare, 0f, 1f);
        var toTheSide = Math.Clamp(0.5f - acrossShare, 0f, 1f);
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
