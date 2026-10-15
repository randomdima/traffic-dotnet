using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

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
    public static void Loads(in CarBuild car, in CarPose pose, Span<float> into)
    {
        var alongShare = pose.AccelerationMps2.X * car.CgHeightM / car.WheelbaseWeightM2PerS2;
        var acrossShare = pose.AccelerationMps2.Y * car.CgHeightM / car.TrackWeightM2PerS2;

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
    /// <b>The two front wheels' turns as the directions they turn the car's by</b> — each the unit vector of its
    /// <see cref="Ackermann"/> angle in the body's frame, <c>+y</c> the driver's right — worked out off the circle itself
    /// rather than off the angle: a wheel <c>r</c> off the centre's line and a wheelbase ahead of it points along
    /// <c>(r, wheelbase)</c>, so a turn costs a tangent and two roots where the angles cost a tangent, two arctangents
    /// and a sine and cosine a wheel.
    /// </summary>
    /// <remarks>
    /// Under <see cref="TyreFigures.AckermannLeastSteerRad"/> both turn by the rack's own angle, as <c>(1, s)</c> made a
    /// unit — which, so near straight, is its cosine and its sine to the float. Only for a rack off nought: a wheel not
    /// turned at all is the car's own direction (<see cref="Step"/>).
    /// </remarks>
    public static void AckermannTurns(SimConfig config, in CarBuild car, float steerRad, Span<Vector2> into)
    {
        if (MathF.Abs(steerRad) < config.Tyre.AckermannLeastSteerRad)
        {
            var rackM = MathF.Sqrt(1f + (steerRad * steerRad));
            into[0] = into[1] = new Vector2(1f / rackM, steerRad / rackM);
            return;
        }

        var radiusM = MathF.Abs(car.WheelbaseM / Trig.Tan(steerRad));
        var inner = WheelTurn(MathF.Max(config.Tyre.InnerWheelLeastRadiusM, radiusM - car.HalfTrackM), car.WheelbaseM);
        var outer = WheelTurn(radiusM + car.HalfTrackM, car.WheelbaseM);

        // Turning to the driver's right puts the right-hand wheels on the inside of the circle.
        var (rightWheel, leftWheel) = steerRad > 0f ? (inner, outer) : (outer, inner);
        var sign = MathF.Sign(steerRad);
        into[0] = new Vector2(rightWheel.X, sign * rightWheel.Y);
        into[1] = new Vector2(leftWheel.X, sign * leftWheel.Y);
    }

    /// <summary>The direction a wheel <paramref name="offM"/> from the circle's centre line and a wheelbase ahead of it rolls along.</summary>
    static Vector2 WheelTurn(float offM, float wheelbaseM)
    {
        var reachM = MathF.Sqrt((offM * offM) + (wheelbaseM * wheelbaseM));
        return new Vector2(offM / reachM, wheelbaseM / reachM);
    }

    /// <summary>A direction turned by a turn, both unit vectors: the car's own turned by a wheel's (<see cref="AckermannTurns"/>).</summary>
    public static Vector2 Turned(Vector2 direction, Vector2 turn) => new(
        (direction.X * turn.X) - (direction.Y * turn.Y), (direction.Y * turn.X) + (direction.X * turn.Y));

    /// <summary>
    /// The two front wheels turned for the same circle: the inner one turns further than the outer,
    /// because they are turning about one centre at two radii.
    /// </summary>
    /// <remarks>The angles a picture of the wheels is drawn at; the model turns its wheels by <see cref="AckermannTurns"/>, the same circle.</remarks>
    public static void Ackermann(SimConfig config, in CarBuild car, float steerRad, Span<float> into)
    {
        into[2] = 0f;
        into[3] = 0f;
        if (MathF.Abs(steerRad) < config.Tyre.AckermannLeastSteerRad)
        {
            into[0] = steerRad;
            into[1] = steerRad;
            return;
        }

        var radiusM = MathF.Abs(car.WheelbaseM / Trig.Tan(steerRad));
        var innerRad = Trig.Atan(car.WheelbaseM / MathF.Max(config.Tyre.InnerWheelLeastRadiusM, radiusM - car.HalfTrackM));
        var outerRad = Trig.Atan(car.WheelbaseM / (radiusM + car.HalfTrackM));

        // Turning to the driver's right puts the right-hand wheels on the inside of the circle.
        var sign = MathF.Sign(steerRad);
        into[0] = sign * (sign > 0 ? innerRad : outerRad);
        into[1] = sign * (sign > 0 ? outerRad : innerRad);
    }

    // The marks a wheel leaves. All of it is arithmetic over what Step already reported, kept here
    // beside the model that reported it rather than in the town that draws the quads: what a mark
    // *is* is a fact about a tyre, and where it is drawn is not.
}
