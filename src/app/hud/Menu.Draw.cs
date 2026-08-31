using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Hud;

/// <summary>Drawing one page: the two groups of maps, or the debug switches.</summary>
internal sealed partial class Menu
{
    public void Draw(
        ref ScreenDraw draw, Vector2 uiPx, Rect anchor, Vector2 pointerPx, DebugSwitches switches, TrimFigures trims)
    {
        if (!LaidFor(uiPx, anchor)) Lay(uiPx, anchor);

        Theme.Frame(ref draw, Box);

        // The title sits on the middle of its own band, which on the start menu is as tall as the way out
        // standing at the end of it and on the popup is the title's own line.
        var titlePx = (TitleHeightPx(AtTheStart) - Theme.HeadingPx) * 0.5f;
        draw.Text(
            Box.AtPx + new Vector2(Theme.PaddingPx, Theme.PaddingPx + titlePx), Title, Theme.HeadingPx,
            Theme.Heading);

        Theme.Separator(
            ref draw, Box.AtPx + new Vector2(Theme.PaddingPx, RuleTopPx(AtTheStart)),
            Box.SizePx.X - Theme.PaddingPx * 2f);

        for (var tab = 0; tab < _tabs.Length; tab++)
        {
            // A tab this layout does not carry was laid as no rectangle at all.
            if (_tabs[tab].SizePx.X <= 0f) continue;

            // The way out is a button standing in the tab strip rather than a page: it is the one
            // thing on the menu that does not come back.
            if (tab == ExitTab)
            {
                Theme.Button(ref draw, _tabs[tab], pointerPx, TabNames[tab], Theme.Danger);
                continue;
            }

            var picked = tab == Page;
            Theme.Face(ref draw, _tabs[tab], pointerPx, picked ? Theme.RowPicked : Theme.RowRest, picked);
            draw.TextFitted(
                _tabs[tab].AtPx + new Vector2(Theme.InsetPx, (Theme.RowPx - Theme.TextPx) * 0.5f), TabNames[tab],
                Theme.TextPx, picked ? Theme.Text : Theme.Dim, Theme.FitWidthPx(_tabs[tab]));
        }

        switch (Page)
        {
            case Maps: DrawMaps(ref draw, pointerPx); break;
            case Figures: DrawTrims(ref draw, pointerPx, trims); break;
            default: DrawSwitches(ref draw, pointerPx, switches); break;
        }
    }

    void DrawMaps(ref ScreenDraw draw, Vector2 pointerPx)
    {
        // The lines of a map row are centred on it together, so a row's name sits the same distance from
        // its top edge as the last line under it does from its bottom.
        var namePx = NamePx(AtTheStart);
        Span<char> text = stackalloc char[32];

        for (var slot = 0; slot < _shownRows && _firstRow + slot < _rowCount; slot++)
        {
            var box = _rows[slot];
            var row = _firstRow + slot;
            var group = _rowGroup[row];
            var fitPx = Theme.FitWidthPx(box);

            if (group >= 0)
            {
                Theme.Face(ref draw, box, pointerPx, Theme.RowRest);
                var line = new TextBuffer(text);
                line.Add(_groupOpen[group] ? GroupMark : "+ ");
                line.Add(_rowNames[row]);
                draw.TextFitted(
                    box.AtPx + new Vector2(Theme.InsetPx, (Theme.RowPx - namePx) * 0.5f), line.Written,
                    namePx, Theme.Heading, fitPx);
                continue;
            }

            Theme.Face(ref draw, box, pointerPx);

            var lines = _rowLines[row];
            var downPx = (box.SizePx.Y - TextHeightPx(namePx, lines)) * 0.5f;
            draw.TextFitted(
                box.AtPx + new Vector2(Theme.InsetPx, downPx), _rowNames[row], namePx, Theme.Text, fitPx);

            downPx += namePx;
            var description = _rowDescriptions[row].AsSpan();
            foreach (var line in LinesOf(row)[..lines])
            {
                downPx += Theme.GapPx * 0.5f;
                draw.TextFitted(
                    box.AtPx + new Vector2(Theme.InsetPx, downPx), description[line], Theme.SmallTextPx,
                    Theme.Dim, fitPx);

                downPx += Theme.SmallTextPx;
            }
        }

        if (Scrolls) ScrollBar(ref draw);
    }

