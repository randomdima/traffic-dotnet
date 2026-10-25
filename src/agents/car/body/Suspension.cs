using System.Numerics;
using System.Runtime.InteropServices;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// <b>A car's body on its springs</b> (CAR-56): how far it has sunk, pitched nose down and rolled right side down off where
/// it stands at rest, and how fast each is going. The suspension's whole state, and nobody's decision — it is what the
/// tyres' pushes and the springs make of each other (<see cref="Suspension.Step"/>).
/// </summary>
/// <remarks>Field for field with <c>SprungBody</c> (<c>kernels/suspension.glsl</c>), which the device keeps in each car's pose.</remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SprungBody(
    float HeaveM, float PitchRad, float RollRad, float HeaveMps, float PitchRadPerS, float RollRadPerS)
{
    /// <summary>How it stands tilted, nose down and right side down: what the picture draws (<c>CarView.TiltHalves</c>).</summary>
    public Vector2 TiltRad => new(PitchRad, RollRad);
}

/// <summary>
/// <b>A rigid body on a damped spring at every wheel</b> (CAR-56, CAR-3e): what a wheel carries is what its spring pushes
/// up with, and the body heaves, pitches and rolls by what the springs and the tyres' pushes at the ground leave it. Any
/// number of wheels at any places under the body (<see cref="CarBuild.Wheels"/>), each sprung and damped alike.
/// </summary>
/// <remarks>
/// <para>
/// <b>A wheel <c>(x, y)</c> off the centre of mass sinks by <c>heave + x·pitch + y·roll</c></b> and pushes up with what it
/// stood on at rest plus its spring times that sink and its damper times how fast it is sinking. A spring pushing less
/// than nothing is a wheel off the ground, which carries nothing: that is contact, not a limit, and the body sinks on
/// the others by its weight there.
/// </para>
/// <para>
/// <b>A tyre pushes at the ground, a height under the centre of mass</b>, so a push along the car pitches it and one
/// across rolls it; a collision acts at about that height and pitches nothing, and is not counted. One semi-implicit
/// Euler step a tick: the stiffest mode of the fleet, a microcar's roll, is 2.5 Hz — a quarter of a radian of it a tick
/// at 60 Hz.
/// </para>
/// <para>
/// Line for line with <c>kernels/suspension.glsl</c>, where the device steps it (<c>world_view.comp</c>); the host's world
/// is the same function (<c>ManagedWorld.StepTheSprings</c>), to the bit.
/// </para>
/// </remarks>
internal static class Suspension
{
    /// <summary>
    /// <b>The least any figure of the state is before it is nought</b>: 2^-100. Not a physical bound — a hair of a
    /// radian under anything a picture or a grip could show — but the float's: a state decaying towards rest would
    /// otherwise reach the subnormals, which a device that flushes them reads as nought and the host does not, and this
    /// keeps every product a step makes of the state normal.
    /// </summary>
    public const float LeastState = 7.888609e-31f;

    /// <summary>
    /// <b>What each wheel carries</b>, as its share of what the car stands on: its spring's push over all of them, so the
    /// shares are the whole car (CAR-3e) and a wheel off the ground has none.
    /// </summary>
    /// <param name="shareInto">A share a wheel, in <see cref="CarBuild.Wheels"/> order.</param>
    /// <returns>
    /// <b>The acceleration across the car its springs are carrying</b>, to the driver's right: the roll moment the
    /// wheels push back with over the height it acts through — the corner a driven axle has already paid for
    /// (<c>TyrePass.DriveCeilingMps2</c>), which at a steady corner is the corner itself.
    /// </returns>
    public static float Loads(in CarBuild car, in SprungBody body, Span<float> shareInto)
    {
        var plane = Deviation(car, body);
        var totalN = 0f;
        var acrossNm = 0f;
        for (var wheel = 0; wheel < car.WheelCount; wheel++)
        {
            var atM = FromCentreOfMassM(car, wheel);
            var standingN = Standing(car, atM);
            var pushN = MathF.Max(0f, standingN + Along(plane, atM));
            shareInto[wheel] = pushN;
            totalN += pushN;
            acrossNm += (pushN - standingN) * atM.Y;
        }

        // A car with no wheel on the ground stands on nothing, and none of its tyres has anything to grip with. One quotient a
        // car and not one a wheel: on the device each is a correctly rounded one, and dear.
        var perN = totalN > 0f ? 1f / totalN : 0f;
        for (var wheel = 0; wheel < car.WheelCount; wheel++) shareInto[wheel] *= perN;

        return -acrossNm * car.InverseMassCgHeightPerKgM;
    }

