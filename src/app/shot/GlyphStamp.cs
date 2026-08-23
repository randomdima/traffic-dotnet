using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Shot;

/// <summary>
/// The interface's own typeface, written into an image on the CPU — what a caption band is lettered
/// with, since nothing composited after the frame has a GPU pipeline behind it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the same sheet the game draws its text from</b> (<see cref="GlyphSheet.Resource"/>), read
/// off the same embedded resource and cut by the same constants, so a caption reads as part of the
/// project rather than as whatever font happened to be installed. A second typeface would also be a
/// second thing to install: this build carries no font dependency at all.
/// </para>
/// <para>
/// The sheet is resized once per text height and kept, because a band writes a few dozen glyphs at two
/// or three sizes and resampling per glyph would be the whole cost of composing a sheet.
/// </para>
/// </remarks>
internal static class GlyphStamp
{
    /// <summary>What is written where a line will not fit, since the sheet carries no ellipsis.</summary>
    const string Cut = "...";

    static readonly Lock Gate = new();

    static readonly Dictionary<int, Image<Rgba32>> Scaled = [];

    /// <summary>How wide one glyph is drawn at a given height — a whole number, so a line is fixed-pitch
    /// and lands on pixel boundaries rather than drifting along itself.</summary>
    public static int AdvancePx(int heightPx) => Math.Max(1, (int)MathF.Round(GlyphSheet.AdvancePx(heightPx)));

    public static int WidthPx(int characters, int heightPx) => characters * AdvancePx(heightPx);

    /// <summary>
    /// The head of <paramref name="text"/> that fits in <paramref name="widthPx"/>, with the last
    /// characters given up to <see cref="Cut"/> when it does not. A caption that overruns its band is
    /// worse than one that stops: the figure it runs into is read as part of it.
    /// </summary>
    public static string Fit(string text, int widthPx, int heightPx)
    {
        var room = widthPx / AdvancePx(heightPx);
        if (room >= text.Length) return text;
        return room <= Cut.Length ? text[..Math.Max(0, room)] : text[..(room - Cut.Length)] + Cut;
    }

    /// <summary>
    /// Write a line at <paramref name="atPx"/> from its top-left, tinted <paramref name="colour"/>.
    /// The glyph sheet is white with an alpha cut into it, so the tint is the whole colour and the
    /// sheet is the coverage.
    /// </summary>
    public static void Write(Image<Rgba32> into, Point atPx, string text, int heightPx, Rgba32 colour)
    {
        var sheet = SheetAt(heightPx);
        var advance = AdvancePx(heightPx);
        var cellPx = new Size(sheet.Width / GlyphSheet.Columns, sheet.Height / GlyphSheet.Rows);

        for (var index = 0; index < text.Length; index++)
        {
            // Where the character's cell starts, asked of the one place the sheet's shape is written
            // down rather than worked out again here.
            var uv = GlyphSheet.UvOf(text[index]);
            var from = new Point((int)MathF.Round(uv.X * sheet.Width), (int)MathF.Round(uv.Y * sheet.Height));
            Blend(into, sheet, from, cellPx, new Point(atPx.X + index * advance, atPx.Y), colour);
        }
    }

    static void Blend(Image<Rgba32> into, Image<Rgba32> sheet, Point from, Size cellPx, Point atPx, Rgba32 colour)
    {
        for (var y = 0; y < cellPx.Height; y++)
        {
            var intoY = atPx.Y + y;
            if (intoY < 0 || intoY >= into.Height) continue;

            for (var x = 0; x < cellPx.Width; x++)
            {
                var intoX = atPx.X + x;
                if (intoX < 0 || intoX >= into.Width) continue;

                var coverage = sheet[from.X + x, from.Y + y].A / 255f;
                if (coverage <= 0f) continue;

                var under = into[intoX, intoY];
                into[intoX, intoY] = new Rgba32(
                    Mix(under.R, colour.R, coverage), Mix(under.G, colour.G, coverage),
                    Mix(under.B, colour.B, coverage), Math.Max(under.A, (byte)(coverage * colour.A)));
            }
        }
    }

    static byte Mix(byte under, byte over, float coverage) => (byte)MathF.Round(under + ((over - under) * coverage));

    /// <summary>
    /// The sheet cut to this text height. The cell is resized to a whole number of pixels either way,
    /// so every glyph is sampled from the same grid the advance steps along.
    /// </summary>
    static Image<Rgba32> SheetAt(int heightPx)
    {
        lock (Gate)
        {
            if (Scaled.TryGetValue(heightPx, out var kept)) return kept;

            using var source = Load();
            var cut = source.Clone(canvas => canvas.Resize(
                AdvancePx(heightPx) * GlyphSheet.Columns, heightPx * GlyphSheet.Rows, KnownResamplers.Bicubic));

            Scaled[heightPx] = cut;
            return cut;
        }
    }

    static Image<Rgba32> Load()
    {
        using var stream = typeof(GlyphStamp).Assembly.GetManifestResourceStream(GlyphSheet.Resource)
                           ?? throw new InvalidOperationException(
                               $"No embedded resource {GlyphSheet.Resource}: did the project file include it?");

        return Image.Load<Rgba32>(stream);
    }
}
