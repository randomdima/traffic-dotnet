using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>A bridge's art as the one array the ground samples it from</b> (TER-3b.2, RAIL-8): a layer a strip — the walkway,
/// the carriageway, the joint, the parapet, a rail bridge's side — and then one layer of the fittings side by side, every
/// picture resampled to the
/// layer's size. A strip's corners file where along and across it they stand (<see cref="GroundVertex.OnTheStrip"/>),
/// and its surface says which layer it reads (<see cref="Surface.BridgeWalkway"/> onward).
/// </summary>
/// <remarks>
/// <para>
/// <b>One binding and not one a picture</b>: a browser's stage samples sixteen textures at most and the town's ground
/// already takes eleven of them. The layers are one size by construction, which is what an array asks. <b>And no more
/// strips</b>: a corner files its surface in four bits beside its fade's (<see cref="GroundVertex.Fading"/>), and the
/// fittings' layer is the last of the sixteen.
/// </para>
/// <para>
/// <b>A fitting is a cell of its layer</b>, the cells an eighth of the picture apiece so their edges fall on whole steps of
/// what a corner files along a strip, and each fitting is drawn inside an inset of its cell so a filtered or mipped
/// sample at its rim reads the clear border rather than the next fitting.
/// </para>
/// </remarks>
internal static class BridgeSheet
{
    public const int LayerWidthPx = 1024;
    public const int LayerHeightPx = 256;

    /// <summary>The fittings' layer, after the four strips'.</summary>
    public const int FittingsLayer = (int)BridgeLookKind.Lamp;

    public const int Layers = FittingsLayer + 1;

    const int FittingCells = 8;

    /// <summary>How far inside its cell a fitting is drawn, in the steps a corner files a strip's length in.</summary>
    const uint InsetAlongSteps = 2;

    /// <summary>And across, in the steps it files the width in.</summary>
    const uint InsetAcrossSteps = 16;

    /// <summary>
    /// <b>The rectangle of the fittings' layer a fitting is drawn in</b>, as a share of the picture along and across:
    /// each edge on a step a strip's corner files exactly, so the corners laid at it read the drawing edge to edge.
    /// </summary>
    public static (float AlongFrom, float AlongTo, float AcrossFrom, float AcrossTo) CellOf(BridgeLookKind fitting)
    {
        var cell = (uint)(fitting - BridgeLookKind.Lamp);
        const uint cellSteps = GroundVertex.TrackStepsPerPicture / FittingCells;
        const float acrossSteps = GroundVertex.AcrossSteps;
        return (
            ((cell * cellSteps) + InsetAlongSteps) / (float)GroundVertex.TrackStepsPerPicture,
            (((cell + 1) * cellSteps) - InsetAlongSteps) / (float)GroundVertex.TrackStepsPerPicture,
            InsetAcrossSteps / acrossSteps,
            (acrossSteps - InsetAcrossSteps) / acrossSteps);
    }

    /// <summary>Every layer, top row first, one after another: what <see cref="BridgeLooks.Shared"/> names, laid out.</summary>
    public static Texel[] Compose()
    {
        var looks = BridgeLooks.Shared;
        var all = new Texel[LayerWidthPx * LayerHeightPx * Layers];
        for (var kind = BridgeLookKind.Walkway; kind <= BridgeLookKind.RailParapet; kind++)
        {
            var layer = all.AsSpan((int)kind * LayerWidthPx * LayerHeightPx, LayerWidthPx * LayerHeightPx);
            Resampled(looks[kind].SpritePath, layer, 0, 0, LayerWidthPx, LayerHeightPx, wraps: true);
        }

        var fittings = all.AsSpan(FittingsLayer * LayerWidthPx * LayerHeightPx, LayerWidthPx * LayerHeightPx);
        for (var kind = BridgeLookKind.Lamp; kind <= BridgeLookKind.RailEndPost; kind++)
        {
            var (alongFrom, alongTo, acrossFrom, acrossTo) = CellOf(kind);
            var x0 = (int)MathF.Round(alongFrom * LayerWidthPx);
            var y0 = (int)MathF.Round(acrossFrom * LayerHeightPx);
            Resampled(
                looks[kind].SpritePath, fittings, x0, y0, (int)MathF.Round(alongTo * LayerWidthPx) - x0,
                (int)MathF.Round(acrossTo * LayerHeightPx) - y0, wraps: false);
        }

        return all;
    }

    /// <summary>
    /// One picture into a rectangle of a layer, bilinear and weighted by cover, so a clear texel lends a neighbour none of its
    /// black. A strip <paramref name="wraps"/> along its length, so its ends sample each other as they meet when laid.
    /// </summary>
    static void Resampled(string path, Span<Texel> layer, int x0, int y0, int widthPx, int heightPx, bool wraps)
    {
        var (sourceWidth, sourceHeight) = ImageHeader.Measure(path);
        var source = new Texel[sourceWidth * sourceHeight];
        Texels.Decode(path, source);

        for (var y = 0; y < heightPx; y++)
        {
            var sy = Math.Clamp(((y + 0.5f) * sourceHeight / heightPx) - 0.5f, 0f, sourceHeight - 1f);
            var row0 = (int)sy;
            var row1 = Math.Min(row0 + 1, sourceHeight - 1);
            var fy = sy - row0;
            for (var x = 0; x < widthPx; x++)
            {
                var sx = ((x + 0.5f) * sourceWidth / widthPx) - 0.5f;
                int column0, column1;
                float fx;
                if (wraps)
                {
                    var wrapped = sx < 0f ? sx + sourceWidth : sx;
                    column0 = (int)wrapped % sourceWidth;
                    column1 = (column0 + 1) % sourceWidth;
                    fx = wrapped - MathF.Floor(wrapped);
                }
                else
                {
                    sx = Math.Clamp(sx, 0f, sourceWidth - 1f);
                    column0 = (int)sx;
                    column1 = Math.Min(column0 + 1, sourceWidth - 1);
                    fx = sx - column0;
                }

                layer[((y0 + y) * LayerWidthPx) + x0 + x] = Mixed(
                    source[(row0 * sourceWidth) + column0], source[(row0 * sourceWidth) + column1],
                    source[(row1 * sourceWidth) + column0], source[(row1 * sourceWidth) + column1], fx, fy);
            }
        }
    }

    static Texel Mixed(Texel a, Texel b, Texel c, Texel d, float fx, float fy)
    {
        var wa = (1f - fx) * (1f - fy) * a.A;
        var wb = fx * (1f - fy) * b.A;
        var wc = (1f - fx) * fy * c.A;
        var wd = fx * fy * d.A;
        var cover = wa + wb + wc + wd;
        if (cover <= 0f) return default;

        return new Texel(
            (byte)MathF.Round(((wa * a.R) + (wb * b.R) + (wc * c.R) + (wd * d.R)) / cover),
            (byte)MathF.Round(((wa * a.G) + (wb * b.G) + (wc * c.G) + (wd * d.G)) / cover),
            (byte)MathF.Round(((wa * a.B) + (wb * b.B) + (wc * c.B) + (wd * d.B)) / cover),
            (byte)MathF.Round(cover));
    }
}
