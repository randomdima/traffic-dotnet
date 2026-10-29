using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>OBS-2m — the bottom-left corner is the selection, and it is the only place it is written.</b> A
/// title naming what is picked out over a body of rows: what the unit is, what it is doing, how fast, what
/// is claimed in front of it, how much of its trip is left, and what the run's own watches have against
/// it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing about the selection is written on the town</b> (CTL-1). What stands at the unit is the mark
/// and only the mark — the ring and the path (<see cref="Render.SpriteRings"/>, <see cref="SelectionPath"/>)
/// — because a shape says <em>which</em> one without covering the road it is about to drive down. Words
/// take room, and the room they take is the town.
/// </para>
/// <para>
/// <b>A group is counted and not described</b> (CTL-1b): there is no such thing as the speed of thirty
/// cars, so a set says how many of each kind it holds and nothing else.
/// </para>
/// <para>
/// <b>It does not need the unit on the picture.</b> Somebody indoors or riding in a car is not drawn and
/// wears no ring (PHY-7), and a unit the camera has been panned off has nothing at it either — this
/// still says what it is doing and where.
/// </para>
/// <para>
/// <b>The rows themselves are <see cref="UnitReadout"/>'s and are not written here.</b> Every figure on
/// them is read off the body, and the same rows are what a script driving a unit by hand prints — one
/// machine and two readers, so a panel and a log cannot disagree about what a car is doing.
/// </para>
/// </remarks>
internal sealed class UnitPanel
{
    /// <summary>
    /// The longest line the body is budgeted for, and the longest the title is. <b>Both are budgets rather
    /// than measurements</b>, so the panel does not move when a speed gains a digit or a car enters a
    /// manoeuvre with a longer name — what overruns is fitted to the panel instead.
    /// </summary>
    const int WidestLine = UnitReadout.ValueColumn + 32;

    const int TitleLine = 20;

    const float RowPitchPx = Theme.SmallTextPx + 4f;

    const float TitleRowPx = Theme.TextPx + 10f;

    const string ReleaseLabel = "Release";

    Rect _title;

    /// <summary>The reset's button on the title row, or an empty box while nothing picked out is the player's.</summary>
    Rect _release;

    bool _releaseAsked;

    /// <summary>Whether the body is showing. <b>Open by default</b>: a panel that only appears when something is picked out has already been asked for.</summary>
    public bool Open { get; private set; } = true;

    /// <summary>The whole panel, or an empty box on a frame that drew none — a click on it is not a click on the town behind it.</summary>
    public Rect Box { get; private set; }

    /// <summary>How many rows the last draw wrote, which is what a test reads the layout off.</summary>
    public int Rows { get; private set; }

    /// <summary>
    /// A click on the panel: the release button asks for the reset, the rest of the title opens and shuts the body,
    /// and anywhere else on it is taken and dropped.
    /// </summary>
    public bool Click(Vector2 atPx)
    {
        if (!Box.Contains(atPx)) return false;

        if (_release.Contains(atPx)) _releaseAsked = true;
        else if (_title.Contains(atPx)) Open = !Open;

        return true;
    }

    /// <summary>
    /// Whether the release button was pressed since this was last asked. <b>The reset is the world's</b> (CTL-4) and
    /// the panel has no town to do it to, so the button says somebody asked and the frame does it — the same call
    /// <c>R</c> makes.
    /// </summary>
    public bool TakeReleaseAsked()
    {
        if (!_releaseAsked) return false;

        _releaseAsked = false;
        return true;
    }

