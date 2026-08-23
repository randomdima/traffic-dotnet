using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Hud;

/// <summary>Drawing one page: the list, the switches, the seeds, the pace and the legend.</summary>
internal sealed partial class Menu
{
    public void Draw(
        ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerPx, bool hasTown, DebugSwitches switches, RunState run,
        ulong worldSeed, ulong agentSeed)
    {
        if (_laidFor != uiPx || _laidWithTown != hasTown) Lay(uiPx, hasTown);

        // Over a town the screen behind is dimmed, because the panel does not cover it and a click
        // that got past it would reach a town nobody is looking at.
        if (hasTown) draw.Rect(Vector2.Zero, uiPx, Theme.Scrim);

        Theme.Frame(ref draw, Box);
        draw.Text(
            Box.AtPx + new Vector2(Theme.PaddingPx, Theme.PaddingPx), "traffic-dotnet", Theme.HeadingPx, Theme.Heading);
        Theme.Separator(
            ref draw, Box.AtPx + new Vector2(Theme.PaddingPx, RuleTopPx), Box.SizePx.X - Theme.PaddingPx * 2f);

        for (var tab = 0; tab < _tabs.Length; tab++)
        {
            var picked = tab == Page;
            Theme.Face(ref draw, _tabs[tab], pointerPx, picked ? Theme.RowPicked : Theme.RowRest, picked);
            draw.TextFitted(
                _tabs[tab].AtPx + new Vector2(Theme.InsetPx, (Theme.RowPx - Theme.TextPx) * 0.5f), PageNames[tab],
                Theme.TextPx, picked ? Theme.Text : Theme.Dim, Theme.FitWidthPx(_tabs[tab]));
        }

        if (IsList(Page)) DrawList(ref draw, pointerPx);
        else if (NeedsTown(Page) && !hasTown) Line(ref draw, _lines[0], NoTown, Theme.TextPx, Theme.Dim);
        else if (Page == Layers) DrawSwitches(ref draw, pointerPx, switches);
        else if (Page == Seeds) DrawSeeds(ref draw, pointerPx, worldSeed, agentSeed);
        else if (Page == Pace) DrawPace(ref draw, pointerPx, run);
        else DrawLegend(ref draw);

        if (hasTown) Theme.Button(ref draw, _close, pointerPx, "Close  (Esc)");
        Theme.Button(ref draw, _quit, pointerPx, "Exit game", Theme.Danger);

        if (Page == Checks && LastOutput.Length > 0) Output(ref draw, uiPx);
    }

    void DrawList(ref ScreenDraw draw, Vector2 pointerPx)
    {
        // The two lines are centred on the row together, so a row's name sits the same distance from
        // its top edge as the line under it does from its bottom.
        var firstLinePx = (Theme.TallRowPx - (Theme.TextPx + Theme.GapPx * 0.5f + Theme.SmallTextPx)) * 0.5f;

        for (var slot = 0; slot < _shownRows && _firstRow + slot < _rowCount; slot++)
        {
            var box = _rows[slot];
            var row = _firstRow + slot;
            Theme.Face(ref draw, box, pointerPx);

            var fitPx = Theme.FitWidthPx(box);
            draw.TextFitted(
                box.AtPx + new Vector2(Theme.InsetPx, firstLinePx), _rowNames[row], Theme.TextPx, Theme.Text, fitPx);
            draw.TextFitted(
                box.AtPx + new Vector2(Theme.InsetPx, firstLinePx + Theme.TextPx + Theme.GapPx * 0.5f),
                _rowDescriptions[row], Theme.SmallTextPx, Theme.Dim, fitPx);
        }

        if (Scrolls) ScrollBar(ref draw);
    }

