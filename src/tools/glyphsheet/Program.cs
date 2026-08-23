using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TrafficSimulation.Tools.GlyphSheet;

/// <summary>
/// Writes the engine's one typeface: a fixed-pitch sheet of the printable ASCII range, plus one
/// solid cell so that a plain rectangle is a glyph quad like any other.
/// </summary>
/// <remarks>
/// <para>
/// Run it when the face changes and never at build time. The sheet it writes is committed beside the
/// shaders and embedded in the assembly, so the runtime carries no font library, no rasteriser and no
/// file to find. The face is a system font read by name rather than a file shipped here, because the
/// output is what is kept and the input is needed once.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Sixteen columns of the printable range, then one row holding the solid cell. Cell height is
    /// chosen so the sheet is sampled at about one to one at the size panel text is drawn.
    /// </summary>
    const int Columns = 16;

    const int Rows = 7;
    const int CellWidthPx = 16;
    const int CellHeightPx = 24;
    const int FirstChar = 32;
    const int LastChar = 127;

    static int Main(string[] args)
    {
        var faceName = args.Length > 0 ? args[0] : "Liberation Mono";
        var into = args.Length > 1 ? args[1] : DefaultOutput();

        if (!SystemFonts.TryGet(faceName, out var family))
        {
            Console.Error.WriteLine($"No font family '{faceName}' on this machine. Installed: " +
                                    string.Join(", ", SystemFonts.Families.Select(f => f.Name)));
            return 1;
        }

        // Sized to the cell rather than to a point size: a face whose advance overruns the cell would
        // print one glyph into the next one's uv, which reads as a font that smears rather than as a
        // measurement that is wrong.
        var font = FitToCell(family);

        using var sheet = new Image<Rgba32>(Columns * CellWidthPx, Rows * CellHeightPx, new Rgba32(0, 0, 0, 0));
        var options = new RichTextOptions(font)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        for (var code = FirstChar; code < LastChar; code++)
        {
            var cell = code - FirstChar;
            options.Origin = new PointF(
                (cell % Columns + 0.5f) * CellWidthPx,
                (cell / Columns + 0.5f) * CellHeightPx);
            sheet.Mutate(canvas => canvas.DrawText(options, ((char)code).ToString(), Color.White));
        }

        // The solid cell: what every panel, bar, tape and debug line is drawn with, so the whole
        // interface is one pipeline reading one image and nothing branches on what a quad is.
        var lastRow = (LastChar - FirstChar + Columns - 1) / Columns;
        for (var y = lastRow * CellHeightPx; y < (lastRow + 1) * CellHeightPx; y++)
        {
            for (var x = 0; x < CellWidthPx; x++) sheet[x, y] = new Rgba32(255, 255, 255, 255);
        }

        // The disc, one cell along: a round shape is what a collision circle and a network node are,
        // and a cell that already carries one is one quad rather than a fan of segments.
        Disc(sheet, lastRow);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(into))!);
        sheet.SaveAsPng(into);

        Console.WriteLine($"{family.Name} at {font.Size:F1} pt written to {into} — " +
                          $"{sheet.Width}x{sheet.Height}, {Columns}x{Rows} cells of {CellWidthPx}x{CellHeightPx}, " +
                          $"ASCII {FirstChar}..{LastChar - 1}, the solid cell at index {lastRow * Columns} " +
                          $"and the disc at {lastRow * Columns + 1}.");
        return 0;
    }

    /// <summary>
    /// A filled circle in the cell after the solid one, inscribed in the cell's short side and
    /// coverage-antialiased so it does not read as a polygon when it is drawn large.
    /// </summary>
    static void Disc(Image<Rgba32> sheet, int row)
    {
        const float EdgePx = 1f;

        var left = CellWidthPx;
        var top = row * CellHeightPx;
        var centre = new PointF(left + CellWidthPx * 0.5f, top + CellHeightPx * 0.5f);
        var radius = Math.Min(CellWidthPx, CellHeightPx) * 0.5f - EdgePx;

        for (var y = top; y < top + CellHeightPx; y++)
        {
            for (var x = left; x < left + CellWidthPx; x++)
            {
                var distance = MathF.Sqrt(
                    (x + 0.5f - centre.X) * (x + 0.5f - centre.X) + (y + 0.5f - centre.Y) * (y + 0.5f - centre.Y));
                var coverage = Math.Clamp((radius - distance) / EdgePx + 0.5f, 0f, 1f);
                sheet[x, y] = new Rgba32(255, 255, 255, (byte)(coverage * 255f));
            }
        }
    }

    /// <summary>
    /// The largest size at which the widest printable glyph still fits the cell with a texel of
    /// margin — searched rather than assumed, because a metric-compatible face is not a promise
    /// about the advance at a given point size.
    /// </summary>
    static Font FitToCell(FontFamily family)
    {
        var widest = string.Concat(Enumerable.Range(FirstChar, LastChar - FirstChar).Select(c => (char)c));
        var best = family.CreateFont(6f);
        for (var size = 6f; size <= CellHeightPx * 2f; size += 0.5f)
        {
            var font = family.CreateFont(size);
            var measured = TextMeasurer.MeasureAdvance(widest, new TextOptions(font));
            var advance = measured.Width / widest.Length;
            var line = TextMeasurer.MeasureSize("Mg", new TextOptions(font)).Height;
            if (advance > CellWidthPx - 1f || line > CellHeightPx - 1f) break;

            best = font;
        }

        return best;
    }

    /// <summary>The sheet lives in <c>src/runtime/glyphs/</c>, beside the shaders.</summary>
    static string DefaultOutput()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var runtime = Path.Combine(dir.FullName, "runtime", "glyphs");
            if (Directory.Exists(Path.Combine(dir.FullName, "runtime"))) return Path.Combine(runtime, "glyphs.png");
        }

        throw new DirectoryNotFoundException($"No engine root above {AppContext.BaseDirectory}: expected a folder holding runtime/.");
    }
}