    /// <param name="watching">
    /// The run's own watches, or empty on a map that claims nothing. <b>What a watch has against this one
    /// body is written here</b> (OBS-2i): a claim is a statement about the town, and a finding that names a
    /// car is the one thing on that panel that is about a unit.
    /// </param>
    public void Draw(
        ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerPx, TownWorld world,
        ReadOnlySpan<ScenarioWatch> watching)
    {
        if (world.SelectedCount == 0)
        {
            // Nothing was laid, so nothing may be pressed and nothing may swallow a click on the town.
            Box = default;
            _title = default;
            _release = default;
            Rows = 0;
            return;
        }

        var unit = world.Lead;
        Span<char> text = stackalloc char[UnitReadout.MostRows * UnitReadout.RoomPerRow];
        Span<int> ends = stackalloc int[UnitReadout.MostRows];

        // Written before the panel is laid out rather than counted twice: what a row has to say is what
        // decides whether it is there at all, and a predicted count is a row drawn through the panel's own
        // bottom edge the day one of them learns a condition.
        Rows = Open ? UnitReadout.Lines(world, unit, watching, text, ends) : 0;

        var widthPx = Open
            ? GlyphSheet.WidthPx(WidestLine, Theme.SmallTextPx) + Theme.PaddingPx * 2f
            : GlyphSheet.WidthPx(TitleLine, Theme.TextPx) + Theme.PaddingPx * 1.2f;
        var heightPx = StatusPanel.HeightFor(Rows);
        Box = new Rect(
            new Vector2(Theme.MarginPx, MathF.Max(Theme.MarginPx, uiPx.Y - Theme.MarginPx - heightPx)),
            new Vector2(widthPx, heightPx));
        Theme.Frame(ref draw, Box);

        Span<char> head = stackalloc char[TitleLine * 2];
        Title(ref draw, pointerPx, head, world, unit);
        if (Rows == 0) return;

        Theme.Separator(
            ref draw, Box.AtPx + new Vector2(Theme.PaddingPx * 0.6f, Theme.GapPx + TitleRowPx + Theme.GapPx),
            Box.SizePx.X - Theme.PaddingPx * 1.2f);

        for (var row = 0; row < Rows; row++)
        {
            var from = row > 0 ? ends[row - 1] : 0;
            draw.TextFitted(
                Box.AtPx + new Vector2(Theme.PaddingPx * 0.6f, StatusPanel.BodyTopPx + row * RowPitchPx),
                text[from..ends[row]], Theme.SmallTextPx, Theme.Text, Box.SizePx.X - Theme.PaddingPx);
        }
    }

    /// <summary>
    /// What this is talking about, on the one line that is there whether the body is open or shut: the
    /// unit, or how many of them there are — and the release button at its end while any of them is the player's.
    /// </summary>
    void Title(ref ScreenDraw draw, Vector2 pointerPx, scoped Span<char> into, TownWorld world, Selection unit)
    {
        _title = new Rect(
            Box.AtPx + new Vector2(Theme.EdgePx, Theme.GapPx),
            new Vector2(Box.SizePx.X - Theme.EdgePx * 2f, TitleRowPx));

        var releaseWidthPx = GlyphSheet.WidthPx(ReleaseLabel.Length, Theme.SmallTextPx) + Theme.InsetPx * 2f;
        _release = world.SelectionIsManual
            ? new Rect(new Vector2(_title.Right - releaseWidthPx, _title.AtPx.Y), new Vector2(releaseWidthPx, TitleRowPx))
            : default;
        var headingWidthPx = _title.SizePx.X - _release.SizePx.X;

        if (_title.Contains(pointerPx) && !_release.Contains(pointerPx))
        {
            draw.RoundedRect(_title.AtPx, new Vector2(headingWidthPx, TitleRowPx), Theme.RowRadiusPx, Theme.RowHover);
        }

        var line = new TextBuffer(into);
        line.Add(Open ? "- " : "+ ");
        if (world.SelectedCount > 1)
        {
            line.Add(world.SelectedCount);
            line.Add(" units");
        }
        else
        {
            line.Add(unit.Kind switch
            {
                SelectionKind.Car => "car ",
                SelectionKind.Locomotive => "train ",
                SelectionKind.Wagon => "wagon ",
                _ => "walker ",
            });
            line.Add(unit.Index);
        }

        draw.TextFitted(
            _title.AtPx + new Vector2(Theme.PaddingPx * 0.6f, (TitleRowPx - Theme.TextPx) * 0.5f), line.Written,
            Theme.TextPx, Theme.Heading, headingWidthPx - Theme.PaddingPx);

        if (_release.SizePx.X > 0f) Theme.Button(ref draw, _release, pointerPx, ReleaseLabel, textPx: Theme.SmallTextPx);
    }
}
