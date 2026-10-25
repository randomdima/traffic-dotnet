using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>One fan a picture draws</b> — a diesel's radiator fan, a roof's condenser — as the art drew it, cut off it with
/// <c>qq props</c>: its hub, from the picture's middle (<c>+y</c> down the picture), and its rim, the ellipse square to the
/// picture its opening was drawn as — about <see cref="RimAtM"/>, <see cref="RimM"/> half across each way.
/// </summary>
/// <remarks>
/// <b>The art may draw a fan squashed or a little off its hub, and a fan turned as drawn wobbles</b>: what turns is the
/// largest circle about the hub inside the rim, and the rest is left as drawn (<c>FanTable</c>).
/// </remarks>
internal readonly record struct DrawnFan(Vector2 AtM, Vector2 RimAtM, Vector2 RimM)
{
    /// <summary>How far along <paramref name="unit"/> the rim stands from the hub: where a ray out of it leaves the ellipse.</summary>
    public float RimReachM(Vector2 unit)
    {
        var from = (AtM - RimAtM) / RimM;
        var along = unit / RimM;
        var a = Vector2.Dot(along, along);
        var b = Vector2.Dot(from, along);
        var c = Vector2.Dot(from, from) - 1f;
        return (-b + MathF.Sqrt(MathF.Max(0f, (b * b) - (a * c)))) / a;
    }

    /// <summary>
    /// The rim's longer half-axis and the hub's offset from its middle, which no point of the rim is further from the hub
    /// than — a hair past its farthest reach where the hub is off the middle.
    /// </summary>
    public float FarthestReachM => Vector2.Distance(AtM, RimAtM) + MathF.Max(RimM.X, RimM.Y);

    /// <summary>
    /// Whether it is a fan its picture can hold: a rim of some size, the hub inside it, and all of the rim on a picture
    /// <paramref name="footprintM"/> across.
    /// </summary>
    public bool FitsOn(Vector2 footprintM) =>
        RimM.X > 0f && RimM.Y > 0f && ((AtM - RimAtM) / RimM).LengthSquared() < 1f
        && MathF.Abs(AtM.X) + FarthestReachM <= footprintM.X * 0.5f && MathF.Abs(AtM.Y) + FarthestReachM <= footprintM.Y * 0.5f;
}
