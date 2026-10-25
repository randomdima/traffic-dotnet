using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>Where the patches are, what they push the body with, and how far each is steered — the arithmetic of the layout rather than of the rubber.</summary>
internal static partial class TyreModel
{
    /// <summary>
    /// Where a car's patches stand in the town, which is where the ground under them is sampled and
    /// where their impulses are spent. Taken once and handed to both, since the two are the same
    /// points and the body cannot move between them.
    /// </summary>
    public static void WheelPointsM(in CarBuild car, in CarPose pose, Span<Vector2> into)
    {
        var forward = pose.Forward;
        var right = pose.Right;
        for (var wheel = 0; wheel < car.WheelCount; wheel++)
        {
            var atBody = WheelAtM(car, wheel);
            into[wheel] = pose.PositionM + (forward * atBody.X) + (right * atBody.Y);
        }
    }

    /// <summary>
    /// Where a wheel stands in the body's own frame: <c>+x</c> forward, <c>+y</c> the driver's side.
    /// <b>The car's own wheels under its own body</b> (CAR-11) — a pickup carries its rear axle further back
    /// under a longer body than a hatchback does, and the patches are where its picture puts them.
    /// </summary>
    public static Vector2 WheelAtM(in CarBuild car, int wheel) => car.WheelAt(wheel).AtM;

    /// <summary>
    /// <b>What a car's patches pushed its body with over a step</b>, along it and to its right: their impulses over the
    /// step, at the ground — what its springs are carried on by (<see cref="Suspension.Step"/>). A collision is not a
    /// patch's and pushes nothing here.
    /// </summary>
    /// <param name="forward">The body's heading after the step, which the device reads it in as well.</param>
    public static Vector2 PushN(ReadOnlySpan<WheelImpulse> spent, Vector2 forward, float dtS)
    {
        var spentNs = Vector2.Zero;
        foreach (var wheel in spent) spentNs += wheel.ImpulseNs;
        var pushN = spentNs / dtS;
        return new Vector2(Vector2.Dot(pushN, forward), Vector2.Dot(pushN, Heading.RightOf(forward)));
    }

    /// <summary>
    /// <b>The steered wheels' turns as the directions they turn the car's by</b>, each at its wheel's own index — the unit
    /// vector of its <see cref="Ackermann"/> angle in the body's frame, <c>+y</c> the driver's right — worked out off the
    /// circle itself rather than off the angle: a wheel <c>r</c> off the centre's line and <c>a</c> ahead of the axle the
    /// centre stands on points along <c>(r, a)</c>, so a turn costs a tangent and a root a wheel where the angles cost an
    /// arctangent and a sine and cosine more. A wheel steered behind that axle turns the other way, as it should.
    /// </summary>
    /// <remarks>
    /// Under <see cref="TyreFigures.AckermannLeastSteerRad"/> every one turns by the rack's own angle, as <c>(1, s)</c> made
    /// a unit — which, so near straight, is its cosine and its sine to the float. Only for a rack off nought: a wheel not
    /// turned at all is the car's own direction (<see cref="Step"/>).
    /// </remarks>
    public static void AckermannTurns(SimConfig config, in CarBuild car, float steerRad, Span<Vector2> into)
    {
        if (MathF.Abs(steerRad) < config.Tyre.AckermannLeastSteerRad)
        {
            var rackM = MathF.Sqrt(1f + (steerRad * steerRad));
            var rack = new Vector2(1f / rackM, steerRad / rackM);
            for (var wheel = 0; wheel < car.WheelCount; wheel++) into[wheel] = rack;
            return;
        }

        // The circle's centre stands on the line of the axle that does not steer, as far to the side the car is turning.
        var radiusM = MathF.Abs(car.WheelbaseM / Trig.Tan(steerRad));
        var sign = MathF.Sign(steerRad);
        for (var wheel = 0; wheel < car.WheelCount; wheel++)
        {
            if (!car.WheelAt(wheel).IsSteered) continue;

            var (offM, aheadM) = OffTheCentre(config, car, radiusM, sign, wheel);
            var reachM = MathF.Sqrt((offM * offM) + (aheadM * aheadM));
            into[wheel] = new Vector2(offM / reachM, sign * aheadM / reachM);
        }
    }

    /// <summary>
    /// A wheel's place off the circle's centre: across, from the centre's line — the inside of a corner being the side
    /// the car turns to — and along, ahead of the axle the centre stands on.
    /// </summary>
    static (float OffM, float AheadM) OffTheCentre(SimConfig config, in CarBuild car, float radiusM, float sign, int wheel)
    {
        var atM = car.WheelAt(wheel).AtM;
        return (MathF.Max(config.Tyre.InnerWheelLeastRadiusM, radiusM - (sign * atM.Y)), atM.X + car.CentreAheadOfAxleM);
    }

    /// <summary>A direction turned by a turn, both unit vectors: the car's own turned by a wheel's (<see cref="AckermannTurns"/>).</summary>
    public static Vector2 Turned(Vector2 direction, Vector2 turn) => new(
        (direction.X * turn.X) - (direction.Y * turn.Y), (direction.Y * turn.X) + (direction.X * turn.Y));

    /// <summary>
    /// The steered wheels turned for the same circle: the inner one turns further than the outer,
    /// because they are turning about one centre at different radii. An unsteered wheel is nought.
    /// </summary>
    /// <remarks>The angles a picture of the wheels is drawn at; the model turns its wheels by <see cref="AckermannTurns"/>, the same circle.</remarks>
    public static void Ackermann(SimConfig config, in CarBuild car, float steerRad, Span<float> into)
    {
        var straight = MathF.Abs(steerRad) < config.Tyre.AckermannLeastSteerRad;
        var radiusM = MathF.Abs(car.WheelbaseM / Trig.Tan(steerRad));
        var sign = MathF.Sign(steerRad);
        for (var wheel = 0; wheel < car.WheelCount; wheel++)
        {
            if (!car.WheelAt(wheel).IsSteered)
            {
                into[wheel] = 0f;
                continue;
            }

            if (straight)
            {
                into[wheel] = steerRad;
                continue;
            }

            var (offM, aheadM) = OffTheCentre(config, car, radiusM, sign, wheel);
            into[wheel] = sign * Trig.Atan(aheadM / offM);
        }
    }

    // The marks a wheel leaves. All of it is arithmetic over what Step already reported, kept here
    // beside the model that reported it rather than in the town that draws the quads: what a mark
    // *is* is a fact about a tyre, and where it is drawn is not.
}
