using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Rail;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The level crossings' gear, as instances</b> (RAIL-7): on each approach the barrier as far as it is lowered, the head
/// beside it, and the one of its two lamps that is lit — gear by gear, the arm under the head and the glow over it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A barrier is seen from above</b>: raised it points at the sky and shows nothing past its pivot, and lowered it lies
/// across the lanes it closes, so what is drawn of it is its length as it is foreshortened — the sine of how far down it
/// has swung.
/// </para>
/// <para>
/// <b>Both pictures are laid in code</b> (<see cref="Barrier"/>, <see cref="Head"/>): a red and white arm and a dark box
/// with two lamps, nothing kept under <c>assets/</c>; the lit lamp is the town's own glow, tinted red.
/// </para>
/// </remarks>
internal static class CrossingSprites
{
    /// <summary>How finely the two pictures are laid.</summary>
    const float PixelsPerM = 32f;

    /// <summary>The arm's picture: how long it is drawn, and how many red and white bands it is painted in.</summary>
    const float ArmPictureM = 4f;

    const int Bands = 8;

    /// <summary>A glow is red at the town's own strength.</summary>
    static readonly Vector4 Red = new(1f, 0.12f, 0.08f, 0.9f);

    /// <summary>A glow, as an instance carries its tint.</summary>
    public static uint GlowTint => SpriteTint.Pack(Red);

    /// <summary>
    /// <b>What one approach's gear shows now</b>: how much of its barrier is seen from above, and which lamp is lit (−1 for
    /// neither) — what the host's fill draws and what the device's emit is handed each frame, so the two draw one gear.
    /// </summary>
    public static (float ShownM, int Lit) Shown(CrossingGear gear, int each, float nowS) =>
        (gear.ArmM[each] * MathF.Sin(gear.Lowered(each, nowS) * MathF.PI * 0.5f), gear.LampLit(each, nowS));

    public static int Fill(
        CrossingGear gear, float nowS, RailFigures rail, int barrierSheet, int headSheet, int glowSheet, Vector2 viewCentreM,
        Vector2 viewSpanM, Span<SpriteInstance> into)
    {
        var written = 0;
        var halfSpanM = viewSpanM * 0.5f;
        var glowTint = GlowTint;
        for (var each = 0; each < gear.Count && written + 3 <= into.Length; each++)
        {
            var reachM = gear.ArmM[each] + rail.GearPastKerbM;
            var offM = Vector2.Abs(gear.PivotM[each] - viewCentreM);
            if (offM.X > halfSpanM.X + reachM || offM.Y > halfSpanM.Y + reachM) continue;

            var (shownM, lit) = Shown(gear, each, nowS);
            if (shownM > rail.BarrierWidthM)
            {
                into[written++] = new SpriteInstance(
                    gear.PivotM[each] + (gear.DownAlong[each] * (shownM * 0.5f)), new Vector2(shownM, rail.BarrierWidthM) * 0.5f,
                    SpriteCell.Whole, SpriteTint.Plain, (uint)barrierSheet, gear.ArmHeadingRad[each]);
            }

            var headingRad = gear.HeadHeadingRad[each];
            into[written++] = new SpriteInstance(
                gear.HeadM[each], new Vector2(rail.HeadSizeM * 0.5f), SpriteCell.Whole, SpriteTint.Plain, (uint)headSheet, headingRad);

            if (lit < 0) continue;

            var lampM = lit == 0 ? gear.HeadM[each] - gear.LampAcrossM[each] : gear.HeadM[each] + gear.LampAcrossM[each];
            into[written++] = new SpriteInstance(
                lampM, new Vector2(rail.LampGlowM * 0.5f), SpriteCell.Whole, glowTint, (uint)glowSheet, headingRad);
        }

        return written;
    }

    /// <summary>The most instances one approach's gear draws: its arm, its head and a lit lamp's glow.</summary>
    public const int EachGear = 3;

    /// <summary>A barrier's arm: bands of red and white along it, its pivot's end at −x.</summary>
    public static SheetSource Barrier(RailFigures rail)
    {
        var (widthPx, heightPx) = ((int)(ArmPictureM * PixelsPerM), Math.Max(2, (int)MathF.Ceiling(rail.BarrierWidthM * PixelsPerM)));
        var rgba = new byte[widthPx * heightPx * 4];
        for (var row = 0; row < heightPx; row++)
        {
            for (var column = 0; column < widthPx; column++)
            {
                var red = (column * Bands / widthPx) % 2 == 0;
                var rim = row == 0 || row == heightPx - 1;
                Put(rgba, ((row * widthPx) + column) * 4, rim ? new Vector3(0.25f) : red ? new Vector3(0.82f, 0.1f, 0.08f) : new Vector3(0.95f));
            }
        }

        return SheetSource.Generated(rgba, widthPx, heightPx);
    }

    /// <summary>A crossing's head from above: a dark box with its two lamps' rims across it.</summary>
    public static SheetSource Head(RailFigures rail)
    {
        var sidePx = Math.Max(4, (int)MathF.Ceiling(rail.HeadSizeM * PixelsPerM));
        var lampPx = rail.LampsApartM * 0.5f * PixelsPerM;
        var rgba = new byte[sidePx * sidePx * 4];
        for (var row = 0; row < sidePx; row++)
        {
            for (var column = 0; column < sidePx; column++)
            {
                var atPx = new Vector2(column + 0.5f, row + 0.5f) - new Vector2(sidePx * 0.5f);
                var nearestPx = MathF.Min(Vector2.Distance(atPx, new Vector2(0f, -lampPx)), Vector2.Distance(atPx, new Vector2(0f, lampPx)));
                var lamp = nearestPx < lampPx * 0.6f;
                var edge = column < 2 || row < 2 || column >= sidePx - 2 || row >= sidePx - 2;
                Put(rgba, ((row * sidePx) + column) * 4, edge ? new Vector3(0.9f) : lamp ? new Vector3(0.35f, 0.05f, 0.05f) : new Vector3(0.12f));
            }
        }

        return SheetSource.Generated(rgba, sidePx, sidePx);
    }

    static void Put(byte[] rgba, int texel, Vector3 colour)
    {
        rgba[texel] = (byte)(colour.X * 255f);
        rgba[texel + 1] = (byte)(colour.Y * 255f);
        rgba[texel + 2] = (byte)(colour.Z * 255f);
        rgba[texel + 3] = 255;
    }
}
