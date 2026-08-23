using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>What the frame cost, where it went, and where the tick's own time went under it</b> — three
/// collapsible sections, each of which adds up to the figure on its own header.
/// </summary>
/// <remarks>
/// <para>
/// <b>The budget closes, and that is the point of it.</b> The frame's rows sum to the frame, the
/// tick's rows sum to the tick, and what no row claimed is printed as <c>other</c> rather than
/// dropped. A read-out whose parts do not add up to its total cannot be used to decide anything: the
/// row somebody is about to go and optimise might be three percent of the frame, and until the rows
/// close there is no way to know it from thirty.
/// </para>
/// <para>
/// <b>A frame and a tick are different measurements and the panel says which is which.</b> A frame
/// runs however many fixed ticks the clock was owed — none on a frozen town, three at pace 3 — so the
/// tick section is a mean <em>per tick</em> and <c>sim</c> in the frame section is every tick that
/// frame ran, together. <c>a frame</c> on both headers is what bridges them, and the two disagreeing
/// is itself a finding.
/// </para>
/// <para>
/// <b>Every timing is a window's mean and not the frame just drawn</b> (<see cref="FrameMeter"/>).
/// The counts beside them are not averaged: a body count is the state of the town rather than a
/// measurement of it.
/// </para>
/// <para>
/// <b>Sections collapse because the panel is read at two depths.</b> Watching a run wants the frame,
/// its rate and its worst; chasing a row wants ten more lines under it, and a panel that always
/// showed all of them covered a quarter of the town with figures nobody was reading.
/// </para>
/// </remarks>
internal sealed class FrameReadout
{
    /// <summary>Where a row's figure starts, in characters, which is what lines the columns up without measuring a glyph.</summary>
    const int ValueColumn = 15;

    /// <summary>
    /// The longest line any row comes to: the overlay row's own account of itself, which is what the
    /// panel is sized on. Narrower and it is the text that is cut, which reads as a read-out with a
    /// word missing rather than as a panel that is too small.
    /// </summary>
    const int WidestLine = ValueColumn + 26;

    const float MarginPx = 12f;
    const float RowPitchPx = Theme.SmallTextPx + 4f;

    public const int Frame = 0;
    public const int Tick = 1;
    public const int Town = 2;
    const int Sections = 3;

    /// <summary>
    /// What each section adds when it is open. <b>The one thing here that has to be kept in step with
    /// what a section writes</b>, because the panel is sized before a word of it is drawn — and
    /// <see cref="Rows"/> is what lets a test say whether it still is.
    /// </summary>
    const int FrameRows = 10;

    const int TickRows = 10;
    const int TownRows = 5;

    /// <summary>
    /// Which sections are open. <b>The frame and the tick open by default and the counts do not</b>:
    /// the first two are what the read-out is switched on for, and the third answers a question
    /// somebody asks once.
    /// </summary>
    readonly bool[] _open = [true, true, false];

    readonly Rect[] _headers = new Rect[Sections];

    /// <summary>The whole panel, so a click that lands on it is not also a click on the town behind it.</summary>
    public Rect Box { get; private set; }

    /// <summary>
    /// How many rows the last draw actually wrote. <b>The panel is sized from a count taken before any
    /// of them are written</b>, so this is what says the two still agree — a section that grew a row
    /// without the count following it draws that row through the panel's own bottom edge.
    /// </summary>
    public int Rows { get; private set; }

    /// <summary>How tall a row is, so a caller can check the panel against the rows it wrote.</summary>
    public static float HeightFor(int rows) => rows * RowPitchPx + Theme.PaddingPx * 2f;

    public bool IsOpen(int section) => _open[section];

    /// <summary>
    /// A click on the panel: a header toggles its section, and anywhere else on the panel is taken and
    /// dropped. Returns whether the panel took it.
    /// </summary>
    public bool Click(Vector2 atPx)
    {
        if (!Box.Contains(atPx)) return false;

        for (var section = 0; section < Sections; section++)
        {
            if (!_headers[section].Contains(atPx)) continue;

            _open[section] = !_open[section];
            break;
        }

        return true;
    }

    public void Draw(
        ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerPx, in FrameFigures frame, long crossings, int quads,
        TownWorld world, bool relaid)
    {
        var widthPx = GlyphSheet.WidthPx(WidestLine, Theme.SmallTextPx) + Theme.PaddingPx * 2f;
        Box = new Rect(
            new Vector2(uiPx.X - MarginPx - widthPx, MarginPx + Theme.GearPx + 8f),
            new Vector2(widthPx, HeightFor(RowCount(frame))));
        Theme.Frame(ref draw, Box);

        Span<char> text = stackalloc char[80];
        var row = 0;

        FrameSection(ref draw, ref row, pointerPx, text, frame);
        TickSection(ref draw, ref row, pointerPx, text, frame);
        TownSection(ref draw, ref row, pointerPx, text, world, crossings, quads, relaid);
        Rows = row;
    }

