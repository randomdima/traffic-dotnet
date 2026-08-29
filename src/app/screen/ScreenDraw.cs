using System.Numerics;

namespace TrafficSimulation.App.Screen;

/// <summary>
/// The one thing that writes the third pipeline's buffer. Panels, text, tapes, rings and every debug
/// line go through it, and it writes straight into mapped memory the driver already owns.
/// </summary>
/// <remarks>
/// <para>
/// <b>It allocates nothing and it builds no strings.</b> Text is taken as a span of characters, so a
/// caller with a number to print formats it into a <c>stackalloc</c> buffer
/// (<see cref="TextBuffer"/>) rather than into a <c>string</c> per frame — which is rule 2 applied to
/// the frame rather than only to the tick.
/// </para>
/// <para>
/// <b>A full buffer stops accepting quads rather than growing.</b> The buffer is laid at startup for
/// the busiest frame the interface can produce; running out is a bug in the laying, and at sixty
/// hertz the useful thing to do with it is drop the overflow and let the picture say so.
/// </para>
/// </remarks>
internal ref struct ScreenDraw(Span<OverlayQuad> into)
{
    readonly Span<OverlayQuad> _into = into;

    /// <summary>How many quads have been written, which is what the indirect draw is told.</summary>
    public int Written { get; private set; }

    public readonly bool Full => Written >= _into.Length;

    /// <summary>A filled rectangle in interface pixels, given by its top-left corner and its size.</summary>
    public void Rect(Vector2 atPx, Vector2 sizePx, Vector4 colour) =>
        Solid(atPx + sizePx * 0.5f, sizePx * 0.5f, 0f, colour, screen: true);

    /// <summary>
    /// The same rectangle with its corners taken off: three bars and four quarters of the sheet's own
    /// disc, which is the only curve this pipeline has.
    /// </summary>
    public void RoundedRect(Vector2 atPx, Vector2 sizePx, float radiusPx, Vector4 colour)
    {
        var radius = MathF.Min(radiusPx, MathF.Min(sizePx.X, sizePx.Y) * 0.5f);
        if (radius <= 0f)
        {
            Rect(atPx, sizePx, colour);
            return;
        }

        Rect(atPx + new Vector2(0f, radius), new Vector2(sizePx.X, sizePx.Y - radius * 2f), colour);
        Rect(atPx + new Vector2(radius, 0f), new Vector2(sizePx.X - radius * 2f, radius), colour);
        Rect(atPx + new Vector2(radius, sizePx.Y - radius), new Vector2(sizePx.X - radius * 2f, radius), colour);

        Corner(atPx, radius, 0, 0, colour);
        Corner(atPx + new Vector2(sizePx.X - radius, 0f), radius, 1, 0, colour);
        Corner(atPx + new Vector2(0f, sizePx.Y - radius), radius, 0, 1, colour);
        Corner(atPx + new Vector2(sizePx.X - radius, sizePx.Y - radius), radius, 1, 1, colour);
    }

    void Corner(Vector2 atPx, float radiusPx, int right, int down, Vector4 colour) =>
        Write(new OverlayQuad(
            atPx + new Vector2(radiusPx * 0.5f), new Vector2(radiusPx * 0.5f), GlyphSheet.DiscQuarterUv(right, down),
            GlyphSheet.DiscQuarterUvSize, colour, 0f, 1u));

    /// <summary>A rectangle's outline, drawn as four bars inside the rectangle it outlines.</summary>
    public void Outline(Vector2 atPx, Vector2 sizePx, float widthPx, Vector4 colour)
    {
        Rect(atPx, new Vector2(sizePx.X, widthPx), colour);
        Rect(atPx + new Vector2(0f, sizePx.Y - widthPx), new Vector2(sizePx.X, widthPx), colour);
        Rect(atPx + new Vector2(0f, widthPx), new Vector2(widthPx, sizePx.Y - widthPx * 2f), colour);
        Rect(atPx + new Vector2(sizePx.X - widthPx, widthPx), new Vector2(widthPx, sizePx.Y - widthPx * 2f), colour);
    }

    /// <summary>
    /// One line of text, its top-left at <paramref name="atPx"/>, and how wide it came out — so a
    /// caller laying a column can put the next thing after it without measuring twice.
    /// </summary>
    public float Text(Vector2 atPx, scoped ReadOnlySpan<char> text, float heightPx, Vector4 colour)
    {
        var advance = GlyphSheet.AdvancePx(heightPx);
        var size = new Vector2(advance, heightPx);
        for (var at = 0; at < text.Length; at++)
        {
            // Space is a cell of nothing, and a quad of nothing is a quad the buffer did not need.
            if (text[at] == ' ') continue;

            Write(new OverlayQuad(
                atPx + new Vector2((at + 0.5f) * advance, heightPx * 0.5f), size * 0.5f,
                GlyphSheet.UvOf(text[at]), GlyphSheet.CellUv, colour, 0f, 1u));
        }

        return text.Length * advance;
    }

    /// <summary>
    /// The same line, kept inside <paramref name="widthPx"/>: what does not fit is dropped and the
    /// last three glyphs that do are an ellipsis.
    /// </summary>
    /// <remarks>
    /// <b>A label is cut here or it is not cut at all.</b> A panel sized off its own text can still be
    /// narrowed by the window, and a line that ran past the edge read as a panel drawn wrong rather
    /// than as a line with more behind it — the ellipsis is what says which of the two it is.
    /// </remarks>
    public float TextFitted(Vector2 atPx, scoped ReadOnlySpan<char> text, float heightPx, Vector4 colour, float widthPx)
    {
        var advance = GlyphSheet.AdvancePx(heightPx);
        var fits = advance > 0f ? (int)(widthPx / advance) : 0;
        if (fits >= text.Length) return Text(atPx, text, heightPx, colour);

        // Below four glyphs there is no room for the ellipsis either, and three dots where a word was
        // say less than nothing.
        if (fits < 4) return 0f;

        Text(atPx, text[..(fits - 3)], heightPx, colour);
        return Text(atPx + new Vector2((fits - 3) * advance, 0f), "...", heightPx, colour) + (fits - 3) * advance;
    }

    /// <summary>A line in the town's own metres, which is where a debug mark drawn where it happens belongs.</summary>
    public void LineM(Vector2 fromM, Vector2 toM, float widthM, Vector4 colour) =>
        Bar(fromM, toM, widthM, colour, screen: false);

    /// <summary>
    /// <b>One piece of a band down a bending line</b>: the ground between two stations of one arc,
    /// <paramref name="widthM"/> across it, where the line turns <paramref name="turnRad"/> between the
    /// two. Both ends are cut square to the line rather than to the chord, so <b>the next piece is cut on
    /// the same line and the two share an edge</b> — which is the whole difference between a band round a
    /// bend and a row of blocks with a notch on the outside of every joint.
    /// </summary>
    /// <remarks>
    /// A straight is <paramref name="turnRad"/> of zero and comes out as the same quad
    /// <see cref="LineM"/> draws. The slant is capped at the piece's own half length, which only binds
    /// where the band is wider than the bend's own radius — the inner edge folds through the centre of
    /// the turn there, and there is no shape to draw.
    /// </remarks>
    public void BandM(Vector2 fromM, Vector2 toM, float turnRad, float widthM, Vector4 colour)
    {
        var along = toM - fromM;
        var lengthM = along.Length();
        if (lengthM <= 0f) return;

        var halfM = widthM * 0.5f;
        var halfTurnRad = turnRad * 0.5f;
        Write(new OverlayQuad(
            (fromM + toM) * 0.5f, new Vector2(lengthM * 0.5f, halfM * MathF.Cos(halfTurnRad)), GlyphSheet.SolidUv,
            Vector2.Zero, colour, MathF.Atan2(along.Y, along.X), 0u,
            Math.Clamp(halfM * MathF.Sin(halfTurnRad), -lengthM * 0.5f, lengthM * 0.5f)));
    }

    /// <summary>A filled circle in metres: a collision shape, a network node, a marked place on a line.</summary>
    public void DiscM(Vector2 centreM, float radiusM, Vector4 colour) =>
        Write(new OverlayQuad(
            centreM, GlyphSheet.DiscHalfSizeM(radiusM), GlyphSheet.DiscUvMin, GlyphSheet.DiscUvSize, colour, 0f, 0u));

    /// <summary>A circle's outline in metres, as the segments a physics circle is not — but reads as.</summary>
    public void RingM(Vector2 centreM, float radiusM, float widthM, Vector4 colour, int segments = 12)
    {
        var step = MathF.Tau / segments;
        var previous = centreM + new Vector2(radiusM, 0f);
        for (var segment = 1; segment <= segments; segment++)
        {
            var next = centreM + new Vector2(MathF.Cos(segment * step), MathF.Sin(segment * step)) * radiusM;
            LineM(previous, next, widthM, colour);
            previous = next;
        }
    }

    /// <summary>
    /// A rotated box's outline with its corners rounded off — the shape the solver holds for a car
    /// (CAR-12b). <paramref name="sizeM"/> is what the shape reaches, rounding included, so a radius of
    /// zero draws the same outline <see cref="BoxM"/> does.
    /// </summary>
    public void RoundedBoxM(
        Vector2 centreM, Vector2 sizeM, float headingRad, float cornerRadiusM, float widthM, Vector4 colour,
        int segmentsPerCorner = 3)
    {
        if (cornerRadiusM <= 0f)
        {
            BoxM(centreM, sizeM, headingRad, widthM, colour);
            return;
        }

        var forward = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
        var left = new Vector2(-forward.Y, forward.X);
        var core = (sizeM * 0.5f) - new Vector2(cornerRadiusM);

        // Four quarter turns, one about each corner of the core the radius is rolled around, walked in
        // order. Consecutive arcs end and start on the same flat, so joining every point to the last one
        // draws the flats as well without their being a case of their own.
        var step = MathF.PI * 0.5f / segmentsPerCorner;
        var first = Vector2.Zero;
        var previous = Vector2.Zero;
        for (var corner = 0; corner < 4; corner++)
        {
            var alongSign = corner is 0 or 3 ? 1f : -1f;
            var acrossSign = corner is 0 or 1 ? 1f : -1f;
            var pivot = centreM + (forward * (core.X * alongSign)) + (left * (core.Y * acrossSign));

            for (var segment = 0; segment <= segmentsPerCorner; segment++)
            {
                var at = headingRad + (corner * MathF.PI * 0.5f) + (segment * step);
                var next = pivot + (new Vector2(MathF.Cos(at), MathF.Sin(at)) * cornerRadiusM);
                if (corner == 0 && segment == 0) first = next;
                else LineM(previous, next, widthM, colour);

                previous = next;
            }
        }

        LineM(previous, first, widthM, colour);
    }

    /// <summary>A rotated box's outline in metres — the shape the solver holds for a building's part.</summary>
    public void BoxM(Vector2 centreM, Vector2 sizeM, float headingRad, float widthM, Vector4 colour)
    {
        var along = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad)) * sizeM.X * 0.5f;
        var across = new Vector2(-MathF.Sin(headingRad), MathF.Cos(headingRad)) * sizeM.Y * 0.5f;

        var a = centreM - along - across;
        var b = centreM + along - across;
        var c = centreM + along + across;
        var d = centreM - along + across;
        LineM(a, b, widthM, colour);
        LineM(b, c, widthM, colour);
        LineM(c, d, widthM, colour);
        LineM(d, a, widthM, colour);
    }

    /// <summary>
    /// A chevron on a line, pointing the way the line runs. Two strokes, because that is what says
    /// <em>direction</em> without writing it into a label — which is what OBS-2d asks of the layers.
    /// </summary>
    public void ChevronM(Vector2 atM, Vector2 direction, float sizeM, float widthM, Vector4 colour)
    {
        if (direction.LengthSquared() <= 0f) return;

        var along = Vector2.Normalize(direction) * sizeM;
        var across = new Vector2(-along.Y, along.X) * 0.75f;
        LineM(atM - along + across, atM, widthM, colour);
        LineM(atM - along - across, atM, widthM, colour);
    }

    /// <summary>
    /// A bar square across a line, where a chevron would be a claim the line cannot make: the ground under
    /// it is travelled both ways. One stroke, and it says where the mark falls without saying which way
    /// anything goes.
    /// </summary>
    public void TickM(Vector2 atM, Vector2 direction, float sizeM, float widthM, Vector4 colour)
    {
        if (direction.LengthSquared() <= 0f) return;

        var along = Vector2.Normalize(direction) * sizeM;
        var across = new Vector2(-along.Y, along.X) * 0.75f;
        LineM(atM - across, atM + across, widthM, colour);
    }

    void Bar(Vector2 from, Vector2 to, float width, Vector4 colour, bool screen)
    {
        var along = to - from;
        var length = along.Length();
        if (length <= 0f) return;

        Solid((from + to) * 0.5f, new Vector2(length * 0.5f, width * 0.5f), MathF.Atan2(along.Y, along.X), colour, screen);
    }

    void Solid(Vector2 centre, Vector2 halfSize, float rotation, Vector4 colour, bool screen) =>
        Write(new OverlayQuad(centre, halfSize, GlyphSheet.SolidUv, Vector2.Zero, colour, rotation, screen ? 1u : 0u));

    /// <summary>
    /// Quads somebody else already laid, copied in whole. What the debug overlay's cache of the
    /// town's own graphs is spent through: they do not move once the town is laid, so re-emitting
    /// them is a copy of memory rather than a walk of the town.
    /// </summary>
    public void Take(ReadOnlySpan<OverlayQuad> quads)
    {
        var room = Math.Min(quads.Length, _into.Length - Written);
        if (room <= 0) return;

        quads[..room].CopyTo(_into[Written..]);
        Written += room;
    }

    void Write(in OverlayQuad quad)
    {
        if (Written >= _into.Length) return;

        _into[Written++] = quad;
    }
}