    /// <summary>
    /// <b>The body carried on by one step</b> under what its tyres pushed it with at the ground this step, along it and to
    /// its right (<paramref name="pushN"/>, the patches' impulses over the step).
    /// </summary>
    public static SprungBody Step(in CarBuild car, in SprungBody body, Vector2 pushN, float dtS)
    {
        var plane = Deviation(car, body);
        var heaveN = 0f;
        var pitchNm = 0f;
        var rollNm = 0f;
        for (var wheel = 0; wheel < car.WheelCount; wheel++)
        {
            var atM = FromCentreOfMassM(car, wheel);
            var standingN = Standing(car, atM);
            var moreN = MathF.Max(0f, standingN + Along(plane, atM)) - standingN;
            heaveN += moreN;
            pitchNm += moreN * atM.X;
            rollNm += moreN * atM.Y;
        }

        // Down is positive: a spring pushing harder than it stood on lifts the body, and a push forward at the ground
        // tips it back on its springs as a push backward dips its nose.
        var heaveMps2 = -heaveN * car.InverseMassPerKg;
        var pitchRadPerS2 = ((-pushN.X * car.CgHeightM) - pitchNm) * car.InversePitchInertiaPerKgM2;
        var rollRadPerS2 = ((-pushN.Y * car.CgHeightM) - rollNm) * car.InverseRollInertiaPerKgM2;

        var heaveMps = Normal(body.HeaveMps + (heaveMps2 * dtS));
        var pitchRadPerS = Normal(body.PitchRadPerS + (pitchRadPerS2 * dtS));
        var rollRadPerS = Normal(body.RollRadPerS + (rollRadPerS2 * dtS));
        return new SprungBody(
            Normal(body.HeaveM + (heaveMps * dtS)), Normal(body.PitchRad + (pitchRadPerS * dtS)),
            Normal(body.RollRad + (rollRadPerS * dtS)), heaveMps, pitchRadPerS, rollRadPerS);
    }

    /// <summary>
    /// <b>The share of the car's weight each wheel stands on at rest</b>, as <c>s₀ + sₓ·x + s_y·y</c> of its place off the
    /// centre of mass: the springs alike, so the one plane through the wheels whose pushes hold up the weight and turn the
    /// body about nothing. Two pairs on two axles stand on the axles as the centre of mass splits the wheelbase; more
    /// wheels than three share it as the springs have it.
    /// </summary>
    /// <param name="wheelsFromCentreOfMassM">Where each wheel stands off the centre of mass, along the body and to its right.</param>
    public static Vector3 StandingShare(ReadOnlySpan<Vector2> wheelsFromCentreOfMassM)
    {
        // The first column of the inverse of Σ[1 x y]ᵀ[1 x y]: the plane whose pushes sum to one and to no moment.
        double n = 0, x = 0, y = 0, xx = 0, xy = 0, yy = 0;
        foreach (var atM in wheelsFromCentreOfMassM)
        {
            n++;
            x += atM.X;
            y += atM.Y;
            xx += atM.X * (double)atM.X;
            xy += atM.X * (double)atM.Y;
            yy += atM.Y * (double)atM.Y;
        }

        var c0 = (xx * yy) - (xy * xy);
        var cx = (xy * y) - (x * yy);
        var cy = (x * xy) - (xx * y);
        var det = (n * c0) + (x * cx) + (y * cy);
        return new Vector3((float)(c0 / det), (float)(cx / det), (float)(cy / det));
    }

    /// <summary>Where a wheel stands off the centre of mass, along the body and to its right.</summary>
    public static Vector2 FromCentreOfMassM(in CarBuild car, int wheel) =>
        TyreModel.WheelAtM(car, wheel) - new Vector2(car.CgAheadOfCentreM, 0f);

    /// <summary>What a wheel stands on at rest, in newtons.</summary>
    static float Standing(in CarBuild car, Vector2 atM) =>
        car.StandingN + (car.StandingNPerMAlong * atM.X) + (car.StandingNPerMAcross * atM.Y);

    /// <summary>
    /// What every spring pushes with past what it stood on, as a plane of a wheel's place: <c>(at the centre, per metre
    /// along, per metre across)</c>.
    /// </summary>
    static Vector3 Deviation(in CarBuild car, in SprungBody body) => new(
        (car.WheelRateNPerM * body.HeaveM) + (car.WheelDampingNsPerM * body.HeaveMps),
        (car.WheelRateNPerM * body.PitchRad) + (car.WheelDampingNsPerM * body.PitchRadPerS),
        (car.WheelRateNPerM * body.RollRad) + (car.WheelDampingNsPerM * body.RollRadPerS));

    static float Along(Vector3 plane, Vector2 atM) => plane.X + (plane.Y * atM.X) + (plane.Z * atM.Y);

    static float Normal(float figure) => MathF.Abs(figure) < LeastState ? 0f : figure;
}
