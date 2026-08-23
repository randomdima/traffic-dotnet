namespace TrafficSimulation.App.Render;

/// <summary>
/// One picture the sprite pipeline may be told to draw with, and how it is to be sampled.
/// </summary>
/// <remarks>
/// <para>
/// <b>A sheet is a file and clamps</b>, because the texel across a cell boundary belongs to another
/// pose and a mip level would average the two together. The tread is the one exception in the town and
/// it is the opposite of a sheet in both respects: it is a <em>tile</em>, one pitch of tread that a
/// wheel's quad lays several times over and scrolls along, so it must repeat — and it is drawn at a
/// few pixels a side at any framing past a street, so without a mip chain it crawls.
/// </para>
/// <para>
/// <b>A brush has no file.</b> The mark brushes are a gradient a few texels tall rather than a
/// picture, so they are built here rather than shipped: an asset would be a file nobody could read
/// and a step somebody would forget to re-import.
/// </para>
/// </remarks>
internal readonly record struct SheetSource(string? Path, byte[]? Rgba, int WidthPx, int HeightPx, bool Repeats, bool Mipped)
{
    public static SheetSource File(string path, bool repeats = false, bool mipped = false) =>
        new(path, null, 0, 0, repeats, mipped);

    /// <summary>An image with no file behind it, as rows of RGBA bytes.</summary>
    public static SheetSource Generated(byte[] rgba, int widthPx, int heightPx) =>
        new(null, rgba, widthPx, heightPx, Repeats: false, Mipped: false);
}
