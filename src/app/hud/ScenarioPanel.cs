using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>OBS-2i — what the map on screen claims about itself, and whether it is keeping it</b>: one
/// collapsible panel along the bottom of the window, a row a claim, with the figures behind each verdict
/// beside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the panel form of what a headless run prints</b> (<see cref="ScenarioReport"/>) and reads the
/// same watches (<see cref="ScenarioWatch"/>). A second answer drawn here would be a panel that could
/// disagree with the run's own exit code, which is not a thing anybody could resolve by looking at the
/// town.
/// </para>
/// <para>
/// <b>The title says the count and the body says the claims</b>, which is the two depths a scenario is
/// read at: whether the town is keeping what it claims is a glance, and which claim is broken and on what
/// figures is a panel of rows nobody wants covering the map until they ask for it. It opens shut, and one
/// click on the title opens it.
/// </para>
/// <para>
/// <b>It stands along the bottom because a claim is a line of prose</b>, not a figure: the rows are as
/// wide as a sentence and the corners are already spoken for — the run's own read-out has the top-left,
/// the buttons the top-right, and the scale legend the bottom-right, which this panel stops short of.
/// </para>
/// </remarks>
internal sealed class ScenarioPanel
{
    /// <summary>Where the claim starts on a row, and where the figures behind it do, in characters.</summary>
    const int ClaimColumn = 10;

    const int FiguresColumn = ClaimColumn + 74;

    /// <summary>The longest line a row comes to, which is what the panel is sized on before a word of it is drawn.</summary>
    const int WidestLine = FiguresColumn + 64;

    /// <summary>And what the title is sized on: the map's name, the counts and the marker in front of them.</summary>
    const int TitleLine = 52;

    const float RowPitchPx = Theme.SmallTextPx + 4f;

    /// <summary>The band the title occupies, which is the whole of the panel while the body is shut.</summary>
    const float TitleRowPx = Theme.TextPx + 10f;

    /// <summary>How far the panel keeps clear of the legend in the corner it grows towards.</summary>
    const float ClearOfTheLegendPx = 200f;

    Rect _title;

    /// <summary>
    /// Whether the body is showing. <b>Shut by default</b>: the title alone says whether the map is
    /// keeping what it claims, and the rows under it are what somebody asks for once it is not.
    /// </summary>
    public bool Open { get; private set; }

    /// <summary>The whole panel, so a click that lands on it is not also a click on the town behind it.</summary>
    public Rect Box { get; private set; }

    /// <summary>
    /// How many rows the last draw wrote, against the count the panel was sized from before any of them
    /// went down. A watch that grew a row without the count following it draws that row through the
    /// panel's own bottom edge.
    /// </summary>
    public int Rows { get; private set; }

    public void Show() => Open = true;

    public void Toggle() => Open = !Open;

    /// <summary>The town has changed under it, so what was drawn for the last map takes no more clicks.</summary>
    public void TownChanged()
    {
        Box = default;
        _title = default;
    }

    /// <summary>A click on the panel: the title opens and shuts the body, and anywhere else on it is taken and dropped.</summary>
    public bool Click(Vector2 atPx)
    {
        if (!Box.Contains(atPx)) return false;

        if (_title.Contains(atPx)) Open = !Open;
        return true;
    }

    /// <summary>How tall the panel is with that many rows under its title.</summary>
    public static float HeightFor(int rows) => Theme.GapPx + TitleRowPx
                                               + (rows > 0
                                                   ? Theme.GapPx + Theme.EdgePx + Theme.GapPx + (rows * RowPitchPx)
                                                     + Theme.PaddingPx
                                                   : Theme.GapPx);

    public void Draw(ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerPx, string mapName, ReadOnlySpan<ScenarioWatch> watching)
    {
        if (watching.Length == 0)
        {
            TownChanged();
            return;
        }

        var wantedPx = Open
            ? GlyphSheet.WidthPx(WidestLine, Theme.SmallTextPx) + (Theme.PaddingPx * 2f)
            : GlyphSheet.WidthPx(TitleLine, Theme.TextPx) + (Theme.PaddingPx * 1.2f);

        // Never wider than the window can hold beside the legend: a panel that ran under the scale bar
        // would be two instruments drawn over one another in the corner nothing else is allowed to use.
        var widthPx = MathF.Min(wantedPx, MathF.Max(TitleRowPx, uiPx.X - (Theme.MarginPx * 2f) - ClearOfTheLegendPx));
        var heightPx = HeightFor(Open ? RowCount(watching) : 0);
        Box = new Rect(new Vector2(Theme.MarginPx, uiPx.Y - Theme.MarginPx - heightPx), new Vector2(widthPx, heightPx));
        Theme.Frame(ref draw, Box);

        Span<char> text = stackalloc char[240];
        Title(ref draw, pointerPx, text, mapName, watching);

        Rows = 0;
        if (!Open) return;

        Theme.Separator(
            ref draw, Box.AtPx + new Vector2(Theme.PaddingPx * 0.6f, Theme.GapPx + TitleRowPx + Theme.GapPx),
            Box.SizePx.X - (Theme.PaddingPx * 1.2f));

        var row = 0;
        foreach (var watch in watching)
        {
            Heading(ref draw, ref row, text, watch);
            for (var claim = 0; claim < watch.Claims; claim++) Claim(ref draw, ref row, text, watch, claim);
            for (var reading = 0; reading < watch.Readings; reading++) Reading(ref draw, ref row, text, watch, reading);
        }

        Rows = row;
    }

