using System.Numerics;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.PixelFormats;

namespace TrafficSimulation.App.Shot;

/// <summary>
/// What a review picture was a picture of, written where a machine can read it: the PNG's own text
/// chunks, and a report beside the file (SHT-5).
/// </summary>
/// <remarks>
/// <b>The caption and these notes are the same figures.</b> The band is for whoever is looking at the
/// picture and the notes are for whatever is reading it afterwards — a diff, a script, or the next
/// session asking what framing yesterday's sheet was taken at. Neither is allowed to be the only copy,
/// because a picture separated from its provenance is a picture nobody can retake.
/// </remarks>
internal static class ShotNotes
{
    /// <summary>The PNG text chunks, keyed as the format's own convention has them.</summary>
    public static void Attach(Image<Rgba32> image, SheetReport report)
    {
        var notes = image.Metadata.GetPngMetadata().TextData;
        notes.Add(new PngTextData("Software", report.Build, string.Empty, string.Empty));
        notes.Add(new PngTextData("Creation Time", report.TakenUtc, string.Empty, string.Empty));
        if (report.Title is { Length: > 0 } title)
            notes.Add(new PngTextData("Title", title, string.Empty, string.Empty));

        // The whole report, so a sheet that has been moved away from its sidecar still says what it is.
        notes.Add(new PngTextData("Description", Written(report), string.Empty, string.Empty));
    }

    /// <summary>The report as a file beside the sheet, which is what a script reads.</summary>
    /// <remarks>
    /// <b>It is the picture's whole name with <c>.json</c> after it</b>, never its extension swapped:
    /// a sheet asked for as <c>junctions.json</c> writes <c>junctions.png</c>, and notes named by
    /// swapping the extension would overwrite the document that asked for them.
    /// </remarks>
    public static string WriteBeside(string sheetPath, SheetReport report)
    {
        var path = NotesFor(sheetPath);
        File.WriteAllText(path, Written(report));
        return path;
    }

    public static string NotesFor(string sheetPath) => sheetPath + ".json";

    static string Written(SheetReport report) => JsonSerializer.Serialize(report, Format);

    static readonly JsonSerializerOptions Format = new() { WriteIndented = true };
}

/// <summary>What a sheet turned out to be: the run that took it, and the census of every cell.</summary>
internal sealed record SheetReport(
    string Sheet,
    string? Title,
    string TakenUtc,
    string Build,
    int WidthPx,
    int HeightPx,
    SheetCellReport[] Cells);

/// <summary>
/// One cell's census. <b>Everything needed to ask for the same picture again is here</b> — the map,
/// the framing, the moment and the seed — beside what the frame cost to draw.
/// </summary>
internal sealed record SheetCellReport(
    string? Label,
    string? Note,
    string Map,
    string[] Ui,
    int WidthPx,
    int HeightPx,
    float[] SpanM,
    float[] CentreM,
    float PxPerM,
    long Tick,
    double Seconds,
    ulong Seed,
    int Triangles,
    int Sprites,
    int SpriteCapacity,
    int InterfaceQuads,
    long Crossings)
{
    public static SheetCellReport Of(in ShotRequest ask, in ShotReport report, string? label, string? note) => new(
        label, note, report.Map, ask.Ui ?? [], report.WidthPx, report.HeightPx,
        Pair(report.SpanM), Pair(report.CentreM), report.PxPerM, report.Tick, ask.Seconds, report.Seed,
        report.Triangles, report.Sprites, report.SpriteCapacity, report.InterfaceQuads, report.Crossings);

    static float[] Pair(Vector2 point) => [point.X, point.Y];
}
