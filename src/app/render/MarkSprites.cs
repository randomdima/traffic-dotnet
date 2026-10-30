using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.App.Render;

/// <summary>
/// How what the traffic has written on the ground is drawn — each stroke of knots (<see cref="DriftKnot"/>) as a curved
/// strip through a brush, laid first, so every body in the town passes over its own marks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hard and soft ground are marked in different kinds, because what happens to them is different in
/// kind.</b> Tarmac is stained a darker shade of <em>itself</em> — rubber laid on a surface that is
/// still there — with the crisp edge a rubber patch has. Soft ground is not stained at all: the tyre
/// ploughs the turf off and turns up the earth beneath it, so the mark is soil-coloured rather than a
/// darker green, and its edges are soft because a rut is displaced ground crumbling into what is left
/// of the lawn.
/// </para>
/// <para>
/// <b>Neither kind reads the ground it is laid on, and neither needs to.</b> Darkening a surface by a
/// share of itself and then laying that at an opacity is arithmetically the same as laying black at
/// the product of the two, and mixing a colour into the ground before laying it is the same as laying
/// that colour at the product — so a mark is one blended quad over whatever it happens to cross, and
/// nothing here has to sample the terrain to know what it looks like.
/// </para>
/// </remarks>
internal static class MarkSprites
{
    /// <summary>
    /// Rubber, as the black an alpha blend has to lay to darken the road by the shade a skid darkens
    /// it: half again, at four tenths' opacity. Well under opaque, so a corner taken twice is visibly
    /// darker than a corner taken once and no single pass ever reaches bare black.
    /// </summary>
    const float RubberAlpha = 0.4f * 0.5f;

    /// <summary>What is under the grass. A rut is not a shadow on a lawn, it is the lawn gone.</summary>
    static readonly Vector3 Soil = new(0.34f, 0.20f, 0.10f);

    /// <summary>
    /// Nearly all of a ploughing pass is soil rather than the ground it came from, at an opacity
    /// higher than rubber's because the grass under a rut is not showing through it: it has been
    /// turned over.
    /// </summary>
    const float SoilAlpha = 0.7f * 0.9f;

    /// <summary>
    /// How much wider than the tyre each kind is laid, so that the brush's fade eats into the bleed
    /// rather than into the mark: what reads as a rut's width stays the width of what made it.
    /// </summary>
    const float RubberBleed = 1f;
    const float SoilBleed = 1.3f;

    /// <summary>
    /// What share of each brush's half-width fades out rather than being laid at full strength — the
    /// edge. Rubber has none: a tyre either laid it or it did not, and the edge of a skid is as sharp
    /// as the patch that made it. A rut has no such edge at all, being ground pushed aside.
    /// </summary>
    public const float RubberEdgeShare = 0f;
    public const float SoilEdgeShare = 0.55f;

    /// <summary>Resolution of the brush across the width of a mark. Small: it is a gradient read through a linear sampler, not a picture.</summary>
    const int BrushSamples = 32;

    /// <summary>How each kind is drawn, for the stage that builds a stroke's curve (<c>mark.vert</c>, <c>town.wgsl</c>'s <c>markVertex</c>).</summary>
    public static MarkLook Look(int rubberSheet, int soilSheet) => new(
        new Vector4(Soil, 1f), (uint)rubberSheet, (uint)soilSheet, RubberBleed, SoilBleed, RubberAlpha, SoilAlpha);

    /// <summary>
    /// The brush a stroke is drawn through: white, fading out across the mark's width and uniform along
    /// its length. Across, because that is the edge that shows — a stroke's curves meet end to end, so
    /// fading the ends too would scallop every track into beads.
    /// </summary>
    public static SheetSource Brush(float edgeShare)
    {
        var rgba = new byte[BrushSamples * 4];
        for (var row = 0; row < BrushSamples; row++)
        {
            // Distance out from the middle as a share of the half-width, and how far into the fading
            // edge that is. Smoothed, so the mark meets the ground without a visible lip.
            var acrossHalf = MathF.Abs(((row + 0.5f) / BrushSamples * 2f) - 1f);
            var alpha = edgeShare <= 0f ? 1f : 1f - SmoothStep(1f - edgeShare, 1f, acrossHalf);

            rgba[(row * 4) + 0] = 255;
            rgba[(row * 4) + 1] = 255;
            rgba[(row * 4) + 2] = 255;
            rgba[(row * 4) + 3] = (byte)Math.Clamp(alpha * 255f, 0f, 255f);
        }

        return SheetSource.Generated(rgba, 1, BrushSamples);
    }

    static float SmoothStep(float from, float to, float at)
    {
        var t = Math.Clamp((at - from) / MathF.Max(to - from, 1e-6f), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}

/// <summary>
/// <b>How a mark of each kind is drawn</b> (<see cref="MarkSprites.Look"/>), as <c>mark.vert</c>'s uniform block and the page's lay it:
/// soil's colour, the two brushes' sheets, then each kind's bleed and opacity.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MarkLook(
    Vector4 Soil, uint RubberSheet, uint SoilSheet, float RubberBleed, float SoilBleed, float RubberAlpha, float SoilAlpha,
    Vector2 Pad = default);
