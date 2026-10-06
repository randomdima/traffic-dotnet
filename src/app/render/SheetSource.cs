using System.Numerics;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// One picture the sprite pipeline may be told to draw with. Every one is packed into the atlas and
/// sampled clamped and un-mipped, because the texel across a cell boundary belongs to another pose.
/// </summary>
/// <remarks>
/// <para>
/// <b>A brush has no file.</b> The mark brushes are a gradient a few texels tall rather than a
/// picture, so they are built here rather than shipped: an asset would be a file nobody could read
/// and a step somebody would forget to re-import.
/// </para>
/// <para>
/// <b>An underlay is painted when the page is filled</b>, under the art and into a margin padded round
/// it, so the shipped file stays the art alone and nothing holds the composed picture past its upload.
/// </para>
/// </remarks>
internal readonly record struct SheetSource(string? Path, byte[]? Rgba, int WidthPx, int HeightPx, SheetUnderlay? Underlay)
{
    public static SheetSource File(string path) => new(path, null, 0, 0, null);

    /// <summary>The picture at <paramref name="path"/> laid over <paramref name="underlay"/>, at the padded size the underlay asks for.</summary>
    public static SheetSource Underlaid(string path, SheetUnderlay underlay) => new(path, null, 0, 0, underlay);

    /// <summary>An image with no file behind it, as rows of RGBA bytes.</summary>
    public static SheetSource Generated(byte[] rgba, int widthPx, int heightPx) =>
        new(null, rgba, widthPx, heightPx, null);
}

/// <summary>A rectangle of a sheet in texels, top row first, between two corners that may fall between texels.</summary>
internal readonly record struct TexelBox(Vector2 MinPx, Vector2 MaxPx);

/// <summary>
/// Solid rectangles laid under a sheet's art, and the transparent margin the sheet is padded by to hold
/// whatever of them stands past the art's edge.
/// </summary>
/// <param name="PadXPx">Texels added on the left and again on the right; the art stays centred.</param>
/// <param name="PadYPx">And on the top and again on the bottom.</param>
/// <param name="Boxes">In the padded sheet's texels. A texel a box only partly covers is covered by that share.</param>
/// <param name="Colour">What every box is painted in.</param>
internal sealed record SheetUnderlay(int PadXPx, int PadYPx, TexelBox[] Boxes, Texel Colour)
{
    /// <summary>The art's size grown by the margin.</summary>
    public (int Width, int Height) Padded(int artWidth, int artHeight) =>
        (artWidth + (PadXPx * 2), artHeight + (PadYPx * 2));

    /// <summary>The padded sheet: the boxes painted, then the art laid over them by its own alpha.</summary>
    public Texel[] Under(ReadOnlySpan<Texel> art, int artWidth, int artHeight)
    {
        var (width, height) = Padded(artWidth, artHeight);
        var sheet = new Texel[width * height];
        foreach (var box in Boxes) Paint(sheet, width, height, box);

        for (var row = 0; row < artHeight; row++)
        {
            var into = sheet.AsSpan(((row + PadYPx) * width) + PadXPx, artWidth);
            var from = art.Slice(row * artWidth, artWidth);
            for (var column = 0; column < artWidth; column++) into[column] = Over(from[column], into[column]);
        }

        return sheet;
    }

    void Paint(Span<Texel> sheet, int width, int height, TexelBox box)
    {
        var firstRow = Math.Max(0, (int)MathF.Floor(box.MinPx.Y));
        var lastRow = Math.Min(height, (int)MathF.Ceiling(box.MaxPx.Y));
        var firstColumn = Math.Max(0, (int)MathF.Floor(box.MinPx.X));
        var lastColumn = Math.Min(width, (int)MathF.Ceiling(box.MaxPx.X));
        for (var row = firstRow; row < lastRow; row++)
        {
            var down = MathF.Min(row + 1f, box.MaxPx.Y) - MathF.Max(row, box.MinPx.Y);
            for (var column = firstColumn; column < lastColumn; column++)
            {
                var across = MathF.Min(column + 1f, box.MaxPx.X) - MathF.Max(column, box.MinPx.X);
                var alpha = (byte)MathF.Round(Colour.A * down * across);
                if (alpha > sheet[(row * width) + column].A)
                    sheet[(row * width) + column] = Colour with { A = alpha };
            }
        }
    }

    /// <summary>Straight alpha, <paramref name="top"/> over <paramref name="under"/>.</summary>
    static Texel Over(Texel top, Texel under)
    {
        if (top.A == byte.MaxValue || under.A == 0) return top;

        var topShare = top.A / 255f;
        var underShare = under.A / 255f * (1f - topShare);
        var alpha = topShare + underShare;
        if (alpha <= 0f) return default;

        return new Texel(
            Mix(top.R, under.R), Mix(top.G, under.G), Mix(top.B, under.B), (byte)MathF.Round(alpha * 255f));

        byte Mix(byte above, byte below) => (byte)MathF.Round(((above * topShare) + (below * underShare)) / alpha);
    }
}
