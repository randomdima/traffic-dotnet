using System.Numerics;

namespace TrafficSimulation.App.Screen;

/// <summary>
/// <b>A small framed card of lines</b> — a title over a few rows — built into a caller's own
/// <c>stackalloc</c> buffers and drawn wherever the caller puts it: beside the pointer, or docked in a corner.
/// </summary>
/// <remarks>
/// <b>The first line kept is the title</b>, drawn a size up in the heading colour; every line after it is a
/// row. A line past the budget is dropped rather than thrown, exactly as a character past the end of its
/// buffer is (<see cref="TextBuffer"/>).
/// </remarks>
internal ref struct InfoCard(Span<char> into, Span<int> ends)
{
    readonly Span<char> _into = into;
    readonly Span<int> _ends = ends;
    int _written;

    public int Count { get; private set; }

    /// <summary>A line under construction. It is kept only once <see cref="Keep"/> has it.</summary>
    public readonly TextBuffer Next() => new(_into[_written..]);

    public void Keep(in TextBuffer line)
    {
        if (Count == _ends.Length) return;

        _written += line.Length;
        _ends[Count] = _written;
        Count++;
    }

    public readonly ReadOnlySpan<char> Line(int line) => _into[(line == 0 ? 0 : _ends[line - 1]).._ends[line]];

    const float TitlePx = Theme.TextPx;
    const float RowTextPx = Theme.SmallTextPx;
    const float RowPitchPx = RowTextPx + 4f;
    const float PaddingPx = Theme.GapPx + 2f;

    /// <summary>What the card comes to, with room for a tag written at the end of its title line.</summary>
    public readonly Vector2 SizePx(int tagLength = 0)
    {
        if (Count == 0) return Vector2.Zero;

        var widestPx = GlyphSheet.WidthPx(Line(0).Length, TitlePx)
                       + (tagLength > 0 ? Theme.GapPx * 2f + GlyphSheet.WidthPx(tagLength, RowTextPx) : 0f);
        for (var line = 1; line < Count; line++)
        {
            widestPx = MathF.Max(widestPx, GlyphSheet.WidthPx(Line(line).Length, RowTextPx));
        }

        return new Vector2(
            widestPx + (PaddingPx * 2f), (PaddingPx * 2f) + TitlePx + ((Count - 1) * RowPitchPx) + (Count > 1 ? 4f : 0f));
    }

    /// <param name="tag">
    /// A word written at the trailing end of the title in the accent — what state the card is in, such as
    /// that it is pinned — rather than a row of its own.
    /// </param>
    public readonly void Draw(ref ScreenDraw draw, Vector2 atPx, scoped ReadOnlySpan<char> tag = default)
    {
        if (Count == 0) return;

        var sizePx = SizePx(tag.Length);
        Theme.Frame(ref draw, new Rect(atPx, sizePx));

        var y = atPx.Y + PaddingPx;
        draw.Text(new Vector2(atPx.X + PaddingPx, y), Line(0), TitlePx, Theme.Heading);
        if (!tag.IsEmpty)
        {
            draw.Text(
                new Vector2(
                    atPx.X + sizePx.X - PaddingPx - GlyphSheet.WidthPx(tag.Length, RowTextPx),
                    y + ((TitlePx - RowTextPx) * 0.5f)),
                tag, RowTextPx, Theme.Accent);
        }

        y += TitlePx + 4f;
        for (var line = 1; line < Count; line++)
        {
            draw.Text(new Vector2(atPx.X + PaddingPx, y + 2f), Line(line), RowTextPx, Theme.Text);
            y += RowPitchPx;
        }
    }

    /// <summary>
    /// Where a card of this size stands beside the pointer: below and to the right of it, and turned back to
    /// the other side of it where that would run off the window.
    /// </summary>
    public static Vector2 Beside(Vector2 pointerPx, Vector2 sizePx, Vector2 uiPx)
    {
        var atPx = pointerPx + OffsetPx;
        if (atPx.X + sizePx.X > uiPx.X - Theme.MarginPx) atPx.X = pointerPx.X - OffsetPx.X - sizePx.X;
        if (atPx.Y + sizePx.Y > uiPx.Y - Theme.MarginPx) atPx.Y = pointerPx.Y - OffsetPx.Y - sizePx.Y;

        return Vector2.Max(atPx, new Vector2(Theme.MarginPx));
    }

    /// <summary>Clear of the cursor's own arrow, which covers the first dozen pixels down and to the right of the point.</summary>
    static readonly Vector2 OffsetPx = new(18f, 20f);
}