    /// <summary>
    /// <b>What the map claims and how it is doing</b>, in the one line that is on screen whether or not
    /// the body is. A broken claim is named in the count rather than only in the rows: the panel is shut
    /// by default, and a run that has broken something has to say so without being opened.
    /// </summary>
    void Title(
        ref ScreenDraw draw, Vector2 pointerPx, scoped Span<char> text, string mapName,
        ReadOnlySpan<ScenarioWatch> watching)
    {
        _title = new Rect(
            Box.AtPx + new Vector2(Theme.EdgePx, Theme.GapPx),
            new Vector2(Box.SizePx.X - (Theme.EdgePx * 2f), TitleRowPx));
        if (_title.Contains(pointerPx)) draw.RoundedRect(_title.AtPx, _title.SizePx, Theme.RowRadiusPx, Theme.RowHover);

        var kept = 0;
        var broken = 0;
        var waiting = 0;
        foreach (var watch in watching)
        {
            kept += watch.Kept;
            broken += watch.Broken;
            waiting += watch.Unanswered;
        }

        var line = new TextBuffer(text);
        line.Add(Open ? "- " : "+ ");
        line.Add(mapName);
        line.Add(": ");
        line.Add(kept);
        line.Add(" kept, ");
        line.Add(broken);
        line.Add(" broken, ");
        line.Add(waiting);
        line.Add(" waiting");

        draw.TextFitted(
            _title.AtPx + new Vector2(Theme.PaddingPx * 0.6f, (TitleRowPx - Theme.TextPx) * 0.5f), line.Written,
            Theme.TextPx, broken > 0 ? Theme.Broken : Theme.Heading, _title.SizePx.X - Theme.PaddingPx);
    }

    /// <summary>One watch's own heading: what this set of claims is about, and what the map was laid to answer.</summary>
    void Heading(ref ScreenDraw draw, ref int row, scoped Span<char> text, ScenarioWatch watch)
    {
        var line = new TextBuffer(text);
        line.Add(watch.Name);
        line.PadTo(ClaimColumn + 12);
        line.Add(watch.Subject);
        Write(ref draw, ref row, line.Written, Theme.Heading);
    }

    void Claim(ref ScreenDraw draw, ref int row, scoped Span<char> text, ScenarioWatch watch, int claim)
    {
        var verdict = watch.Verdict(claim);
        var line = new TextBuffer(text);
        line.Add("  ");
        line.Add(ScenarioReport.Word(verdict));
        line.PadTo(ClaimColumn);
        line.Add(watch.Asks(claim));
        line.PadTo(FiguresColumn);
        watch.Says(claim, ref line);

        Write(ref draw, ref row, line.Written, verdict switch
        {
            ClaimVerdict.Kept => Theme.Text,
            ClaimVerdict.Broken => Theme.Broken,
            _ => Theme.Dim,
        });
    }

    /// <summary>A reading: a figure with no verdict beside it, because nothing here fails a run.</summary>
    void Reading(ref ScreenDraw draw, ref int row, scoped Span<char> text, ScenarioWatch watch, int reading)
    {
        var line = new TextBuffer(text);
        line.Add("  quoted");
        line.PadTo(ClaimColumn);
        line.Add(watch.Reading(reading));
        line.PadTo(FiguresColumn);
        watch.Reads(reading, ref line);
        Write(ref draw, ref row, line.Written, Theme.Dim);
    }

    static int RowCount(ReadOnlySpan<ScenarioWatch> watching)
    {
        var rows = 0;
        foreach (var watch in watching) rows += 1 + watch.Claims + watch.Readings;

        return rows;
    }

    void Write(ref ScreenDraw draw, ref int row, scoped ReadOnlySpan<char> text, Vector4 colour)
    {
        var atPx = Box.AtPx + new Vector2(Theme.PaddingPx * 0.6f, BodyTopPx + (row * RowPitchPx));
        draw.TextFitted(atPx, text, Theme.SmallTextPx, colour, Box.SizePx.X - Theme.PaddingPx);
        row++;
    }

    /// <summary>Where the first body row's text starts: under the title band, past the rule that separates them.</summary>
    static float BodyTopPx => Theme.GapPx + TitleRowPx + Theme.GapPx + Theme.EdgePx + Theme.GapPx;
}