    /// <summary>How far down the page the rows on screen are, as a bar beside them.</summary>
    void ScrollBar(ref ScreenDraw draw)
    {
        var trackPx = _shownRows * RowPitchPx - Theme.GapPx;
        var atX = _rows[0].Right + Theme.GapPx;
        var atY = _rows[0].AtPx.Y;
        draw.RoundedRect(new Vector2(atX, atY), new Vector2(ScrollBarPx, trackPx), ScrollBarPx * 0.5f, Theme.RowRest);

        var thumbPx = MathF.Max(Theme.RowPx, trackPx * _shownRows / _rowCount);
        var travelPx = (trackPx - thumbPx) * _firstRow / (_rowCount - _shownRows);
        draw.RoundedRect(
            new Vector2(atX, atY + travelPx), new Vector2(ScrollBarPx, thumbPx), ScrollBarPx * 0.5f, Theme.Accent);
    }

    void DrawSwitches(ref ScreenDraw draw, Vector2 pointerPx, DebugSwitches switches)
    {
        Check(ref draw, _lines[0], pointerPx, "Frame read-out", switches.FrameReadout);
        Check(ref draw, _lines[1], pointerPx, "Car lines", switches.CarLines);
        Check(ref draw, _lines[2], pointerPx, "Walker lines", switches.WalkerLines);
        Check(ref draw, _lines[3], pointerPx, "Nodes and links", switches.Nodes);
        Check(ref draw, _lines[4], pointerPx, "Lane reservations", switches.Reservations);
        Check(ref draw, _lines[5], pointerPx, "Collision", switches.Collision);
        Check(ref draw, _lines[6], pointerPx, "Ruler", switches.Ruler);
        Check(ref draw, _lines[7], pointerPx, "Track figures", switches.TrackFigures);
    }

    void DrawSeeds(ref ScreenDraw draw, Vector2 pointerPx, ulong worldSeed, ulong agentSeed)
    {
        Span<char> text = stackalloc char[48];
        var line = new TextBuffer(text);
        line.Add("World seed   ");
        line.Add(worldSeed);
        Line(ref draw, _lines[0], line.Written, Theme.TextPx, Theme.Text);

        line.Clear();
        line.Add("Agent seed   ");
        line.Add(agentSeed);
        Line(ref draw, _lines[1], line.Written, Theme.TextPx, Theme.Text);

        Line(
            ref draw, _lines[2], "The world seed is the town file's own and changes with the map.", Theme.SmallTextPx,
            Theme.Dim);
        Theme.Button(ref draw, _lines[3], pointerPx, "Re-roll the agent seed and rebuild");
    }

    void DrawPace(ref ScreenDraw draw, Vector2 pointerPx, RunState run)
    {
        Span<char> text = stackalloc char[48];
        var line = new TextBuffer(text);
        line.Add("Pace   ");
        line.Add(run.TimeScale, "F1");
        line.Add(run.Frozen ? "x   (frozen)" : "x");
        Line(ref draw, _lines[0], line.Written, Theme.TextPx, Theme.Text);

        Check(ref draw, _lines[1], pointerPx, "Agents held  (Pause)", run.AgentsHeld);
        Line(ref draw, _lines[2], "1 / 2 / 3   set the pace, capped at 3x", Theme.SmallTextPx, Theme.Dim);
        Line(ref draw, _lines[3], "`           freeze and unfreeze", Theme.SmallTextPx, Theme.Dim);

        // Two lines of one sentence, at the legend's pitch rather than at a row's: a wrap set a row
        // apart reads as two statements.
        var atPx = _lines[5].AtPx
            + new Vector2(Theme.InsetPx, (Theme.RowPx - Theme.SmallTextPx * 2f - Theme.GapPx) * 0.5f);
        var fitPx = Theme.FitWidthPx(_lines[5]);
        draw.TextFitted(
            atPx, "A pace above 4x integrates the physics coarsely and manufactures", Theme.SmallTextPx, Theme.Dim,
            fitPx);
        draw.TextFitted(
            atPx + new Vector2(0f, LegendPitchPx), "collisions the model never had, so the cap is kept.",
            Theme.SmallTextPx, Theme.Dim, fitPx);
    }

