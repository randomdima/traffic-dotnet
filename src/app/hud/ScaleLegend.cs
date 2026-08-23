using System.Numerics;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>OBS-2e — how big the town is, is on screen at all times</b>: a graduated bar in the bottom-right
/// corner.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its length is held and its marks answer the zoom.</b> The graduations stand at a round number
/// of metres at whatever the camera is showing, and it is the <em>number</em> of them that changes;
/// the bar varies only in ending on a large mark, its length being a whole number of them. Zooming in
/// therefore leaves a bar about the same length on screen standing for a different distance, which is
/// exactly what the pair of reference frames is taken to show.
/// </para>
/// <para>
/// <b>It is furniture and not instrumentation</b>, so it has no switch and is drawn from the moment a
/// town is standing. And <b>nothing is drawn behind it or behind any figure its marks write</b>: the
/// bar and the text carry their own outline against the town instead of a panel, because a panel in
/// the corner of every frame is a piece of the town nobody can see.
/// </para>
/// </remarks>
internal static class ScaleLegend
{
    /// <summary>About how long the bar is meant to come out. It ends on a large mark, so it lands near this rather than on it.</summary>
    const float TargetPx = 190f;

    /// <summary>How far over the held length the bar may run to land on a mark, as a factor of it.</summary>
    const float MostOverPx = 1.1f;

    const float MarginPx = 18f;
    const float BarHeightPx = 5f;
    const float LargeMarkPx = 13f;
    const float SmallMarkPx = 7f;

    /// <summary>How many small graduations divide each large one.</summary>
    const int SmallPerLarge = 5;

    /// <summary>The daylight two neighbouring figures need before both are written.</summary>
    const float FigureGapPx = 6f;

    /// <summary>
    /// The room a large mark's figure is given, in characters: the widest figure the legend writes
    /// (<c>100 m</c>, the unit being on that one) plus the half of its neighbour it has to clear,
    /// because the last figure is right-aligned to the margin rather than centred on its mark.
    /// </summary>
    const int WidestFigureChars = 7;

    /// <summary>
    /// About how many large marks the bar is aimed at. It is a target and not a rule — the round
    /// number is what is held to, and the count that falls out of it is what the bar carries.
    /// </summary>
    const int WantedMarks = 4;

