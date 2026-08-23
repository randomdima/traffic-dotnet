using System.Reflection;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Shot;

/// <summary>
/// A review sheet, taken: every cell staged through the one shot path, captioned, tiled and written
/// out with its notes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here draws a town.</b> Each cell is <see cref="ShotRun"/>'s own frame, taken with the
/// figures <see cref="SheetRequest.ForCell"/> resolved — so a sheet cannot show anything a single
/// <c>--shot</c> would not have shown, and a cell that looks wrong is reproduced by one command.
/// </para>
/// <para>
/// The cells are written as files and deleted once they are tiled, which is what makes a failed run
/// leave the frames it did take: a sheet of four that dies on the third leaves two pictures to look at
/// rather than nothing.
/// </para>
/// </remarks>
internal static class SheetRun
{
    /// <summary>
    /// How many distinct colours a cell has to carry before it is a picture of anything, sampled on a
    /// coarse lattice. Below it the cell is reported and still written: a frame that came back flat is
    /// nearly always a staging mistake, but a menu over an empty world is legitimately nearly flat and
    /// refusing to write it would make the sheet unable to photograph the interface.
    /// </summary>
    const int FewColours = 32;

    public static SheetReport Take(SheetRequest ask, SimConfig config, string? outPath)
    {
        var sheetPath = outPath ?? ask.Out ?? throw new ArgumentException(
            "The sheet says nothing about where to write it: give it an \"out\", or name one with --shot PATH.");

        var cells = ask.CellCount;
        var taken = new List<string>(cells);
        var captions = new ShotCaption[cells];
        var census = new SheetCellReport[cells];

        for (var cell = 0; cell < cells; cell++)
        {
            var label = ask.LabelOf(cell);
            var note = ask.NoteOf(cell);
            var path = CellPath(sheetPath, cell, label);
            var shot = ask.ForCell(cell, path);
            var report = ShotRun.Take(shot, config);

            taken.Add(path);
            captions[cell] = ShotCaption.Of(shot, report, label, note);
            census[cell] = SheetCellReport.Of(shot, report, label, note);
            Console.WriteLine(Row(cell, cells, census[cell]));
        }

        using var sheet = Compose(taken, captions, ask.Caption, Path.GetFileName(sheetPath));
        var whole = new SheetReport(
            sheetPath, ask.Title, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), Build(), sheet.Width,
            sheet.Height, census);

        Write(sheet, sheetPath, whole);
        foreach (var cell in taken) File.Delete(cell);
        return whole;
    }

    /// <summary>
    /// The band and the notes put on a frame that has already been taken — what <c>--shot --caption</c>
    /// does, so a single picture and a cell of a sheet carry the same provenance in the same places.
    /// </summary>
    public static SheetReport Annotate(in ShotRequest ask, in ShotReport report, string? title, string? note)
    {
        var caption = ShotCaption.Of(ask, report, title, note);
        using var frame = Image.Load<Rgba32>(report.Path);
        using var captioned = Caption.Under(frame, caption, Caption.Rows(caption));

        var whole = new SheetReport(
            report.Path, title, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), Build(), captioned.Width,
            captioned.Height, [SheetCellReport.Of(ask, report, title, note)]);

        Write(captioned, report.Path, whole);
        return whole;
    }

    static Image<Rgba32> Compose(
        IReadOnlyList<string> cellPaths, ShotCaption[] captions, bool captioned, string named)
    {
        // One row count for every cell, or the bands would be different heights and the cells could
        // not be tiled at all.
        var rows = 0;
        foreach (var caption in captions) rows = Math.Max(rows, Caption.Rows(caption));

        var cells = new List<Image<Rgba32>>(cellPaths.Count);
        try
        {
            for (var cell = 0; cell < cellPaths.Count; cell++)
            {
                using var frame = Image.Load<Rgba32>(cellPaths[cell]);
                if (Colours(frame) < FewColours)
                    Console.Error.WriteLine(
                        $"{cellPaths[cell]} carries fewer than {FewColours} colours — check what it was staged on");

                cells.Add(captioned ? Caption.Under(frame, captions[cell], rows) : frame.Clone());
            }

            return Sheet.Of(cells, named);
        }
        finally
        {
            foreach (var cell in cells) cell.Dispose();
        }
    }

    static void Write(Image<Rgba32> sheet, string path, SheetReport report)
    {
        ShotNotes.Attach(sheet, report);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        sheet.SaveAsPng(path);
        ShotNotes.WriteBeside(path, report);
    }

    /// <summary>Where a cell is staged before it is tiled — beside the sheet, named after it and after
    /// what the cell is of, so a run that fails part way leaves files anybody can read.</summary>
    static string CellPath(string sheetPath, int cell, string? label)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(sheetPath))!;
        var stem = Path.GetFileNameWithoutExtension(sheetPath);
        var named = Safe(label) is { Length: > 0 } safe ? safe : $"{cell + 1}";
        return Path.Combine(folder, $"{stem}-cell-{cell + 1}-{named}.png");
    }

    static string Safe(string? label)
    {
        if (label is null) return string.Empty;

        var safe = label.ToCharArray();
        for (var at = 0; at < safe.Length; at++)
            if (!char.IsAsciiLetterOrDigit(safe[at]))
                safe[at] = '-';

        return new string(safe).Trim('-');
    }

    static string Row(int cell, int cells, SheetCellReport census) =>
        $"{$"cell {cell + 1}/{cells}",-11}{census.Label ?? census.Map}: {census.WidthPx}x{census.HeightPx} px, " +
        $"{census.SpanM[0]:F0}x{census.SpanM[1]:F0} m at {census.CentreM[0]:F0},{census.CentreM[1]:F0}, " +
        $"{census.PxPerM:F1} px/m, tick {census.Tick}, {census.Sprites} bodies, {census.Triangles} triangles";

    /// <summary>How many distinct colours a frame carries, sampled coarsely — enough to tell a picture
    /// from a flat fill, and cheap enough to run on every cell.</summary>
    static int Colours(Image<Rgba32> frame)
    {
        var seen = new HashSet<uint>();
        for (var y = 0; y < frame.Height; y += 8)
            for (var x = 0; x < frame.Width; x += 8)
                seen.Add(frame[x, y].PackedValue);

        return seen.Count;
    }

    /// <summary>
    /// What took the picture. The configuration is in it because a frame's figures mean different
    /// things off a Debug build, and a sheet outlives the shell it was taken from.
    /// </summary>
    static string Build()
    {
        var name = Assembly.GetExecutingAssembly().GetName();
        var configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif
        return $"{name.Name} {name.Version} ({configuration}) on {RuntimeInformation.FrameworkDescription}";
    }
}
