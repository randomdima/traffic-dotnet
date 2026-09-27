using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>The debug page: the sections down one side and the one picked beside them</b> (OBS-2y). A section is a
/// thing a session is opened to look at, and it carries every switch and every figure that question turns —
/// so what is on the glass at once is one question's worth of rows, never every switch the slice owns.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each switch says what it draws and what its colours mean</b>, under its name and beside it: a layer
/// read off a picture has to be told apart from the one beside it by colour, and a key kept anywhere but
/// on the row that throws it is a key nobody has open while looking.
/// </para>
/// <para>
/// <b>A section's row says how many of its layers are on</b> without being opened, which is how a reader
/// finds the layer still drawing over the town after they have moved on to another section.
/// </para>
/// </remarks>
internal sealed partial class Menu
{
    static int Sections => DebugLayers.Sections.Length;

    /// <summary>Which section of the debug page is showing. It is kept while the menu is shut, so the gear opens back onto it.</summary>
    public int Section { get; private set; }

    public void OpenSection(int section) => Section = Math.Clamp(section, 0, Sections - 1);

    readonly Rect[] _sections = new Rect[DebugLayers.Sections.Length];
    readonly Rect[] _layerRows = new Rect[DebugLayers.MostInASection];
    Rect _allOff;

    /// <summary>Where the rule between the sections and the pane stands, and where the one over a section's figures does.</summary>
    Rect _paneRule;

    Rect _figuresRule;

    /// <summary>A switch carries its name over the line saying what it draws, as a map row carries its own.</summary>
    const float SwitchPitchPx = Theme.TallRowPx + Theme.GapPx;

    /// <summary>What a section keeps between its switches and its figures, with a rule in the middle of it.</summary>
    const float FiguresGapPx = Theme.GapPx * 3f;

    /// <summary>What the sections keep between themselves and the pane beside them, with a rule in the middle of it.</summary>
    const float PaneGapPx = Theme.PaddingPx * 2f;

    /// <summary>
    /// The most of the content column the sections take. <b>The pane is what is read</b>, so on a window too
    /// narrow for both the names down the side are cut before the rows beside them are.
    /// </summary>
    const float MostSidebarShare = 0.34f;

    /// <summary>The box a switch's tick is drawn in.</summary>
    const float TickPx = Theme.RowPx - Theme.InsetPx;

    /// <summary>A colour in a switch's key: a short bar, the weight of a line on the town and not of a panel.</summary>
    static readonly Vector2 SwatchPx = new(14f, 4f);

    /// <summary>How many of a section's switches are on, room for which the section's row keeps at its end.</summary>
    static readonly float BadgeRoomPx = GlyphSheet.WidthPx("9 on".Length, Theme.SmallTextPx) + Theme.InsetPx;

    static readonly float SidebarWantedPx = SidebarWanted();

    static float SidebarWanted()
    {
        var widestPx = GlyphSheet.WidthPx(AllOffLabel.Length, Theme.TextPx);
        foreach (var section in DebugLayers.Sections)
        {
            widestPx = MathF.Max(widestPx, GlyphSheet.WidthPx(section.Name.Length, Theme.TextPx) + BadgeRoomPx);
        }

        return widestPx + (Theme.InsetPx * 2f);
    }

    const string AllOffLabel = "All layers off";

    static float SidebarWidthPx(float contentWidthPx) => MathF.Min(SidebarWantedPx, contentWidthPx * MostSidebarShare);

    static float PaneWidthPx(float contentWidthPx) => contentWidthPx - SidebarWidthPx(contentWidthPx) - PaneGapPx;

    /// <summary>
    /// What the page wants to be laid at so that nothing on it is cut: the sections, and the widest of a
    /// switch's name with its key and the line under it.
    /// </summary>
    /// <remarks>A property and not a field, because it reads figures declared in another half of this class.</remarks>
    static float DebugWantedPx
    {
        get
        {
            var paneWantedPx = GroundColumnLeastPx;
            foreach (var entry in DebugLayers.All)
            {
                var namePx = TickRoomPx + GlyphSheet.WidthPx(entry.Name.Length, Theme.TextPx) + Theme.GapPx * 2f
                             + KeyWidthPx(entry.Key) + Theme.InsetPx;
                var saysPx = TickRoomPx + GlyphSheet.WidthPx(entry.Says.Length, Theme.SmallTextPx) + Theme.InsetPx;
                paneWantedPx = MathF.Max(paneWantedPx, MathF.Max(namePx, saysPx));
            }

            var sliderPx = MathF.Max(
                WidestPx(TrimFigures.Names, Theme.TextPx), GlyphSheet.WidthPx(ShellProbe.Named.Length, Theme.TextPx));
            paneWantedPx = MathF.Max(paneWantedPx, sliderPx + TrimShareRoomPx);

            return SidebarWantedPx + PaneGapPx + paneWantedPx;
        }
    }

