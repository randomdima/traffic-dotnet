using System.Numerics;

namespace TrafficSimulation.App.Screen;

/// <summary>
/// Where each character sits on the sheet, and where the one solid cell is. The face itself is
/// <c>src/runtime/glyphs/glyphs.png</c>, cut by the workshop tool beside it.
/// </summary>
/// <remarks>
/// <b>This is the only place the sheet's shape is written down.</b> Re-cutting the sheet at another
/// cell size is a change here and nowhere else — everything that draws text asks for a height in
/// pixels and is given an advance back.
/// </remarks>
internal static class GlyphSheet
{
    public const string Resource = "Glyphs/glyphs.png";

    public const int Columns = 16;
    public const int Rows = 7;
    public const int CellWidthPx = 16;
    public const int CellHeightPx = 24;
    public const int FirstChar = 32;
    public const int LastChar = 127;

    /// <summary>
    /// The first cell of the row after the printable range: opaque everywhere, which is what a plain
    /// rectangle is drawn with.
    /// </summary>
    /// <remarks>
    /// <b>It is the start of the next row and not the next cell.</b> The printable range ends part
    /// way along a row, so the cell straight after the last glyph is an empty one — and a rectangle
    /// drawn with that samples nothing and is invisible, which is a bug that looks like a renderer
    /// dropping every panel while still drawing the text on them.
    /// </remarks>
    const int SolidCell = (LastChar - FirstChar + Columns - 1) / Columns * Columns;

    /// <summary>The cell after that: a filled circle, so a collision shape or a network node is one quad and not a fan.</summary>
    const int DiscCell = SolidCell + 1;

    /// <summary>What one glyph occupies of the sheet, as a fraction — the same for every cell, since the face is fixed-pitch.</summary>
    public static Vector2 CellUv => new(1f / Columns, 1f / Rows);

    /// <summary>How wide a glyph is drawn at a given height, so a line of text can be measured before it is laid.</summary>
    public static float AdvancePx(float heightPx) => heightPx * CellWidthPx / CellHeightPx;

    public static float WidthPx(int characters, float heightPx) => characters * AdvancePx(heightPx);

    /// <summary>
    /// The middle of the solid cell rather than its corner: the sampler filters linearly and clamps,
    /// so a rectangle drawn off the middle of a cell that is opaque throughout cannot pick up the
    /// transparent one beside it however the quad is scaled.
    /// </summary>
    public static Vector2 SolidUv => CellCentreUv(SolidCell);

    /// <summary>
    /// The disc's cell, <b>inset by half a texel</b>. The cell before it is opaque throughout, and a
    /// quad whose uv started on the boundary would filter half of that white into the disc's rim —
    /// which reads as a shape with a bright bite out of one side.
    /// </summary>
    public static Vector2 DiscUvMin => new(
        (DiscCell % Columns * CellWidthPx + 0.5f) / (Columns * CellWidthPx),
        (DiscCell / Columns * CellHeightPx + 0.5f) / (Rows * CellHeightPx));

    public static Vector2 DiscUvSize => new(
        (CellWidthPx - 1f) / (Columns * CellWidthPx), (CellHeightPx - 1f) / (Rows * CellHeightPx));

    /// <summary>
    /// The half-extents of the quad that draws a disc of this radius <b>round</b>.
    /// </summary>
    /// <remarks>
    /// The disc is cut into the sheet inscribed in the cell's <em>short</em> side and the cell is not
    /// square, so a quad the size of the disc stretches it to half again as wide as it is tall — which
    /// drew every network node and every collision circle in the town as an ellipse. Any quad with the
    /// cell's own aspect draws it round; this one is scaled so that the circle inside comes out at the
    /// radius the caller asked for.
    /// </remarks>
    public static Vector2 DiscHalfSizeM(float radiusM) =>
        new Vector2(CellWidthPx, CellHeightPx) * (radiusM / DiscDiameterPx);

    /// <summary>
    /// How much of the cell the disc actually covers: the short side, less the antialiasing margin the
    /// sheet tool leaves either side of it so a disc drawn large does not read as a polygon.
    /// </summary>
    const float DiscDiameterPx = 14f;

    /// <summary>
    /// One quarter of the disc, which is what a rounded corner is drawn with — <paramref name="right"/>
    /// and <paramref name="down"/> are 0 or 1 and say which quarter.
    /// </summary>
    /// <remarks>
    /// <b>A rounded rectangle is three bars and four of these</b>, and it is four *quarters* rather
    /// than four whole discs because the fills here are drawn one over another: a disc lapping into the
    /// bar beside it would blend twice and leave a brighter arc inside every corner.
    /// </remarks>
    public static Vector2 DiscQuarterUv(int right, int down) => new(
        (DiscCell % Columns * CellWidthPx + DiscMarginPx.X + right * DiscDiameterPx * 0.5f) / (Columns * CellWidthPx),
        (DiscCell / Columns * CellHeightPx + DiscMarginPx.Y + down * DiscDiameterPx * 0.5f) / (Rows * CellHeightPx));

    public static Vector2 DiscQuarterUvSize =>
        new(DiscDiameterPx * 0.5f / (Columns * CellWidthPx), DiscDiameterPx * 0.5f / (Rows * CellHeightPx));

    /// <summary>Where the disc starts inside its cell: it is centred in one that is neither square nor its own size.</summary>
    static Vector2 DiscMarginPx =>
        new((CellWidthPx - DiscDiameterPx) * 0.5f, (CellHeightPx - DiscDiameterPx) * 0.5f);

    /// <summary>Where a character's cell starts, or the space cell for anything the sheet does not carry.</summary>
    public static Vector2 UvOf(char character)
    {
        var cell = character >= FirstChar && character < LastChar ? character - FirstChar : 0;
        return new Vector2(cell % Columns * CellUv.X, cell / Columns * CellUv.Y);
    }

    static Vector2 CellCentreUv(int cell) =>
        new((cell % Columns + 0.5f) * CellUv.X, (cell / Columns + 0.5f) * CellUv.Y);
}
