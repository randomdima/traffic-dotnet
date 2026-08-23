using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TrafficSimulation.App.Shot;

/// <summary>
/// Several frames tiled into a single picture, in reading order, separated by gutters in a colour the
/// town never draws (SHT-3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tiling does not save pixels</b> — the cost of a frame is its area, so four quarter-size cells
/// cost what four quarter-size frames cost. What a sheet buys is <i>one</i> reading over N subjects:
/// the question is asked once and answered against every cell, and the cells are seen side by side,
/// which is the only way "each of these is individually correct, and they are the same kind of thing"
/// can be asked at all.
/// </para>
/// <para>
/// It is the reference implementation's format too, so a sheet here is compared against a sheet there,
/// cell for cell, rather than against a layout nobody drew.
/// </para>
/// <para>
/// The gutter is magenta because the town has no magenta in it: a cell boundary can then never be
/// mistaken for something in a picture. A sheet that comes back as bare gutter is a real failure mode
/// — the reference build once produced one, of the right name and the right size, containing no
/// picture at all — so nothing here writes one.
/// </para>
/// </remarks>
internal static class Sheet
{
    /// <summary>The gutter between cells, and what it is filled with.</summary>
    public const int GutterPx = 6;

    static readonly Rgba32 Gutter = new(255, 0, 255);

    /// <summary>As square a grid as the cells make: two abreast for two, three or four, three beyond.</summary>
    public static int Columns(int cells) => cells <= 1 ? 1 : cells <= 4 ? 2 : 3;

    /// <summary>The size a sheet of this many cells comes to.</summary>
    public static (int WidthPx, int HeightPx) SizeOf(int cells, int cellWidthPx, int cellHeightPx)
    {
        var columns = Columns(cells);
        var rows = (cells + columns - 1) / columns;
        return (columns * cellWidthPx + ((columns - 1) * GutterPx),
                rows * cellHeightPx + ((rows - 1) * GutterPx));
    }

    /// <summary>
    /// Tile the cell files into one image and write it to <paramref name="sheetPath"/>. The cell files
    /// are deleted afterwards: what a review looks at is the sheet, and a stray half-set of loose cells
    /// beside it is a second version of the same picture.
    /// </summary>
    public static void Tile(IReadOnlyList<string> cellPaths, string sheetPath)
    {
        var cells = new List<Image<Rgba32>>(cellPaths.Count);
        try
        {
            foreach (var path in cellPaths) cells.Add(Image.Load<Rgba32>(path));
            using var sheet = Of(cells, Path.GetFileName(sheetPath));
            sheet.Save(sheetPath);
        }
        finally
        {
            foreach (var cell in cells) cell.Dispose();
        }

        foreach (var cell in cellPaths) File.Delete(cell);
    }

    /// <summary>
    /// The cells laid out as one image, which the caller owns. Every cell must be the same size — a
    /// sheet whose cells were photographed at different framings is a comparison nobody can make.
    /// </summary>
    /// <param name="named">What the sheet will be called, for the message a failure carries.</param>
    public static Image<Rgba32> Of(IReadOnlyList<Image<Rgba32>> cells, string named)
    {
        var first = cells[0];
        var (widthPx, heightPx) = SizeOf(cells.Count, first.Width, first.Height);
        var columns = Columns(cells.Count);

        var sheet = new Image<Rgba32>(widthPx, heightPx);
        sheet.Mutate(canvas => canvas.BackgroundColor(Color.FromPixel(Gutter)));

        for (var cell = 0; cell < cells.Count; cell++)
        {
            var image = cells[cell];
            if (image.Width != first.Width || image.Height != first.Height)
            {
                sheet.Dispose();
                throw new InvalidOperationException(
                    $"cell {cell} of {named} is {image.Width}x{image.Height} where the sheet's cells are "
                    + $"{first.Width}x{first.Height}");
            }

            var at = new Point(
                cell % columns * (first.Width + GutterPx),
                cell / columns * (first.Height + GutterPx));
            sheet.Mutate(canvas => canvas.DrawImage(image, at, 1f));
        }

        if (!IsBareGutter(sheet)) return sheet;

        sheet.Dispose();
        throw new InvalidOperationException($"{named} came back as bare gutter — no cell was drawn into it");
    }

    /// <summary>Is this nothing but gutter? Sampled on a coarse lattice: a sheet with one cell in it
    /// fails this test at the first sample inside that cell.</summary>
    static bool IsBareGutter(Image<Rgba32> sheet)
    {
        for (var y = 0; y < sheet.Height; y += 16)
            for (var x = 0; x < sheet.Width; x += 16)
                if (sheet[x, y] != Gutter)
                    return false;

        return true;
    }
}