    public static void Draw(ref ScreenDraw draw, Vector2 uiPx, float pixelsPerMetre)
    {
        if (pixelsPerMetre <= 0f) return;

        // A round number of metres per large mark, picked so that a handful of them fit the held
        // length: the count is what varies with the zoom, and the bar ends on a large mark at each
        // end because its length is a whole number of them.
        var stepM = Ladder.StepM(TargetPx / pixelsPerMetre / WantedMarks);

        // Then up the ladder until a large mark is far enough from the next for both to carry their
        // figures. A legend is read off its figures, so the marks answer to them and not the reverse.
        var spacingPx = GlyphSheet.WidthPx(WidestFigureChars, Theme.SmallTextPx) + FigureGapPx;
        for (var rung = 0; rung < 8 && stepM * pixelsPerMetre < spacingPx; rung++) stepM = Ladder.StepM(stepM * 2.5f);

        // Whichever whole number of marks lands nearest the held length, rather than the largest that
        // fits under it: a step twice as coarse as the last one would otherwise halve the bar.
        var spacingPerMarkPx = stepM * pixelsPerMetre;
        var marks = Math.Clamp((int)MathF.Round(TargetPx / spacingPerMarkPx), 1, 8);
        while (marks > 1 && marks * spacingPerMarkPx > TargetPx * MostOverPx) marks--;

        var barPx = marks * stepM * pixelsPerMetre;

        // Everything is stacked upwards off one baseline, so the whole instrument sits inside the
        // margin rather than hanging off the bottom of the frame.
        var rightPx = uiPx.X - MarginPx;
        var baselineY = uiPx.Y - MarginPx;
        var barTopY = baselineY - BarHeightPx;
        var leftPx = rightPx - barPx;

        // A dark stroke a pixel outside the bright one, on every mark and under every figure: the
        // legend has to read over grass and over tarmac, and it may not have a panel to sit on.
        Bar(ref draw, new Vector2(leftPx, barTopY), barPx, BarHeightPx, Theme.LegendShadow, Theme.Legend);

        for (var mark = 0; mark <= marks * SmallPerLarge; mark++)
        {
            var large = mark % SmallPerLarge == 0;
            var atX = leftPx + barPx * mark / (marks * SmallPerLarge);
            var heightPx = large ? LargeMarkPx : SmallMarkPx;
            Bar(ref draw, new Vector2(atX, barTopY - heightPx), 1.5f, heightPx, Theme.LegendShadow, Theme.Legend);
        }

        // The figure over each large mark, the last of which names the unit — so the bar is read as a
        // distance rather than as a decoration. Written from the right, because the figure that has to
        // survive is the one carrying the unit: the last is also the widest and the only one pulled off
        // its own mark, so a run written left to right is a run whose last two collide.
        Span<char> text = stackalloc char[16];
        var placedLeftPx = float.MaxValue;
        var textTopY = barTopY - LargeMarkPx - Theme.SmallTextPx - 3f;
        for (var mark = marks; mark >= 0; mark--)
        {
            var written = new TextBuffer(text);
            if (mark == marks) Ladder.WriteDistance(ref written, marks * stepM);
            else written.Add(mark * stepM, "F0");

            // Centred over its mark, except that the last figure carries the unit and would hang off
            // the frame: it is pulled back to the margin instead, since the legend has to be inside
            // the corner it is drawn in.
            var atX = leftPx + (barPx * mark / marks);
            var widthPx = GlyphSheet.WidthPx(written.Length, Theme.SmallTextPx);
            var atPx = new Vector2(MathF.Min(atX - (widthPx * 0.5f), rightPx - widthPx), textTopY);

            // A mark whose figure will not fit beside its neighbour's keeps its mark and loses its
            // figure. Two figures run together read as a third number that is not on the bar at all.
            if (atPx.X + widthPx + FigureGapPx > placedLeftPx) continue;

            Outlined(ref draw, atPx, written.Written, Theme.SmallTextPx);
            placedLeftPx = atPx.X;
        }
    }

    static void Bar(ref ScreenDraw draw, Vector2 atPx, float widthPx, float heightPx, Vector4 shadow, Vector4 colour)
    {
        draw.Rect(atPx - Vector2.One, new Vector2(widthPx + 2f, heightPx + 2f), shadow);
        draw.Rect(atPx, new Vector2(widthPx, heightPx), colour);
    }

    /// <summary>
    /// The text drawn four times in the shadow colour and once over it. Cheap, and the only way to
    /// keep a figure legible over ground the legend is not allowed to cover.
    /// </summary>
    static void Outlined(ref ScreenDraw draw, Vector2 atPx, scoped ReadOnlySpan<char> text, float heightPx)
    {
        draw.Text(atPx + new Vector2(-1f, 0f), text, heightPx, Theme.LegendShadow);
        draw.Text(atPx + new Vector2(1f, 0f), text, heightPx, Theme.LegendShadow);
        draw.Text(atPx + new Vector2(0f, -1f), text, heightPx, Theme.LegendShadow);
        draw.Text(atPx + new Vector2(0f, 1f), text, heightPx, Theme.LegendShadow);
        draw.Text(atPx, text, heightPx, Theme.Legend);
    }

    /// <summary>The same outlined text, for the ruler — which has the same problem and must not solve it differently.</summary>
    public static void OutlinedText(ref ScreenDraw draw, Vector2 atPx, scoped ReadOnlySpan<char> text, float heightPx) =>
        Outlined(ref draw, atPx, text, heightPx);
}
