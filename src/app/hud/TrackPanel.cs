using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>What each shape of the proving ground is costing each kind of car</b>, while it is happening — one
/// collapsible section per shape, and under each of them the three drivetrains side by side.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the panel form of <c>--bench track</c> and reads the same instrument</b>
/// (<see cref="TrackMetrics"/>). A second implementation of the arithmetic would be a second answer, and
/// the probe's table and this panel disagreeing is not a thing anybody could resolve by looking at the
/// track. What differs is only how long each has been watching.
/// </para>
/// <para>
/// <b>The header carries the figure and the rows carry the account of it.</b> Watching the lap wants the
/// top speed of each shape and nothing else; asking why one drivetrain is slower wants four more lines
/// under it, and a panel that always showed twenty of them covered the track it was written to explain.
/// </para>
/// <para>
/// <b>It stands in the top-left, under the town's own name</b>, because the shapes are what this map is
/// and the frame read-out already owns the other corner.
/// </para>
/// </remarks>
internal sealed class TrackPanel
{
    /// <summary>Where each column starts, in characters — which is what lines them up without measuring a glyph.</summary>
    const int TopColumn = 13;

    const int HoldColumn = 21;
    const int SlowColumn = 29;
    const int AccelColumn = 37;
    const int OffLineColumn = 45;

    /// <summary>The longest line any row comes to, which is what the panel is sized on.</summary>
    const int WidestLine = OffLineColumn + 6;

    const float MarginPx = 12f;
    const float RowPitchPx = Theme.SmallTextPx + 4f;

    /// <summary>Clear of the two boxes the run's own furniture keeps in this corner.</summary>
    const float BelowTheHudPx = MarginPx + Theme.TextPx + 14f + 4f + Theme.SmallTextPx + 12f + 8f;

    /// <summary>What a section adds when it is open: the column heading, and one row per drivetrain.</summary>
    const int RowsPerSection = 1 + TrackMetrics.Drivetrains;

    /// <summary>
    /// Which sections are showing their rows. <b>The first opens and the rest do not</b>: the panel is read
    /// at two depths, and one section open is what says the others can be.
    /// </summary>
    readonly bool[] _open = Opened();

    static bool[] Opened()
    {
        var open = new bool[TrackMetrics.ShapeCount];
        if (open.Length > 0) open[0] = true;
        return open;
    }

    readonly Rect[] _headers = new Rect[TrackMetrics.ShapeCount];

    /// <summary>The whole panel, so a click that lands on it is not also a click on the town behind it.</summary>
    public Rect Box { get; private set; }

    /// <summary>
    /// How many rows the last draw actually wrote, against the count the panel was sized from before a
    /// word of it went down. A section that grew a row without the count following it draws that row
    /// through the panel's own bottom edge.
    /// </summary>
    public int Rows { get; private set; }

    public static float HeightFor(int rows) => (rows * RowPitchPx) + (Theme.PaddingPx * 2f);

    public bool IsOpen(int shape) => _open[shape];

    /// <summary>Where a section's own header was last drawn, which is the only pressable thing on the panel.</summary>
    public Rect HeaderOf(int shape) => _headers[shape];

    /// <summary>
    /// The town has changed under it. <b>A panel that is not being drawn takes no clicks</b> — the switch
    /// outlives the map, and a box left standing from the proving ground would swallow the top-left corner
    /// of every town opened after it.
    /// </summary>
    public void TownChanged()
    {
        Box = default;
        Array.Clear(_headers);
    }

    /// <summary>A click on the panel: a header opens or shuts its section, and anywhere else on it is taken and dropped.</summary>
    public bool Click(Vector2 atPx)
    {
        if (!Box.Contains(atPx)) return false;

        for (var shape = 0; shape < _headers.Length; shape++)
        {
            if (!_headers[shape].Contains(atPx)) continue;

            _open[shape] = !_open[shape];
            break;
        }

        return true;
    }

    public void Draw(ref ScreenDraw draw, Vector2 pointerPx, TrackMetrics metrics)
    {
        var widthPx = GlyphSheet.WidthPx(WidestLine, Theme.SmallTextPx) + (Theme.PaddingPx * 2f);
        Box = new Rect(new Vector2(MarginPx, BelowTheHudPx), new Vector2(widthPx, HeightFor(RowCount(metrics))));
        Theme.Frame(ref draw, Box);

        Span<char> text = stackalloc char[80];
        var row = 0;
        for (var shape = 0; shape < metrics.Shapes; shape++)
        {
            Section(ref draw, ref row, pointerPx, text, metrics, shape);
        }

        Rows = row;
    }

