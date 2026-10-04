using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>A prefab with no picture yet, drawn as a plain block of its look</b> (GEN-57): its rounded rectangle filled in
/// the look's colour and clear past its corners, its walls a shade darker, and the wall its door is on lighter — so
/// where a traced town's prefabs stand, what each is, how round and which way it faces can be read before any of them
/// is drawn.
/// </summary>
/// <remarks>
/// Built rather than shipped, as a brush is (<see cref="SheetSource"/>): it stands in for art that does not exist,
/// and a file of it would be a picture somebody had to remember to delete.
/// </remarks>
internal static class PrefabSprites
{
    /// <summary>How finely a block is drawn: enough for a wall to read at a street's framing, and no more.</summary>
    const float PixelsPerM = 4f;

    /// <summary>How thick its walls are drawn.</summary>
    const float WallM = 0.5f;

    /// <summary>How far in from the door's wall the strip that marks it reaches.</summary>
    const float DoorStripM = 1.5f;

    const float WallShade = 0.6f;
    const float DoorLighter = 1.3f;

    /// <summary>
    /// One colour a look, in <see cref="BuildingLook"/>'s order: warm for homes, cool for work, bright for the few
    /// that stand out — and none of them the grey a survey's footprint is drawn in under them.
    /// </summary>
    static readonly Vector3[] LookColour =
    [
        new(0.78f, 0.35f, 0.28f), // house
        new(0.86f, 0.68f, 0.34f), // apartments
        new(0.42f, 0.52f, 0.78f), // tower
        new(0.36f, 0.36f, 0.40f), // garages
        new(0.52f, 0.40f, 0.28f), // shed
        new(0.86f, 0.35f, 0.72f), // kiosk
        new(0.96f, 0.50f, 0.14f), // retail
        new(0.22f, 0.64f, 0.68f), // office
        new(0.56f, 0.40f, 0.70f), // industrial
        new(0.34f, 0.70f, 0.30f), // school
        new(0.96f, 0.82f, 0.88f), // hospital
        new(0.98f, 0.86f, 0.18f), // religious
        new(0.84f, 0.84f, 0.80f), // canopy
        new(0.52f, 0.92f, 0.90f), // greenhouse
    ];

    public static SheetSource Plain(in BuildingVariant variant)
    {
        var colour = LookColour[(int)(variant.Look ?? BuildingLook.House)];
        var widthPx = Math.Max(2, (int)MathF.Ceiling(variant.FootprintM.X * PixelsPerM));
        var heightPx = Math.Max(2, (int)MathF.Ceiling(variant.FootprintM.Y * PixelsPerM));
        var wallPx = WallM * PixelsPerM;
        var doorPx = DoorStripM * PixelsPerM;
        var halfPx = new Vector2(widthPx, heightPx) * 0.5f;
        var cornerPx = MathF.Min(variant.CornerRadiusM * PixelsPerM, MathF.Min(halfPx.X, halfPx.Y));

        var rgba = new byte[widthPx * heightPx * 4];
        for (var row = 0; row < heightPx; row++)
        {
            for (var column = 0; column < widthPx; column++)
            {
                var inPx = -OutsidePx(new Vector2(column + 0.5f, row + 0.5f) - halfPx, halfPx, cornerPx);
                var texel = ((row * widthPx) + column) * 4;
                if (inPx <= 0f) continue;

                var shade = inPx < wallPx ? WallShade : heightPx - row - 0.5f < doorPx ? DoorLighter : 1f;
                rgba[texel + 0] = Byte(colour.X * shade);
                rgba[texel + 1] = Byte(colour.Y * shade);
                rgba[texel + 2] = Byte(colour.Z * shade);
                rgba[texel + 3] = Byte(MathF.Min(inPx, 1f));
            }
        }

        return SheetSource.Generated(rgba, widthPx, heightPx);
    }

    /// <summary>How far a point stands outside a rounded rectangle about the origin, negative inside it.</summary>
    static float OutsidePx(Vector2 atPx, Vector2 halfPx, float cornerPx)
    {
        var past = Vector2.Abs(atPx) - (halfPx - new Vector2(cornerPx));
        return Vector2.Max(past, Vector2.Zero).Length() + MathF.Min(MathF.Max(past.X, past.Y), 0f) - cornerPx;
    }

    static byte Byte(float share) => (byte)Math.Clamp(share * 255f, 0f, 255f);
}