    /// <summary>What one of the ground's layers wants: its tick, its name, and what it came to.</summary>
    static float GroundColumnLeastPx =>
        TickRoomPx + WidestPx(GroundParts.Names, Theme.TextPx) + Theme.GapPx + GroundReadingRoomPx;

    /// <summary>
    /// <b>The ground's layers stand in two columns wherever a column still has room for a short name beside
    /// its reading</b>: nine rows down one column was the tallest section by half again, and the whole page is
    /// as tall as its tallest section — so a single column made every other section a pane of empty panel.
    /// The name is what gives way, since it is cut with an ellipsis and the reading is not.
    /// </summary>
    static int GroundColumns(float paneWidthPx) => paneWidthPx >= (GroundColumnFloorPx * 2f) + Theme.GapPx ? 2 : 1;

    /// <summary>The narrowest a column of the ground's layers is laid: its tick, a short name and its reading.</summary>
    static float GroundColumnFloorPx =>
        TickRoomPx + GlyphSheet.WidthPx(ShortestGroundNameChars, Theme.TextPx) + Theme.GapPx + GroundReadingRoomPx;

    const int ShortestGroundNameChars = 8;

    static float GroundHeightPx(float paneWidthPx)
    {
        var columns = GroundColumns(paneWidthPx);
        var rows = (GroundParts.Count + columns - 1) / columns;
        return (rows + GroundRows - GroundParts.Count) * LinePitchPx;
    }

    static float SectionHeightPx(int section, float paneWidthPx)
    {
        var laid = DebugLayers.Sections[section];
        var heightPx = laid.Layers.Length * SwitchPitchPx;
        heightPx += laid.Figures switch
        {
            SectionFigures.Trims => FiguresGapPx + (TrimFigures.Count * TrimPitchPx) + LinePitchPx,
            SectionFigures.Shell => FiguresGapPx + (2 * TrimPitchPx),
            SectionFigures.Ground => FiguresGapPx + GroundHeightPx(paneWidthPx),
            _ => 0f,
        };

        return heightPx - Theme.GapPx;
    }

    /// <summary>The sections, a gap, and the row that turns every layer off.</summary>
    static float SidebarHeightPx => ((Sections + 1) * LinePitchPx) + Theme.GapPx;

    /// <summary>What the debug page comes to: the tallest of its sections, or the list of them where that is taller.</summary>
    static float DebugHeightPx(float paneWidthPx)
    {
        var tallestPx = SidebarHeightPx;
        for (var section = 0; section < Sections; section++)
        {
            tallestPx = MathF.Max(tallestPx, SectionHeightPx(section, paneWidthPx));
        }

        return tallestPx;
    }

    static float KeyWidthPx(ReadOnlySpan<LegendKey> keys)
    {
        var widthPx = 0f;
        foreach (var key in keys)
        {
            widthPx += SwatchPx.X + (Theme.GapPx * 0.5f) + GlyphSheet.WidthPx(key.Word.Length, Theme.SmallTextPx);
        }

        return widthPx + (Math.Max(0, keys.Length - 1) * Theme.GapPx * 2f);
    }