    /// <summary>
    /// <b>Every control the player has</b>, which is the claim the reference frame makes of this
    /// page: the camera, the selection, the orders, the drive keys, the handbrake, the pace and
    /// freeze keys, the lane-graph key, fullscreen, and Escape itself.
    /// </summary>
    void DrawLegend(ref ScreenDraw draw)
    {
        for (var row = 0; row * 2 + 1 < ControlLegend.Length; row++)
        {
            var atPx = _legend.AtPx + new Vector2(Theme.InsetPx, row * LegendPitchPx);
            draw.TextFitted(atPx, ControlLegend[row * 2], Theme.SmallTextPx, Theme.Heading, LegendKeyWidthPx);
            draw.TextFitted(
                atPx + new Vector2(LegendKeyWidthPx, 0f), ControlLegend[row * 2 + 1], Theme.SmallTextPx, Theme.Text,
                _legend.SizePx.X - Theme.InsetPx - LegendKeyWidthPx);
        }
    }

    /// <summary>
    /// The last check's own printing, beside the menu rather than over it — the corner overlay a
    /// watched check owes somebody who has no terminal.
    /// </summary>
    void Output(ref ScreenDraw draw, Vector2 uiPx)
    {
        var widthPx = MathF.Min(680f, Box.AtPx.X - Theme.PaddingPx * 2f);
        if (widthPx < 200f) return;

        var pitchPx = Theme.SmallTextPx + Theme.GapPx * 0.5f;
        var lines = Math.Min(LastOutput.Length, (int)((uiPx.Y - Theme.PaddingPx * 6f) / pitchPx));
        var box = new Rect(
            new Vector2(Theme.PaddingPx, Theme.PaddingPx * 3f),
            new Vector2(widthPx, lines * pitchPx + Theme.PaddingPx * 2f));
        Theme.Frame(ref draw, box);

        var first = Math.Max(0, LastOutput.Length - lines);
        for (var line = 0; line < lines; line++)
        {
            draw.TextFitted(
                box.AtPx + new Vector2(Theme.PaddingPx, Theme.PaddingPx + line * pitchPx), LastOutput[first + line],
                Theme.SmallTextPx, Theme.Text, widthPx - Theme.PaddingPx * 2f);
        }
    }

    /// <summary>A row that says something rather than doing it: no face under it, and the same inset as one that does.</summary>
    static void Line(ref ScreenDraw draw, Rect box, scoped ReadOnlySpan<char> text, float textPx, Vector4 colour) =>
        draw.TextFitted(
            box.AtPx + new Vector2(Theme.InsetPx, (box.SizePx.Y - textPx) * 0.5f), text, textPx, colour,
            Theme.FitWidthPx(box));

    static void Check(ref ScreenDraw draw, Rect box, Vector2 pointerPx, scoped ReadOnlySpan<char> name, bool on)
    {
        if (box.Contains(pointerPx)) draw.RoundedRect(box.AtPx, box.SizePx, Theme.RowRadiusPx, Theme.RowHover);

        var tickPx = Theme.RowPx - Theme.InsetPx;
        var tick = new Rect(
            box.AtPx + new Vector2(Theme.InsetPx, (box.SizePx.Y - tickPx) * 0.5f), new Vector2(tickPx));
        draw.Outline(tick.AtPx, tick.SizePx, Theme.EdgePx, on ? Theme.Accent : Theme.PanelEdge);
        if (on) draw.Rect(tick.Inset(3f).AtPx, tick.Inset(3f).SizePx, Theme.Accent);

        var textAtPx = box.AtPx + new Vector2(Theme.InsetPx * 2f + tickPx, (box.SizePx.Y - Theme.TextPx) * 0.5f);
        draw.TextFitted(
            textAtPx, name, Theme.TextPx, on ? Theme.Text : Theme.Dim, box.Right - Theme.InsetPx - textAtPx.X);
    }
}
