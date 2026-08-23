namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// The single conversion site between metres and pixels. Nothing else may multiply or divide by a
/// pixels-per-metre factor. Simulation state is metres throughout: +y down, headings from +x toward +y.
/// </summary>
internal readonly struct WorldScale(float artPixelsPerMetre)
{
    /// <summary>Fixed for the life of the town: the grid the art was cut on (21 px/m, blown up ×3).</summary>
    public float ArtPixelsPerMetre { get; } = artPixelsPerMetre;

    public float MetresFromArtPixels(float pixels) => pixels / ArtPixelsPerMetre;

    public float ArtPixelsFromMetres(float metres) => metres * ArtPixelsPerMetre;

    /// <summary>
    /// The camera's factor, which moves with the zoom. Interface pixels, not display pixels — a span
    /// in metres is a claim about what is framed, not about how densely it is drawn.
    /// </summary>
    public static float ScreenPixelsPerMetre(float viewSpanM, int uiShortSidePx) => uiShortSidePx / viewSpanM;
}