    /// <summary>How far down the page the rows on screen are, as a bar beside them.</summary>
    void ScrollBar(ref ScreenDraw draw)
    {
        var trackPx = _rows[_shownRows - 1].Bottom - _rows[0].AtPx.Y;
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
        for (var line = 0; line < MostLines; line++)
        {
            Check(ref draw, _lines[line], pointerPx, Lines[line], Switch(switches, line));
        }
    }

    /// <summary>
    /// One row a figure: what it is called, what share of the shipped figure it is standing at, and the
    /// track it is dragged along. <b>The track fills from the middle rather than from its left end</b>,
    /// because the middle is what the build ships and which way a figure has been taken is the reading.
    /// </summary>
    void DrawTrims(ref ScreenDraw draw, Vector2 pointerPx, TrimFigures trims)
    {
        Span<char> text = stackalloc char[16];
        var firstLinePx = (Theme.TallRowPx - (Theme.TextPx + Theme.GapPx + TrackPx)) * 0.5f;

        for (var trim = 0; trim < TrimFigures.Count; trim++)
        {
            var box = _trims[trim];
            var share = trims.Of(trim);
            var shipped = share == 1f;
            Theme.Face(ref draw, box, pointerPx, trim == _held ? Theme.RowPicked : Theme.RowRest, trim == _held);

            var namePx = box.AtPx + new Vector2(Theme.InsetPx, firstLinePx);
            var reading = new TextBuffer(text);
            reading.Add((int)MathF.Round(share * 100f));
            reading.Add("%");
            var readingPx = GlyphSheet.WidthPx(reading.Written.Length, Theme.TextPx);
            draw.TextFitted(
                namePx, TrimFigures.Names[trim], Theme.TextPx, Theme.Text,
                box.Right - Theme.InsetPx - readingPx - Theme.GapPx - namePx.X);
            draw.Text(
                new Vector2(box.Right - Theme.InsetPx - readingPx, firstLinePx + box.AtPx.Y), reading.Written,
                Theme.TextPx, shipped ? Theme.Dim : Theme.Heading);

            Track(ref draw, box, firstLinePx + Theme.TextPx + Theme.GapPx, share);
        }

        // The row under the tracks, which is not one of them: it is the way back to the build's own figures
        // and it is dim while there is nothing to come back from.
        var reset = _trims[ResetRow];
        Theme.Button(
            ref draw, reset, pointerPx, "Reset to shipped", trims.Untouched ? Theme.RowRest : Theme.Accent);
    }

    /// <summary>The track a trim is dragged along, with the shipped figure marked at the middle of it.</summary>
    static void Track(ref ScreenDraw draw, Rect box, float downPx, float share)
    {
        var atPx = box.AtPx + new Vector2(TrackInsetPx, downPx);
        var widthPx = TrackWidthPx(box);
        draw.RoundedRect(atPx, new Vector2(widthPx, TrackPx), TrackPx * 0.5f, Theme.PanelEdge);

        // From the middle to where the figure stands, so a doubled figure and a halved one read as the
        // same length of bar either side of shipped.
        var middlePx = WhereOnTheTrack(1f) * widthPx;
        var standsPx = WhereOnTheTrack(share) * widthPx;
        var fromPx = MathF.Min(middlePx, standsPx);
        draw.RoundedRect(
            atPx + new Vector2(fromPx, 0f), new Vector2(MathF.Abs(standsPx - middlePx), TrackPx), TrackPx * 0.5f,
            Theme.Accent);

        draw.Rect(atPx + new Vector2(middlePx - (Theme.EdgePx * 0.5f), -ShippedTickPx), new Vector2(
            Theme.EdgePx, TrackPx + (ShippedTickPx * 2f)), Theme.Dim);

        var knob = new Rect(
            atPx + new Vector2(standsPx - (KnobPx * 0.5f), (TrackPx - KnobPx) * 0.5f), new Vector2(KnobPx));
        draw.RoundedRect(knob.AtPx, knob.SizePx, KnobPx * 0.5f, Theme.Text);
    }

    const float TrackPx = 4f;
    const float KnobPx = 12f;

    /// <summary>How far the mark for the shipped figure stands proud of the track either side of it.</summary>
    const float ShippedTickPx = 4f;

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
