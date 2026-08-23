using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Shot;

/// <summary>
/// What a frame was a picture of, written under it: the place, the framing, the moment, the seed — and
/// a graduated bar that says how big the thing in the picture is (SHT-2).
/// </summary>
/// <remarks>
/// A review that has to be told the scale in prose beside the picture is a review of two documents that
/// drift apart. Everything here is derived from the request and the census the shot came back with, so
/// the caption cannot claim a framing the frame does not have.
/// </remarks>
internal readonly record struct ShotCaption
{
    /// <summary>What this cell is of, in a sheet of several. Null for a frame that is on its own.</summary>
    public string? Label { get; init; }

    public required string Map { get; init; }

    /// <summary>The <c>--ui</c> words as given, or empty for the ordinary interface.</summary>
    public string Ui { get; init; }

    /// <summary>What a reviewer is being asked to look at, if the request said. Drawn as its own row.</summary>
    public string? Note { get; init; }

    public required Vector2 SpanM { get; init; }

    public required Vector2 CentreM { get; init; }

    public required float PxPerM { get; init; }

    public long Tick { get; init; }

    public double Seconds { get; init; }

    public ulong Seed { get; init; }

    public static ShotCaption Of(in ShotRequest ask, in ShotReport report, string? label, string? note) => new()
    {
        Label = label,
        Map = report.Map,
        Ui = ask.Ui is { Length: > 0 } ui ? string.Join(',', ui) : string.Empty,
        Note = note,
        SpanM = report.SpanM,
        CentreM = report.CentreM,
        PxPerM = report.PxPerM,
        Tick = report.Tick,
        Seconds = ask.Seconds,
        Seed = report.Seed,
    };

    /// <summary>The head row: what the cell is, where it was taken, and what was switched on.</summary>
    public string Head()
    {
        var head = Label is { Length: > 0 } label ? $"{label} — {Map}" : Map;
        return Ui.Length > 0 ? $"{head} · ui {Ui}" : head;
    }

    /// <summary>
    /// The figures row: what a metre is worth here, where the camera stood, and how far into the run
    /// the shutter opened. It is everything the same picture would have to be asked for again with.
    /// </summary>
    public string Figures()
    {
        var moment = Seconds > 0 ? $"tick {Tick} ({Seconds:F1} s)" : $"tick {Tick}";
        return $"{SpanM.X:F0}x{SpanM.Y:F0} m at {CentreM.X:F0},{CentreM.Y:F0} · {PxPerM:F1} px/m · " +
               $"{moment} · seed {Seed}";
    }
}

/// <summary>
/// The band a review picture carries under the frame. <b>It is composited under the picture and never
/// over it</b> (SHT-1): the pixels above the band are the game's own, so a frame can still be compared
/// against another build's frame of the same ground.
/// </summary>
internal static class Caption
{
    /// <summary>How wide a cell has to be for a figure to be drawn at its full height, in pixels per
    /// character of the widest row. Below it the whole band scales down with the cell.</summary>
    const int WidthPerFigurePx = 46;

    const int SmallestTextPx = 9;
    const int LargestTextPx = 17;

    /// <summary>How much taller the head row is than the figures under it.</summary>
    const int HeadTallerPx = 3;

    /// <summary>About how long the scale bar comes out, and the most of the band's width it may take.</summary>
    const int BarTargetPx = 200;
    const float BarMostOfWidth = 0.32f;

    /// <summary>How many segments the bar is cut into. They carry no figures of their own, so this is
    /// legibility and not arithmetic: what is measured off the bar is its whole length.</summary>
    const int Marks = 4;

    /// <summary>How many text rows a caption comes to — two, or three where it carries a note.</summary>
    public static int Rows(in ShotCaption caption) => caption.Note is { Length: > 0 } ? 3 : 2;

    /// <summary>
    /// How tall the band is for a cell of this width. <b>Every cell of a sheet is given the same
    /// number of rows</b>, or the cells would be different heights and could not be tiled.
    /// </summary>
    public static int HeightPx(int widthPx, int rows)
    {
        var figures = FiguresTextPx(widthPx);
        var padding = Padding(figures);
        return (padding * 2) + figures + HeadTallerPx + ((rows - 1) * (figures + Gap(figures)));
    }

    /// <summary>
    /// The frame with its band under it, as a new image. The caller owns both — the frame is left
    /// exactly as the renderer read it back.
    /// </summary>
    public static Image<Rgba32> Under(Image<Rgba32> frame, in ShotCaption caption, int rows)
    {
        var bandPx = HeightPx(frame.Width, rows);
        var composed = new Image<Rgba32>(frame.Width, frame.Height + bandPx);
        var band = new Rectangle(0, frame.Height, frame.Width, bandPx);

        composed.Mutate(canvas => canvas.DrawImage(frame, new Point(0, 0), 1f));
        Fill(composed, band, Of(Theme.Panel));

        // A hairline between the two, because the band is a claim *about* the picture and must not
        // read as the bottom of it.
        Fill(composed, new Rectangle(0, frame.Height, frame.Width, 1), Of(Theme.PanelEdge));

        Letter(composed, band, caption, rows);
        return composed;
    }

