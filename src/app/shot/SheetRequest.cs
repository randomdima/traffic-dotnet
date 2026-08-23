using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrafficSimulation.App.Shot;

/// <summary>
/// A review sheet as a document: what to photograph, from where, and what each cell is of (SHT-4).
/// <c>--sheet FILE.json</c> reads one, and <c>--sheet -</c> reads one off standard input.
/// </summary>
/// <remarks>
/// <para>
/// <b>The figures on this record are the request's defaults and a cell overrides what it differs
/// in.</b> Four framings of one junction are then four lines rather than four copies of a command
/// line, and what the cells have in common is stated once and cannot drift between them — which is
/// the whole reason a sheet is asked for as a document instead of as flags.
/// </para>
/// <para>
/// A member the schema does not carry is an <b>error</b> rather than a silent default: a misspelt
/// <c>secondes</c> that quietly photographed tick zero is the failure this format exists to make
/// impossible.
/// </para>
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SheetRequest
{
    /// <summary>How many cells a sheet may hold before it stops being one picture anybody reads.</summary>
    public const int MostCells = 9;

    static readonly JsonSerializerOptions Format = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Ignored, and accepted so an editor can be pointed at a schema without the read failing.</summary>
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    /// <summary>What the sheet as a whole is of. It heads a single-cell sheet and labels nothing else.</summary>
    public string? Title { get; init; }

    /// <summary>Where the sheet is written. <c>--shot PATH</c> stands in for it when the document omits it.</summary>
    public string? Out { get; init; }

    public string? Map { get; init; }

    /// <summary>One cell's size in pixels, <c>[width, height]</c> — the sheet's own size falls out of the grid.</summary>
    public int[]? Size { get; init; }

    /// <summary>The span across a cell's <b>short</b> side, in metres, as <c>--view</c> asks for it.</summary>
    public float View { get; init; }

    public float[]? At { get; init; }

    public string[]? Ui { get; init; }

    public double Seconds { get; init; }

    public float UiScale { get; init; }

    public bool Validate { get; init; }

    /// <summary>Every pair of points a tape is laid between, as <c>[x1, y1, x2, y2]</c>.</summary>
    public float[][]? Rule { get; init; }

    /// <summary>What a reviewer is being asked to look at. Drawn in the caption and kept in the notes.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Off for a picture that is going to be compared against another build's frame pixel for pixel;
    /// on for everything a person or an agent reads.
    /// </summary>
    public bool Caption { get; init; } = true;

    /// <summary>The cells, in reading order. Absent is a sheet of one cell: the defaults above, alone.</summary>
    public SheetCell[]? Cells { get; init; }

    public int CellCount => Cells is { Length: > 0 } cells ? cells.Length : 1;

    public (int WidthPx, int HeightPx) SizePx =>
        Size is { Length: 2 } size ? (size[0], size[1]) : (800, 600);

    /// <summary>
    /// The document, read and checked. <paramref name="path"/> is a file, or <c>-</c> for standard
    /// input — an agent staging a sheet has the request in hand and no reason to leave a file behind.
    /// </summary>
    public static SheetRequest Read(string path) => Parse(
        path == "-" ? Console.In.ReadToEnd() : File.ReadAllText(path),
        path == "-" ? "the sheet on standard input" : path);

    /// <summary>The document itself, checked. <paramref name="named"/> is what a complaint calls it.</summary>
    public static SheetRequest Parse(string text, string named)
    {
        SheetRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<SheetRequest>(text, Format);
        }
        catch (JsonException broken)
        {
            throw new ArgumentException($"{named} is not a sheet this build can read: {broken.Message}", broken);
        }

        if (request is null) throw new ArgumentException($"{named} is empty.");

        request.Check(named);
        return request;
    }

    /// <summary>
    /// What this cell is called — its own label, or its place in the reading order. A sheet of one
    /// cell falls back to the sheet's title instead: "1" labels nothing when there is nothing to
    /// tell it apart from.
    /// </summary>
    public string? LabelOf(int cell)
    {
        if (Cells is not { Length: > 0 } cells) return Title;
        return cells[cell].Label ?? (cells.Length > 1 ? $"{cell + 1}" : Title);
    }

    public string? NoteOf(int cell) => (Cells is null ? null : Cells[cell].Note) ?? Note;

    /// <summary>
    /// One cell as the shot path's own request: the sheet's figures, with whatever the cell overrode.
    /// </summary>
    public ShotRequest ForCell(int cell, string path)
    {
        var over = Cells is null ? new SheetCell() : Cells[cell];
        var (widthPx, heightPx) = SizePx;
        var map = over.Map ?? Map ?? throw new ArgumentException(
            $"cell {cell + 1} names no map and the sheet has no default: add \"map\" to one or the other.");

        return new ShotRequest(
            Map: map,
            Path: path,
            WidthPx: widthPx,
            HeightPx: heightPx,
            ViewM: over.View ?? View,
            AtM: Point(over.At) ?? Point(At),
            Ui: over.Ui ?? Ui,
            UiScale: UiScale,
            Seconds: over.Seconds ?? Seconds,
            RulerPointsM: Tape(over.Rule ?? Rule),
            Validate: Validate);
    }

    void Check(string named)
    {
        if (Size is not (null or { Length: 2 }) || SizePx.WidthPx <= 0 || SizePx.HeightPx <= 0)
            throw new ArgumentException($"{named}: \"size\" is [width, height] in pixels, both above zero.");

        if (CellCount > MostCells)
            throw new ArgumentException(
                $"{named} asks for {CellCount} cells; a sheet holds at most {MostCells}. Take two sheets.");

        Place(named, "at", At);
        Tape(Rule, named);
        for (var cell = 0; cell < (Cells?.Length ?? 0); cell++)
        {
            Place($"{named}, cell {cell + 1}", "at", Cells![cell].At);
            Tape(Cells[cell].Rule, $"{named}, cell {cell + 1}");
        }
    }

    static void Place(string named, string member, float[]? point)
    {
        if (point is not (null or { Length: 2 }))
            throw new ArgumentException($"{named}: \"{member}\" is [x, y] in metres.");
    }

    static Vector2? Point(float[]? point) => point is { Length: 2 } ? new Vector2(point[0], point[1]) : null;

    /// <summary>The tape's points, flattened the way the ruler is clicked: two points to a measurement.</summary>
    static List<Vector2>? Tape(float[][]? pairs, string? named = null)
    {
        if (pairs is null) return null;

        var points = new List<Vector2>(pairs.Length * 2);
        foreach (var pair in pairs)
        {
            if (pair is not { Length: 4 })
                throw new ArgumentException($"{named ?? "the sheet"}: \"rule\" is a list of [x1, y1, x2, y2].");

            points.Add(new Vector2(pair[0], pair[1]));
            points.Add(new Vector2(pair[2], pair[3]));
        }

        return points;
    }
}

/// <summary>
/// One cell of a sheet: what it is of, and whatever it differs from the sheet's own figures in. A
/// member left out is the sheet's, so what the cells have in common is written down once.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SheetCell
{
    /// <summary>What this cell is of, drawn in its caption. Its place in the reading order when it has none.</summary>
    public string? Label { get; init; }

    public string? Note { get; init; }

    public string? Map { get; init; }

    public float[]? At { get; init; }

    public float? View { get; init; }

    public double? Seconds { get; init; }

    public string[]? Ui { get; init; }

    public float[][]? Rule { get; init; }
}
