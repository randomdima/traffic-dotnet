using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The ground's patches</b>: a field over the town's plane that decides, everywhere, which of a surface's two looks
/// it wears — a kept lawn or a worn one, fresh asphalt or worn — and tints it, paler and warmer where it is worn. The
/// field does not repeat, so neither does where one look gives way to the other, however short each look's own period.
/// Read by the ground's stages (<c>ground_patches.glsl</c>, <c>town.wgsl</c>) from <see cref="CameraView.Patches"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The field is <c>ground_patches.glsl</c>'s</b>: four octaves of value noise, each half as wide as the last.
/// <see cref="FieldMean"/> and <see cref="FieldSpread"/> are that field's own, measured once over 300 000 samples of it,
/// and change with it. The stages read it in standard deviations from its mean, so every figure here is in them.
/// </para>
/// <para>
/// <b>Every surface reads a field of its own</b>: the lattice is hashed with the surface's number, and each is as wide
/// as its own figure. Read off one field, a lawn, the walk beside it and the road beyond all wore at the same places,
/// and a worn patch ran across two kerbs as one blotch.
/// </para>
/// <para>
/// <b>A look's share is a threshold</b>, as a cloud cover is (<see cref="CloudShadows"/>): the field is near enough
/// normal, so the second look is where it stands above the quantile of the first's share, the hand-over
/// <see cref="ViewFigures.GroundLookEdgeSpread"/> wide about it.
/// </para>
/// <para>
/// <b>And no look is drawn as its repeat</b> (<see cref="Scatter"/>): the ground is cut into hexes, each reading the
/// look from a place of its own and blended into its neighbours, so the period a look was joined at is never seen.
/// </para>
/// </remarks>
/// <param name="Field">The field's mean and one over its spread.</param>
/// <param name="PerAcross">One over each surface's patches' width, in metres: grass, tarmac, pavement, deck.</param>
/// <param name="PerAcrossWet">And the water's and the shore's.</param>
/// <param name="Scatter">
/// One over how far apart the hexes' middles are, in metres, and how sharply one hands over to the next — the power
/// each one's share is raised to before the three meeting there are summed back to one.
/// </param>
/// <param name="Ragged">
/// One over how wide the lumps a fade's edge is ragged by are, in metres, and how far they move its cover at its middle
/// (<see cref="ViewFigures.GroundFadeRaggedShare"/>).
/// </param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GroundPatches(
    Vector4 Field, Vector4 Grass, Vector4 Tarmac, Vector4 Pavement, Vector4 Deck, Vector4 Water, Vector4 Shore,
    Vector4 PerAcross, Vector4 PerAcrossWet, Vector4 Scatter, Vector4 Ragged)
{
    /// <summary>The row the shore's figures stand in: after the water's, since the track's strip between them in
    /// <see cref="Surface"/> takes no patches.</summary>
    public const int ShoreRow = 5;

    const float FieldMean = 0.5f;
    const float FieldSpread = 0.139f;

    /// <summary>The patches as the ground's stages read them: the field's mean and one over its spread; then each
    /// surface's light and warmth, and where its second look begins and where it is whole, in the order a ground vertex
    /// files its surface, the shore at <see cref="ShoreRow"/>; then each surface's width, the hexes and the fades.</summary>
    public static GroundPatches From(ViewFigures view) => new(
        new Vector4(FieldMean, 1f / FieldSpread, 0f, 0f),
        Look(view, view.GroundPatchLightGrass, view.GroundPatchWarmGrass, view.GroundLookShareGrass),
        Look(view, view.GroundPatchLightTarmac, view.GroundPatchWarmTarmac, view.GroundLookShareTarmac),
        Look(view, view.GroundPatchLightPavement, view.GroundPatchWarmPavement, view.GroundLookSharePavement),
        Look(view, view.GroundPatchLightDeck, view.GroundPatchWarmDeck, view.GroundLookShareDeck),
        Look(view, view.GroundPatchLightWater, view.GroundPatchWarmWater, view.GroundLookShareWater),
        Look(view, view.GroundPatchLightShore, view.GroundPatchWarmShore, view.GroundLookShareShore),
        new Vector4(
            1f / view.GroundPatchAcrossGrassM, 1f / view.GroundPatchAcrossTarmacM, 1f / view.GroundPatchAcrossPavementM,
            1f / view.GroundPatchAcrossDeckM),
        new Vector4(1f / view.GroundPatchAcrossWaterM, 1f / view.GroundPatchAcrossShoreM, 0f, 0f),
        new Vector4(1f / view.GroundScatterAcrossM, view.GroundScatterSharpness, 0f, 0f),
        new Vector4(1f / view.GroundFadeRaggedAcrossM, view.GroundFadeRaggedShare, 0f, 0f));

    /// <summary>
    /// <b>How far the water's two looks have drifted at <paramref name="tick"/></b>, in metres, the still look's then
    /// the lively one's: each with the prevailing wind at its own share of its speed
    /// (<see cref="ViewFigures.WaterDriftStillWindShare"/>), so the two slide over each other and the river is never the
    /// same picture twice. A paused town's water stands.
    /// </summary>
    /// <remarks>
    /// <b>The prevailing wind and not the wind of each moment</b>, as the clouds are (<see cref="Clouds"/>): a drift is
    /// where the air of every moment since took the ripples, and a gust swings the wind either side of a way the
    /// prevailing one already keeps.
    /// </remarks>
    public static Vector4 DriftAt(Wind wind, SimConfig config, long tick)
    {
        var elapsedS = tick / (double)config.Sim.TickRateHz;
        var (sin, cos) = MathF.SinCos(wind.PrevailingRad);
        var travelledM = new Vector2(cos, sin) * (float)(config.Wind.SpeedMps * elapsedS);
        return new Vector4(
            travelledM * config.View.WaterDriftStillWindShare, travelledM.X * config.View.WaterDriftLivelyWindShare,
            travelledM.Y * config.View.WaterDriftLivelyWindShare);
    }

    static Vector4 Look(ViewFigures view, float light, float warm, float share)
    {
        if (share is < 0f or > 1f)
        {
            throw new ArgumentException($"A second look covers {share} of its ground: a share, between nought and one.");
        }

        var edge = CloudShadows.Probit(1f - share);
        return new Vector4(light, warm, edge - (view.GroundLookEdgeSpread / 2f), edge + (view.GroundLookEdgeSpread / 2f));
    }
}
