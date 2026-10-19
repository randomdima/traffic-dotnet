using System.Numerics;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The trains, as instances</b> (TRN-4): one quad a wagon in the world, turned to the heading its body is at, drawn in
/// the look its train put on it — a wagon's art is one frame with its front along <c>+x</c>, as a car's is.
/// </summary>
/// <remarks>
/// <b>A vehicle with no art yet is drawn plain</b> (<see cref="Plain"/>): its own shape in its kind's colour, a roof a
/// shade lighter inside a darker rim — laid in code and nothing kept under <c>assets/</c>, so nothing has to trace to a
/// prompt until its picture is generated and kept.
/// </remarks>
internal static class TrainSprites
{
    /// <summary>How finely a plain vehicle is drawn: the ground's own art grid.</summary>
    const float PlainPixelsPerM = 8f;

    /// <summary>How wide a plain vehicle's rim is, and how far its roof's stripe stands in from each side.</summary>
    const float RimM = 0.25f;

    /// <summary>A plain vehicle's colour, by kind: a blue locomotive, a green one, green coaches, black tanks, coal-black wagons.</summary>
    static readonly Vector3[] KindColour =
    [
        new(0.16f, 0.32f, 0.62f), new(0.18f, 0.42f, 0.22f), new(0.24f, 0.46f, 0.30f), new(0.12f, 0.12f, 0.13f),
        new(0.32f, 0.28f, 0.24f),
    ];

    /// <summary>A vehicle with no picture, drawn in code (see the remarks).</summary>
    public static SheetSource Plain(in TrainVehicle vehicle)
    {
        var colour = KindColour[(int)vehicle.Kind];
        var widthPx = Math.Max(2, (int)MathF.Ceiling(vehicle.FootprintM.X * PlainPixelsPerM));
        var heightPx = Math.Max(2, (int)MathF.Ceiling(vehicle.FootprintM.Y * PlainPixelsPerM));
        var rimPx = RimM * PlainPixelsPerM;
        var rgba = new byte[widthPx * heightPx * 4];
        for (var row = 0; row < heightPx; row++)
        {
            for (var column = 0; column < widthPx; column++)
            {
                var edgePx = MathF.Min(MathF.Min(column + 0.5f, widthPx - column - 0.5f), MathF.Min(row + 0.5f, heightPx - row - 0.5f));
                var shade = edgePx < rimPx ? 0.55f : edgePx < 3f * rimPx ? 1.25f : 1f;
                var texel = ((row * widthPx) + column) * 4;
                rgba[texel + 0] = Byte(colour.X * shade);
                rgba[texel + 1] = Byte(colour.Y * shade);
                rgba[texel + 2] = Byte(colour.Z * shade);
                rgba[texel + 3] = 255;
            }
        }

        return SheetSource.Generated(rgba, widthPx, heightPx);
    }

    /// <summary>The sheet a vehicle is drawn from: its picture, or a plain one where it has none yet.</summary>
    public static SheetSource Of(in TrainVehicle vehicle) => vehicle.SpritePath is { } path ? SheetSource.File(path) : Plain(vehicle);

    /// <summary>
    /// Every wagon in the world the view can see on one level, in roster order: on a bridge or off one
    /// (<paramref name="onABridge"/>), as a car is drawn in the run its level is (RAIL-8, TER-7b).
    /// </summary>
    public static int Fill(
        ReadOnlySpan<WagonView> wagons, TrainCatalog catalog, int firstSheet, Vector2 viewCentreM, Vector2 viewSpanM,
        bool onABridge, Span<SpriteInstance> into)
    {
        var written = 0;
        var halfSpanM = viewSpanM * 0.5f;
        foreach (ref readonly var wagon in wagons)
        {
            if (!wagon.IsInWorld || wagon.OnABridge != onABridge || written == into.Length) continue;

            ref readonly var vehicle = ref catalog.Vehicles[wagon.Look];
            var reachM = vehicle.FootprintM.X * 0.5f;
            var offM = Vector2.Abs(wagon.PositionM - viewCentreM);
            if (offM.X > halfSpanM.X + reachM || offM.Y > halfSpanM.Y + reachM) continue;

            into[written++] = new SpriteInstance(
                wagon.PositionM, vehicle.FootprintM * 0.5f, SpriteCell.Whole, SpriteTint.Plain, (uint)(firstSheet + wagon.Look),
                wagon.HeadingRad);
        }

        return written;
    }

    static byte Byte(float channel) => (byte)Math.Clamp(MathF.Round(channel * 255f), 0f, 255f);
}