    /// <summary>
    /// How tall the panel is before a word of it is written. <b>Counted rather than measured after the
    /// fact</b>, because the frame it is drawn into is the frame it is laid out in: a panel sized from
    /// what it wrote last time is a panel one frame behind every collapse.
    /// </summary>
    int RowCount(in FrameFigures frame)
    {
        var rows = Sections;
        if (_open[Frame] && frame.FrameMs > 0d) rows += FrameRows;
        if (_open[Tick]) rows += TickRows;
        if (_open[Town]) rows += TownRows;
        return rows;
    }

    /// <summary>
    /// The frame, split into what this build spent and what it waited for, with the first of those
    /// broken down into the six things the shell does in a frame and the residual none of them claimed.
    /// </summary>
    /// <remarks>
    /// <b>Only <c>cpu</c> and its rows are this build's.</b> Under FIFO the blocked row is the whole of
    /// the pacing and the rate on the header is the display's refresh rate, which moves not at all with
    /// the size of the town — so a frame figure quoted from this panel is the cpu row and never the
    /// header.
    /// </remarks>
    void FrameSection(
        ref ScreenDraw draw, ref int row, Vector2 pointerPx, scoped Span<char> text, in FrameFigures frame)
    {
        var head = new TextBuffer(text);
        Name(ref head, Frame, "frame");
        if (frame.FrameMs <= 0d)
        {
            // No window has closed: on the offscreen path there is no frame to time, and on the
            // windowed one the first frame is dropped on purpose.
            head.Add("not yet measured");
            Header(ref draw, ref row, pointerPx, Frame, head.Written);
            return;
        }

        head.Add(frame.FrameMs, "F2");
        head.Add(" ms   ");
        head.Add(frame.Fps, "F0");
        head.Add(" fps");
        Header(ref draw, ref row, pointerPx, Frame, head.Written);
        if (!_open[Frame]) return;

        Row(ref draw, ref row, text, "  cpu", frame.CpuMs, "ms", "F2");
        Row(ref draw, ref row, text, "    sim", frame.SimMs, "ms", "F2");
        Row(ref draw, ref row, text, "    sprites", frame.SpritesMs, "ms", "F2");
        Row(ref draw, ref row, text, "    interface", frame.InterfaceMs, "ms", "F2");
        Row(ref draw, ref row, text, "    submit", frame.SubmitMs, "ms", "F2");
        Row(ref draw, ref row, text, "    pump", frame.PumpMs, "ms", "F2");
        Row(ref draw, ref row, text, "    input", frame.InputMs, "ms", "F2");
        Row(ref draw, ref row, text, "    other", frame.OtherMs, "ms", "F2");
        Row(ref draw, ref row, text, "  blocked", frame.BlockedMs, "ms", "F2");
        Row(ref draw, ref row, text, "  slowest", frame.WorstMs, "ms", "F2");
    }

    /// <summary>
    /// The tick, ranked by the five phases the brief fixes, with the two of them this town can say
    /// more about opened up under them.
    /// </summary>
    /// <remarks>
    /// <b>The header is the tick measured whole, not the five added up</b>, so <c>other</c> is a real
    /// residual and not an arithmetic identity. It reads as nothing on every shipped map, which is the
    /// useful thing about it: it is what says the five phases still cover the tick. What this
    /// instrument costs is <em>not</em> there — a mark's own timestamp falls inside the phase it
    /// closes — and is priced by <c>--bench tick</c>.
    /// </remarks>
    void TickSection(
        ref ScreenDraw draw, ref int row, Vector2 pointerPx, scoped Span<char> text, in FrameFigures frame)
    {
        var phases = frame.Phases;
        var head = new TextBuffer(text);
        Name(ref head, Tick, "tick");
        head.Add(phases.MillisecondsPer(phases.WholeTicks), "F3");
        head.Add(" ms   ");
        head.Add(frame.TicksPerFrame, "F1");
        head.Add(" a frame");
        Header(ref draw, ref row, pointerPx, Tick, head.Written);
        if (!_open[Tick]) return;

        var sub = frame.Sub;
        Row(ref draw, ref row, text, "    input", phases.MillisecondsPer(phases.InputTicks), "ms");
        Row(ref draw, ref row, text, "    index", phases.MillisecondsPer(phases.IndexTicks), "ms");
        Row(ref draw, ref row, text, "    agents", phases.MillisecondsPer(phases.AgentTicks), "ms");
        Row(ref draw, ref row, text, "      walkers", phases.MillisecondsPer(sub.WalkerTicks), "ms");
        Row(ref draw, ref row, text, "      cars", phases.MillisecondsPer(sub.CarTicks), "ms");
        Row(ref draw, ref row, text, "    bodies", phases.MillisecondsPer(phases.BodyTicks), "ms");
        Row(ref draw, ref row, text, "      solver", phases.MillisecondsPer(sub.SolverTicks), "ms");
        Row(ref draw, ref row, text, "      own", phases.MillisecondsPer(phases.BodyTicks - sub.SolverTicks), "ms");
        Row(ref draw, ref row, text, "    contacts", phases.MillisecondsPer(phases.ContactTicks), "ms");
        Row(ref draw, ref row, text, "    other", phases.MillisecondsPer(phases.OtherTicks), "ms");
    }