    /// <summary>
    /// Every rectangle of the debug page, for the section showing. <b>Nothing is laid on another page, or for
    /// a section not showing</b>: such a row is no rectangle at all, so it can neither be drawn nor clicked.
    /// </summary>
    void LayDebug(float contentX, float topY, float contentWidthPx, float contentHeightPx)
    {
        Array.Clear(_sections);
        Array.Clear(_layerRows);
        Array.Clear(_trims);
        Array.Clear(_grounds);
        _allOff = default;
        _paneRule = default;
        _figuresRule = default;
        if (Page != Debug || AtTheStart) return;

        var sidebarPx = SidebarWidthPx(contentWidthPx);
        for (var section = 0; section < Sections; section++)
        {
            _sections[section] = new Rect(
                new Vector2(contentX, topY + (section * LinePitchPx)), new Vector2(sidebarPx, Theme.RowPx));
        }

        // At the foot of the column rather than under the last section, so it stands apart from the rows that
        // pick what the pane shows: this one changes the town rather than the panel.
        _allOff = new Rect(
            new Vector2(contentX, topY + contentHeightPx - Theme.RowPx), new Vector2(sidebarPx, Theme.RowPx));

        _paneRule = new Rect(
            new Vector2(contentX + sidebarPx + (PaneGapPx * 0.5f), topY), new Vector2(Theme.EdgePx, contentHeightPx));

        var paneX = contentX + sidebarPx + PaneGapPx;
        var paneWidthPx = PaneWidthPx(contentWidthPx);
        var laid = DebugLayers.Sections[Section];
        var y = topY;
        for (var row = 0; row < laid.Layers.Length; row++)
        {
            _layerRows[row] = new Rect(new Vector2(paneX, y), new Vector2(paneWidthPx, Theme.TallRowPx));
            y += SwitchPitchPx;
        }

        if (laid.Figures == SectionFigures.None) return;

        _figuresRule = new Rect(
            new Vector2(paneX, y - Theme.GapPx + (FiguresGapPx * 0.5f)), new Vector2(paneWidthPx, Theme.EdgePx));
        y += FiguresGapPx - Theme.GapPx;

        switch (laid.Figures)
        {
            case SectionFigures.Trims:
                for (var trim = 0; trim < TrimFigures.Count; trim++)
                {
                    _trims[trim] = new Rect(new Vector2(paneX, y), new Vector2(paneWidthPx, Theme.TallRowPx));
                    y += TrimPitchPx;
                }

                _trims[ResetRow] = new Rect(new Vector2(paneX, y), new Vector2(paneWidthPx, Theme.RowPx));
                break;

            case SectionFigures.Shell:
                _trims[ShellRow] = new Rect(new Vector2(paneX, y), new Vector2(paneWidthPx, Theme.TallRowPx));
                _trims[RoundingRow] = new Rect(
                    new Vector2(paneX, y + TrimPitchPx), new Vector2(paneWidthPx, Theme.TallRowPx));
                break;

            case SectionFigures.Ground:
                LayGround(paneX, y, paneWidthPx);
                break;
        }
    }

    /// <summary>The ground's own layers in as many columns as the pane holds, and the rows under them across the whole pane.</summary>
    void LayGround(float paneX, float topY, float paneWidthPx)
    {
        var columns = GroundColumns(paneWidthPx);
        var columnPx = (paneWidthPx - (Theme.GapPx * (columns - 1))) / columns;
        var rows = (GroundParts.Count + columns - 1) / columns;
        for (var part = 0; part < GroundParts.Count; part++)
        {
            var column = part / rows;
            _grounds[part] = new Rect(
                new Vector2(paneX + (column * (columnPx + Theme.GapPx)), topY + ((part % rows) * LinePitchPx)),
                new Vector2(columnPx, Theme.RowPx));
        }

        var y = topY + (rows * LinePitchPx);
        for (var row = GroundAllRow; row < GroundRows; row++)
        {
            _grounds[row] = new Rect(new Vector2(paneX, y), new Vector2(paneWidthPx, Theme.RowPx));
            y += LinePitchPx;
        }
    }

    MenuChoice ClickedDebug(Vector2 pointPx, DebugSwitches switches, TrimFigures trims)
    {
        for (var section = 0; section < Sections; section++)
        {
            if (!_sections[section].Contains(pointPx)) continue;

            OpenSection(section);
            Lay(_laidFor, _laidAt);
            return MenuChoice.None;
        }

        if (_allOff.Contains(pointPx))
        {
            switches.AllOff();
            return MenuChoice.None;
        }

        var layers = DebugLayers.Sections[Section].Layers;
        for (var row = 0; row < layers.Length; row++)
        {
            if (!_layerRows[row].Contains(pointPx)) continue;

            switches.Toggle(layers[row]);
            return MenuChoice.None;
        }

        return DebugLayers.Sections[Section].Figures switch
        {
            SectionFigures.Ground => ClickedGroundRow(pointPx, switches.Ground),
            SectionFigures.None => MenuChoice.None,
            _ => ClickedTrim(pointPx, switches, trims),
        };
    }

    void DrawDebug(
        ref ScreenDraw draw, Vector2 pointerPx, DebugSwitches switches, TrimFigures trims, GroundMesh? mesh)
    {
        for (var section = 0; section < Sections; section++) SectionRow(ref draw, pointerPx, switches, trims, section);

        Theme.Button(
            ref draw, _allOff, pointerPx, AllOffLabel, switches.AnyOn ? Theme.Accent : Theme.RowRest);

        draw.Rect(_paneRule.AtPx, _paneRule.SizePx, Theme.Rule);

        var laid = DebugLayers.Sections[Section];
        for (var row = 0; row < laid.Layers.Length; row++)
        {
            SwitchRow(ref draw, _layerRows[row], pointerPx, DebugLayers.Of(laid.Layers[row]), switches[laid.Layers[row]]);
        }

        if (laid.Figures == SectionFigures.None) return;

        draw.Rect(_figuresRule.AtPx, _figuresRule.SizePx, Theme.Rule);
        if (laid.Figures == SectionFigures.Ground) DrawGround(ref draw, pointerPx, switches.Ground, mesh);
        else DrawTrims(ref draw, pointerPx, switches, trims);
    }

