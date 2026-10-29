namespace TrafficSimulation.App.Render;

/// <summary>
/// One picture the sprite pipeline may be told to draw with. Every one is packed into the atlas and
/// sampled clamped and un-mipped, because the texel across a cell boundary belongs to another pose.
/// </summary>
/// <remarks>
/// <b>A brush has no file.</b> The mark brushes are a gradient a few texels tall rather than a
/// picture, so they are built here rather than shipped: an asset would be a file nobody could read
/// and a step somebody would forget to re-import.
/// </remarks>
internal readonly record struct SheetSource(string? Path, byte[]? Rgba, int WidthPx, int HeightPx)
{
    public static SheetSource File(string path) => new(path, null, 0, 0);

    /// <summary>An image with no file behind it, as rows of RGBA bytes.</summary>
    public static SheetSource Generated(byte[] rgba, int widthPx, int heightPx) => new(null, rgba, widthPx, heightPx);

    /// <summary>
    /// Whether what is drawn from this sheet is light, added to what is under it rather than laid over it: its colour
    /// times its alpha is summed onto the picture, which nothing covering it can do — a glow on a white body would
    /// otherwise be white on white. <see cref="SheetPlace.Light"/> carries it to the fragment stage.
    /// </summary>
    public bool AddsLight { get; init; }

    /// <summary>
    /// How wide a dark line is laid round the picture's solid part as it is packed, in its own pixels; nought lays none
    /// (<see cref="SheetOutline"/>). The file is left as it is.
    /// </summary>
    public float OutlineArtPx { get; init; }
}