    /// <summary>
    /// What the figures above were measured over: the roster, how much of it the solver is still
    /// awake for, the wall's crossing count and what this instrument's own layers cost.
    /// </summary>
    /// <remarks>
    /// <b>A tick figure with no census beside it says how fast a build runs and not whether the town it
    /// ran was a town</b>, which is the discipline every figure in this project is quoted under.
    /// </remarks>
    void TownSection(
        ref ScreenDraw draw, ref int row, Vector2 pointerPx, scoped Span<char> text, TownWorld world, long crossings,
        int quads, bool relaid)
    {
        var head = new TextBuffer(text);
        Name(ref head, Town, "town");
        head.Add(world.People.Count + world.Cars.Count);
        head.Add(" bodies, ");
        head.Add(world.IntegratedBodyCount);
        head.Add(" integrated");
        Header(ref draw, ref row, pointerPx, Town, head.Written);
        if (!_open[Town]) return;

        Count(ref draw, ref row, text, "    walkers", world.People.Count);
        Count(ref draw, ref row, text, "    cars", world.Cars.Count);
        Count(ref draw, ref row, text, "    statics", world.StaticBodyCount);

        var line = new TextBuffer(text);
        line.Add("    crossings");
        line.PadTo(ValueColumn);
        if (crossings > 0)
        {
            line.Add(crossings);
            line.Add(" a frame");
        }
        else
        {
            // Zero means two different things, and saying which is the whole point of the counter:
            // in a Release build it is compiled out, and before the first steady frame there simply
            // is no figure yet.
            line.Add(Runtime.Vk.Crossings == 0 ? "compiled out of Release" : "not yet measured");
        }

        Write(ref draw, ref row, line.Written);

        line.Clear();
        line.Add("    overlay");
        line.PadTo(ValueColumn);
        line.Add(quads);
        line.Add(relaid ? " quads, graphs relaid" : " quads");
        Write(ref draw, ref row, line.Written);
    }

    /// <summary>
    /// A section's own row: the figure that section adds up to, marked with whether what adds up to it
    /// is showing. It is the only pressable thing on the panel.
    /// </summary>
    void Header(ref ScreenDraw draw, ref int row, Vector2 pointerPx, int section, scoped ReadOnlySpan<char> text)
    {
        var atPx = Box.AtPx + new Vector2(Theme.PaddingPx * 0.4f, Theme.PaddingPx + row * RowPitchPx);
        var box = new Rect(
            new Vector2(Box.AtPx.X + Theme.EdgePx, atPx.Y - 2f),
            new Vector2(Box.SizePx.X - Theme.EdgePx * 2f, RowPitchPx));
        _headers[section] = box;

        if (box.Contains(pointerPx)) draw.RoundedRect(box.AtPx, box.SizePx, Theme.RowRadiusPx, Theme.RowHover);

        draw.Text(atPx, text, Theme.SmallTextPx, Theme.Heading);
        row++;
    }

    /// <summary>
    /// A section's name with its state in front of it. <b>The marker is a character and it is
    /// outdented</b>: the sheet carries no arrow, the two characters it does carry say the one thing
    /// that needs saying — what is under this is showing, or it is not — and standing them to the left
    /// of the name puts the headers and the rows under them on two different margins.
    /// </summary>
    void Name(ref TextBuffer line, int section, string name)
    {
        line.Add(_open[section] ? "- " : "+ ");
        line.Add(name);
        line.PadTo(ValueColumn);
    }

    /// <summary>
    /// One figure. <b>The phases are printed to a thousandth and the frame to a hundredth</b>: a
    /// phase is tens of microseconds, and a third decimal on a 16 ms frame is the jitter the window
    /// was averaged to remove.
    /// </summary>
    void Row(
        ref ScreenDraw draw, ref int row, scoped Span<char> text, string name, double value, string unit,
        string format = "F3")
    {
        var line = new TextBuffer(text);
        line.Add(name);
        line.PadTo(ValueColumn);
        line.Add(value, format);
        line.Add(' ');
        line.Add(unit);
        Write(ref draw, ref row, line.Written);
    }

    void Count(ref ScreenDraw draw, ref int row, scoped Span<char> text, string name, int count)
    {
        var line = new TextBuffer(text);
        line.Add(name);
        line.PadTo(ValueColumn);
        line.Add(count);
        Write(ref draw, ref row, line.Written);
    }

    void Write(ref ScreenDraw draw, ref int row, scoped ReadOnlySpan<char> text)
    {
        draw.Text(
            Box.AtPx + new Vector2(Theme.PaddingPx, Theme.PaddingPx + row * RowPitchPx), text, Theme.SmallTextPx,
            Theme.Text);
        row++;
    }
}