    /// <summary>
    /// One section's row down the side: its name, and how many of its layers are on. <b>A star says one of
    /// its figures is not where it started</b> — a trim moved or a layer of the ground hidden — since that
    /// changes the town under every other section as well.
    /// </summary>
    void SectionRow(ref ScreenDraw draw, Vector2 pointerPx, DebugSwitches switches, TrimFigures trims, int section)
    {
        var box = _sections[section];
        var picked = section == Section;
        Theme.Face(ref draw, box, pointerPx, picked ? Theme.RowPicked : Theme.RowRest, picked);

        var laid = DebugLayers.Sections[section];
        var moved = laid.Figures switch
        {
            SectionFigures.Trims => !trims.Untouched,
            SectionFigures.Ground => !switches.Ground.Whole,
            _ => false,
        };

        Span<char> text = stackalloc char[8];
        var badge = new TextBuffer(text);
        var on = DebugLayers.CountOn(switches, section);
        if (on > 0)
        {
            badge.Add(on);
            badge.Add(" on");
        }

        if (moved) badge.Add('*');

        var badgePx = GlyphSheet.WidthPx(badge.Length, Theme.SmallTextPx);
        var middleY = box.AtPx.Y + (box.SizePx.Y * 0.5f);
        draw.Text(
            new Vector2(box.Right - Theme.InsetPx - badgePx, middleY - (Theme.SmallTextPx * 0.5f)), badge.Written,
            Theme.SmallTextPx, on > 0 ? Theme.Accent : Theme.Heading);

        draw.TextFitted(
            new Vector2(box.AtPx.X + Theme.InsetPx, middleY - (Theme.TextPx * 0.5f)), laid.Name, Theme.TextPx,
            picked || on > 0 ? Theme.Text : Theme.Dim, Theme.FitWidthPx(box) - badgePx - Theme.GapPx);
    }

    /// <summary>
    /// One switch: its tick and its name, its key along the same line, and what it draws on the line under
    /// them. <b>The key gives way before the name does</b>, and the name before the tick.
    /// </summary>
    static void SwitchRow(ref ScreenDraw draw, Rect box, Vector2 pointerPx, in LayerEntry entry, bool on)
    {
        if (box.Contains(pointerPx)) draw.RoundedRect(box.AtPx, box.SizePx, Theme.RowRadiusPx, Theme.RowHover);

        var firstLineY = box.AtPx.Y + ((box.SizePx.Y - (Theme.TextPx + (Theme.GapPx * 0.5f) + Theme.SmallTextPx)) * 0.5f);
        var tick = new Rect(
            new Vector2(box.AtPx.X + Theme.InsetPx, firstLineY + ((Theme.TextPx - TickPx) * 0.5f)), new Vector2(TickPx));
        draw.Outline(tick.AtPx, tick.SizePx, Theme.EdgePx, on ? Theme.Accent : Theme.PanelEdge);
        if (on) draw.Rect(tick.Inset(3f).AtPx, tick.Inset(3f).SizePx, Theme.Accent);

        var textX = box.AtPx.X + TickRoomPx;
        var roomPx = box.Right - Theme.InsetPx - textX;
        var namePx = draw.TextFitted(
            new Vector2(textX, firstLineY), entry.Name, Theme.TextPx, on ? Theme.Text : Theme.Dim, roomPx);

        var keyPx = KeyWidthPx(entry.Key);
        if (namePx + (Theme.GapPx * 2f) + keyPx <= roomPx) Key(ref draw, box.Right - Theme.InsetPx - keyPx, firstLineY, entry.Key);

        draw.TextFitted(
            new Vector2(textX, firstLineY + Theme.TextPx + (Theme.GapPx * 0.5f)), entry.Says, Theme.SmallTextPx,
            Theme.Dim, roomPx);
    }

    /// <summary>A layer's colours, each as a bar of it and the word for what it marks.</summary>
    static void Key(ref ScreenDraw draw, float atX, float lineY, ReadOnlySpan<LegendKey> keys)
    {
        var textY = lineY + ((Theme.TextPx - Theme.SmallTextPx) * 0.5f);
        var swatchY = lineY + ((Theme.TextPx - SwatchPx.Y) * 0.5f);
        foreach (var key in keys)
        {
            draw.RoundedRect(new Vector2(atX, swatchY), SwatchPx, SwatchPx.Y * 0.5f, key.Colour with { W = 1f });
            atX += SwatchPx.X + (Theme.GapPx * 0.5f);
            atX += draw.Text(new Vector2(atX, textY), key.Word, Theme.SmallTextPx, Theme.Dim);
            atX += Theme.GapPx * 2f;
        }
    }
}
