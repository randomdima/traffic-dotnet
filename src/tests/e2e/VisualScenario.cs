using System.Numerics;
using TrafficSimulation.App.Shot;

namespace TrafficSimulation.Tests.E2E;

/// <summary>
/// One staged scenario of the end-to-end visual tier (VER-10): a place to point the camera, a moment
/// to point it at, and the claims the frame is judged against.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Expect"/> is the whole point of the type.</b> Each line is a single claim about the
/// picture that a reviewer answers yes or no to, written to be falsifiable <i>by looking</i> — "the
/// dashes are evenly pitched", never "the road looks right". This tier exists for exactly the claims
/// no threshold can carry; one that can be stated as a pixel at a coordinate belongs in
/// <c>src/tests/render/</c>, where it runs unattended.
/// </para>
/// <para>
/// <b>Every scenario is anchored on a named place of a shipped map</b>, never on a search of the
/// town. A locator that goes looking — "the longest lane", "the densest cluster of cars" — asks a
/// different question every time the map is edited and answers the wrong one silently.
/// </para>
/// </remarks>
/// <param name="Name">The scenario's name, which is the frame's file name and the test case's.</param>
/// <param name="Group">Which set it belongs to: <c>core</c> is the fixture map's own detail, taken on
/// every change to anything that draws; <c>wider</c> is what the fixture cannot answer — a whole
/// city, the skewed crossing, the debug layers and the interface.</param>
/// <param name="Map">The shipped town it is staged on.</param>
/// <param name="FrameWidthM">How much ground the frame's width covers.</param>
/// <param name="FinestFeatureM">The smallest thing the claims mention — a road marking is 0.15 m, a
/// lamp 0.12 m, a texture's grain 0.4 m, a street 7 m. With <paramref name="FrameWidthM"/> it is what
/// <see cref="SizeFor"/> derives the resolution from, so a claim about a fine thing gets the pixels
/// to see it and a claim about a coarse one does not pay for pixels nobody reads.</param>
/// <param name="AtM">Where the camera is pinned, or null for the map's own centre.</param>
/// <param name="Seconds">How far into a seeded run the frame is taken. The town is deterministic, so
/// this is a moment and not a race.</param>
/// <param name="Ui">The <c>--ui</c> words: <c>none</c> is a bare frame of the town, an empty list is
/// the ordinary interface, and the rest name a layer or a menu page.</param>
/// <param name="Expected">The reference frame in <c>src/tests/e2e/expected/</c>, or null where this
/// scenario has none. They are the godot-dotnet build's own frames of the same scenarios.</param>
/// <param name="Cells">Several frames of the same kind at the same scale, in reading order. The
/// claims are then asked of <i>every</i> frame — use it for variation (the junction kinds), never for
/// a relationship within one scene.</param>
internal sealed record VisualScenario(
    string Name,
    string Group,
    string Map,
    string Subject,
    float FrameWidthM,
    float FinestFeatureM,
    Vector2? AtM,
    double Seconds,
    string[] Ui,
    string[] Expect,
    string? Expected = null,
    string? ExpectedNote = null,
    (string Label, Vector2 AtM)[]? Cells = null,
    Vector2[]? RulerPointsM = null)
{
    /// <summary>
    /// How many pixels the finest feature the claims mention has to be drawn at. Five is what it
    /// takes to see that a line is a line, that it is straight, and roughly how thick — below about
    /// three it is an artefact of sampling and a reviewer cannot say anything honest about it.
    /// </summary>
    const int FeaturePx = 5;

    /// <summary>
    /// The bounds the derived size is clamped into. The floor keeps a tight close-up from coming back
    /// as a thumbnail nobody can judge; the ceiling is where a frame stops paying for itself, because
    /// past it the cost grows faster than what a reviewer can take in — and a vision model is billed
    /// by the pixel.
    /// </summary>
    const int MinSidePx = 512;
    const int MaxSidePx = 1536;

    /// <summary>Frames are 4:3, and the width is what <see cref="FrameWidthM"/> measures.</summary>
    const float Aspect = 3f / 4f;

    /// <summary>The gutter the reference build leaves between the cells of a contact sheet, which is
    /// <see cref="Sheet.GutterPx"/> here. It is in the arithmetic below even though these frames are
    /// not yet tiled, so a cell is the same size as the cell of the sheet it is compared against.</summary>
    const int GutterPx = Sheet.GutterPx;

    /// <summary>The frame's size in pixels, derived from what the claims are about. Rounded to a
    /// multiple of 64 so a frame's size is a round number at any framing.</summary>
    public (int WidthPx, int HeightPx) SizeFor()
    {
        if (FrameWidthM <= 0f) return (1200, 900); // the interface at rest: no ground in the claim

        var wanted = FrameWidthM / MathF.Max(FinestFeatureM, 0.001f) * FeaturePx;
        var width = Math.Clamp((int)MathF.Round(wanted / 64f) * 64, MinSidePx, MaxSidePx);

        // A cell of a several-frame scenario may be no larger than its share of the ceiling: four
        // cells at the full size are four times what one frame is allowed to cost.
        if (Cells is { Length: > 1 })
        {
            var columns = Sheet.Columns(Cells.Length);
            var ceiling = (MaxSidePx - ((columns - 1) * GutterPx)) / columns;
            width = Math.Clamp(Math.Min(width, ceiling) / 64 * 64, 192, MaxSidePx);
        }

        return (width, (int)MathF.Round(width * Aspect / 64f) * 64);
    }

    /// <summary>
    /// The span the camera opens on, across the frame's <b>short</b> side — which is what the shot
    /// path takes, while a scenario is written in the width a reader sees.
    /// </summary>
    public float ViewM
    {
        get
        {
            var (widthPx, heightPx) = SizeFor();
            return FrameWidthM <= 0f ? 0f : FrameWidthM * heightPx / widthPx;
        }
    }

    /// <summary>The ruler this frame is judged with: how much a metre is worth on it.</summary>
    public float PxPerM => FrameWidthM <= 0f ? 0f : SizeFor().WidthPx / FrameWidthM;

    /// <summary>
    /// The one file a review opens. A scenario with cells is photographed several times and tiled into
    /// a single contact sheet, so this is always one name: the cells are inside it, in reading order.
    /// </summary>
    public string Frame => $"{Name}.png";

    /// <summary>
    /// The exposures the camera takes, paired with the file each is written to. For a scenario with
    /// cells these are working files that <see cref="Sheet"/> tiles into <see cref="Frame"/>
    /// and then deletes.
    /// </summary>
    public (string File, Vector2? AtM)[] Exposures() =>
        Cells is null
            ? [(Frame, AtM)]
            : Array.ConvertAll(Cells, cell => ($"{Name}-cell-{cell.Label}.png", (Vector2?)cell.AtM));
}