    static void Letter(Image<Rgba32> into, Rectangle band, in ShotCaption caption, int rows)
    {
        var figuresPx = FiguresTextPx(band.Width);
        var headPx = figuresPx + HeadTallerPx;
        var padding = Padding(figuresPx);

        // The bar is laid first and the text is fitted to what is left, so a long map name shortens
        // itself rather than running under the graduations.
        var takenPx = Bar(into, band, caption.PxPerM, figuresPx, padding);
        var roomPx = band.Width - padding - takenPx;

        var atY = band.Y + padding;

        // The head is the one row allowed to lose its tail — a map name reads from the front — so it
        // is written at its own height and cut. The rows under it shrink to fit instead: a row of
        // figures with the seed cut off it is a row that cannot reproduce the picture.
        GlyphStamp.Write(into, new Point(padding, atY), GlyphStamp.Fit(caption.Head(), roomPx, headPx),
            headPx, Of(caption.Label is { Length: > 0 } ? Theme.Accent : Theme.Text));

        atY += headPx + Gap(figuresPx);
        Row(caption.Figures(), atY, Theme.Dim);

        if (rows < 3 || caption.Note is not { Length: > 0 } note) return;

        Row(note, atY + figuresPx + Gap(figuresPx), Theme.Legend);
        return;

        void Row(string text, int topY, Vector4 colour)
        {
            var textPx = Shrunk(text, roomPx, figuresPx);
            GlyphStamp.Write(into, new Point(padding, topY), GlyphStamp.Fit(text, roomPx, textPx), textPx, Of(colour));
        }
    }

    /// <summary>
    /// The graduated bar at the right of the band, and <b>how much of the width it spoke for</b>
    /// counted in from the right edge — which is what the rows above are then fitted into. Cut into
    /// alternating segments rather than tick-marked: at a caption's size a mark is a pixel, and a
    /// chequered bar is legible where a comb is not.
    /// </summary>
    static int Bar(Image<Rgba32> into, Rectangle band, float pxPerM, int textPx, int padding)
    {
        if (pxPerM <= 0f) return 0;

        // The same ladder of round numbers the legend and the ruler are graduated on: a review picture
        // whose bar disagreed with the one inside the frame would be two answers to one question.
        // Here it is the bar's *whole length* that stands on the ladder rather than each graduation,
        // because only the length carries a figure — a bar reading 14 m is a bar nobody measures with.
        var targetPx = MathF.Min(BarTargetPx, band.Width * BarMostOfWidth);
        var barM = Ladder.StepM(targetPx / pxPerM);
        var barPx = (int)MathF.Round(barM * pxPerM);
        if (barPx < textPx) return 0; // nothing this frame covers is worth a bar: it would read as a rule

        Span<char> text = stackalloc char[16];
        var written = new TextBuffer(text);
        Ladder.WriteDistance(ref written, barM);
        var figure = written.Written.ToString();

        var figurePx = GlyphStamp.WidthPx(figure.Length, textPx);
        var heightPx = Math.Max(3, textPx / 2);
        var rightPx = band.Width - padding;
        var leftPx = rightPx - figurePx - padding - barPx;
        var topPx = band.Y + ((band.Height - heightPx) / 2);

        Fill(into, new Rectangle(leftPx - 1, topPx - 1, barPx + 2, heightPx + 2), Of(Theme.LegendShadow));
        for (var mark = 0; mark < Marks; mark++)
        {
            var fromPx = leftPx + (barPx * mark / Marks);
            var toPx = leftPx + (barPx * (mark + 1) / Marks);
            Fill(into, new Rectangle(fromPx, topPx, toPx - fromPx, heightPx),
                Of(mark % 2 == 0 ? Theme.Legend : Theme.Dim));
        }

        GlyphStamp.Write(into, new Point(rightPx - figurePx, topPx - ((textPx - heightPx) / 2)), figure, textPx,
            Of(Theme.Legend));

        return band.Width - leftPx + padding;
    }

    /// <summary>The figures row's text height, which every other size in the band is derived from.</summary>
    static int FiguresTextPx(int widthPx) =>
        Math.Clamp(widthPx / WidthPerFigurePx, SmallestTextPx, LargestTextPx);

    /// <summary>
    /// The largest height at or under <paramref name="mostPx"/> that fits the whole line in the room
    /// it has, and never below <see cref="SmallestTextPx"/>. A cell of a four-up sheet is half the
    /// width of a frame on its own, and its figures are the same sixty characters either way.
    /// </summary>
    static int Shrunk(string text, int roomPx, int mostPx)
    {
        for (var textPx = mostPx; textPx > SmallestTextPx; textPx--)
            if (GlyphStamp.WidthPx(text.Length, textPx) <= roomPx)
                return textPx;

        return SmallestTextPx;
    }

    static int Padding(int textPx) => Math.Max(4, textPx * 2 / 3);

    static int Gap(int textPx) => Math.Max(2, textPx / 4);

    /// <summary>The interface's own colours, opaque: a band is a surface and not a wash over a town.</summary>
    static Rgba32 Of(Vector4 colour) => new(colour.X, colour.Y, colour.Z, 1f);

    /// <summary>
    /// A flat rectangle, written straight into the pixels. <b>Drawing a shape is the ImageSharp.Drawing
    /// package and this project references only the core one</b> — a whole geometry library for the
    /// eight rectangles a caption band is made of would be a dependency taken for nothing.
    /// </summary>
    static void Fill(Image<Rgba32> into, Rectangle at, Rgba32 colour)
    {
        var clipped = Rectangle.Intersect(at, into.Bounds);
        into.ProcessPixelRows(pixels =>
        {
            for (var y = clipped.Top; y < clipped.Bottom; y++)
                pixels.GetRowSpan(y).Slice(clipped.Left, clipped.Width).Fill(colour);
        });
    }
}