    int RowCount(TrackMetrics metrics)
    {
        var rows = metrics.Shapes;
        for (var shape = 0; shape < metrics.Shapes; shape++)
        {
            if (_open[shape]) rows += RowsPerSection;
        }

        return rows;
    }

    void Section(
        ref ScreenDraw draw, ref int row, Vector2 pointerPx, scoped Span<char> text, TrackMetrics metrics, int shape)
    {
        var whole = metrics.Figures(shape);
        var head = new TextBuffer(text);
        head.Add(_open[shape] ? "- " : "+ ");
        head.Add(metrics.SectionOf(shape).Name);
        head.PadTo(TopColumn);
        if (whole.Any)
        {
            head.Add(whole.TopMps, "F1");
            head.Add(" m/s   ");
            head.Add(whole.Passes);
            head.Add(" passes");
        }
        else
        {
            // Nothing has been round it yet, which on a lap this long is the first half-minute.
            head.Add("no leg yet");
        }

        Header(ref draw, ref row, pointerPx, shape, head.Written);
        if (!_open[shape]) return;

        var heading = new TextBuffer(text);
        heading.Add("    drive");
        heading.PadTo(TopColumn);
        heading.Add("top m/s");
        heading.PadTo(HoldColumn);
        heading.Add("hold");
        heading.PadTo(SlowColumn);
        heading.Add("slow m");
        heading.PadTo(AccelColumn);
        heading.Add("out s");
        heading.PadTo(OffLineColumn);
        heading.Add("off m");
        Write(ref draw, ref row, heading.Written, Theme.Dim);

        for (var drivetrain = 0; drivetrain < TrackMetrics.Drivetrains; drivetrain++)
        {
            Row(ref draw, ref row, text, TrackMetrics.DrivetrainName(drivetrain), metrics.Figures(shape, drivetrain));
        }
    }

    /// <summary>
    /// One drivetrain's four figures. <b>A column with nothing behind it says so</b> rather than printing
    /// the zero a mean over no samples comes to — a corner nobody has stopped on yet and a corner a car
    /// stopped on in no distance at all are not the same reading.
    /// </summary>
    void Row(
        ref ScreenDraw draw, ref int row, scoped Span<char> text, string drivetrain, in SectionFigures figures)
    {
        var line = new TextBuffer(text);
        line.Add("    ");
        line.Add(drivetrain);
        line.PadTo(TopColumn);
        Figure(ref line, figures.TopMps, figures.Passes, "F1");
        line.PadTo(HoldColumn);
        Figure(ref line, figures.HoldMps, figures.Passes - figures.Stops, "F1");
        line.PadTo(SlowColumn);
        Figure(ref line, figures.SlowM, figures.Slowings, "F1");
        line.PadTo(AccelColumn);
        Figure(ref line, figures.AccelS, figures.Pulls, "F1");
        line.PadTo(OffLineColumn);
        Figure(ref line, figures.OffLineM, figures.Passes, "F2");
        Write(ref draw, ref row, line.Written, Theme.Text);
    }

    static void Figure(ref TextBuffer line, float value, int samples, string format)
    {
        if (samples <= 0)
        {
            line.Add('-');
            return;
        }

        line.Add(value, format);
    }

    /// <summary>A section's own row: the figure it comes to, marked with whether the account of it is showing.</summary>
    void Header(ref ScreenDraw draw, ref int row, Vector2 pointerPx, int shape, scoped ReadOnlySpan<char> text)
    {
        var atPx = Box.AtPx + new Vector2(Theme.PaddingPx * 0.4f, Theme.PaddingPx + (row * RowPitchPx));
        var box = new Rect(
            new Vector2(Box.AtPx.X + Theme.EdgePx, atPx.Y - 2f),
            new Vector2(Box.SizePx.X - (Theme.EdgePx * 2f), RowPitchPx));
        _headers[shape] = box;

        if (box.Contains(pointerPx)) draw.RoundedRect(box.AtPx, box.SizePx, Theme.RowRadiusPx, Theme.RowHover);

        draw.Text(atPx, text, Theme.SmallTextPx, Theme.Heading);
        row++;
    }

    void Write(ref ScreenDraw draw, ref int row, scoped ReadOnlySpan<char> text, Vector4 colour)
    {
        draw.Text(
            Box.AtPx + new Vector2(Theme.PaddingPx, Theme.PaddingPx + (row * RowPitchPx)), text, Theme.SmallTextPx,
            colour);
        row++;
    }
}
