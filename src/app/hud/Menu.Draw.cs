using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Hud;

/// <summary>Drawing one page: the two groups of maps, or the debug switches.</summary>
internal sealed partial class Menu
{
    public void Draw(ref ScreenDraw draw, Vector2 uiPx, Rect anchor, Vector2 pointerPx, DebugSwitches switches)
    {
        if (_laidFor != uiPx || _laidAt != anchor) Lay(uiPx, anchor);

        Theme.Frame(ref draw, Box);
        draw.Text(
            Box.AtPx + new Vector2(Theme.PaddingPx, Theme.PaddingPx), "traffic-dotnet", Theme.HeadingPx, Theme.Heading);
        Theme.Separator(
            ref draw, Box.AtPx + new Vector2(Theme.PaddingPx, RuleTopPx), Box.SizePx.X - Theme.PaddingPx * 2f);

        for (var tab = 0; tab < _tabs.Length; tab++)
        {
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

        if (Page == Maps) DrawMaps(ref draw, pointerPx);
        else DrawSwitches(ref draw, pointerPx, switches);
    }

    void DrawMaps(ref ScreenDraw draw, Vector2 pointerPx)
    {
        // The two lines of a map row are centred on it together, so a row's name sits the same distance
        // from its top edge as the line under it does from its bottom.
        var firstLinePx = (Theme.TallRowPx - (Theme.TextPx + Theme.GapPx * 0.5f + Theme.SmallTextPx)) * 0.5f;
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
                line.Add(_groupOpen[group] ? "- " : "+ ");
                line.Add(_rowNames[row]);
                draw.TextFitted(
                    box.AtPx + new Vector2(Theme.InsetPx, (Theme.RowPx - Theme.TextPx) * 0.5f), line.Written,
                    Theme.TextPx, Theme.Heading, fitPx);
                continue;
            }

            Theme.Face(ref draw, box, pointerPx);
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
