using System.Numerics;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>A car body's tilt on its springs as it is drawn</b> (CAR-56, <c>SprungBody.TiltRad</c>): nose down and right side
/// down, as the body's instance carries it (<see cref="Id"/>) and as what is bolted to the body is placed on it
/// (<see cref="OnBodyM"/>). The tyres are not tilted: they stand where the patches are.
/// </summary>
/// <remarks>
/// <para>
/// <b>The body is drawn as an eye over its middle sees the tilted plane</b>, <c>ViewFigures.CarTiltEyeHeightM</c> up: a
/// corner sunk by <c>s</c> is drawn <c>h/(h + s)</c> of the way out, so the corner its spring let down draws in and the one
/// let up stands out. The vertex stage draws the body so (<c>quad.glsl</c>), and every lamp and the arm's hinge is put
/// where that eye sees its point (<see cref="OnBodyM"/>) — on the host and on the device's emit
/// (<c>sprites.glsl</c>'s <c>spritesOnTiltedBody</c>) alike, to the bit.
/// </para>
/// <para>
/// <b>Each angle a signed byte of <see cref="StepsPerRad"/></b>, which is a power of two so a count and its angle are each
/// other's exactly on both, and fine enough that a step moves a corner a fraction of a pixel; <see cref="MostSteps"/> of
/// them is past what any body rolls.
/// </para>
/// </remarks>
internal static class BodyTilt
{
    public const float StepsPerRad = 512f;

    public const float MostSteps = 127f;

    /// <summary>A tilt as its instance carries it: nose down in the low byte, right side down in the high.</summary>
    public static int Id(Vector2 tiltRad) => (byte)(sbyte)Steps(tiltRad.X) | ((byte)(sbyte)Steps(tiltRad.Y) << 8);

    /// <summary>The tilt the body is drawn at: the one handed in, to a step.</summary>
    public static Vector2 AsDrawn(Vector2 tiltRad) => new Vector2(Steps(tiltRad.X), Steps(tiltRad.Y)) / StepsPerRad;

    /// <summary>
    /// <b>Where a point of the body is drawn once it is tilted</b> (<see cref="AsDrawn"/>), in the body's frame: <c>+x</c>
    /// the nose, <c>+y</c> its right.
    /// </summary>
    public static Vector2 OnBodyM(Vector2 atBodyM, Vector2 drawnTiltRad, float eyeHeightM)
    {
        var sinkM = (atBodyM.X * drawnTiltRad.X) + (atBodyM.Y * drawnTiltRad.Y);
        return atBodyM * (eyeHeightM / (eyeHeightM + sinkM));
    }

    static float Steps(float rad) => Math.Clamp(MathF.Round(rad * StepsPerRad), -MostSteps, MostSteps);
}
