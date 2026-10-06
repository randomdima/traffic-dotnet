using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>What a patch leaves behind: how strongly a scrub writes on the ground.</summary>
internal static partial class TyreModel
{
    /// <summary>
    /// How far this tyre has dragged over the ground without letting go of it, capped at the distance
    /// at which it starts writing. Rubber is not laid in an instant: a tyre chirping through a turn-in
    /// scrubs for a few centimetres and leaves the road as it found it, while one that is properly
    /// away drags metre after metre and writes.
    /// </summary>
    /// <remarks>
    /// What is not being scrubbed drains at the slip bar, so a tyre that hooks up again stops marking
    /// at once and a scrub that comes and goes has to keep coming back to count. The cap is what stops
    /// a long slide banking credit and going on marking after it has ended.
    /// </remarks>
    public static float ScrubTravelM(SimConfig config, float carriedM, float slideSpeedMps, float dtS) =>
        Math.Clamp(
            carriedM + ((slideSpeedMps > 0f ? slideSpeedMps : -config.Marks.SlipMps) * dtS),
            0f, config.Marks.OnsetM);

    /// <summary>
    /// How hard a mark a wheel is leaving, 0..1, given the friction power it reported and the
    /// threshold below which the ground shrugs it off. It darkens over
    /// <see cref="MarkSaturationSpan"/> of the threshold above it, so a mark that clears the bar at all
    /// is worth seeing and everything past a proper slide is equally black.
    /// </summary>
    public static float MarkIntensity(float powerM2S3, float thresholdM2S3) =>
        thresholdM2S3 <= 0f
            ? 0f
            : Math.Clamp((powerM2S3 - thresholdM2S3) / (thresholdM2S3 * MarkSaturationSpan), 0f, 1f);

    /// <summary>
    /// How dark a mark this wheel leaves on the ground beneath it: what it worked the surface with
    /// against the threshold that surface shrugs off and — where the ground is soft enough to be
    /// ploughed — never less than the plough floor, whatever the power.
    /// </summary>
    /// <remarks>
    /// The floor is what makes soft ground different in kind rather than in degree, and whether this
    /// wheel is crossing such ground at all is <see cref="TyreScrub.Ploughing"/> — decided where the
    /// speeds it turns on were already in hand. A hard surface gets none of this and still records
    /// nothing but a slide.
    /// </remarks>
    public static float GroundMarkIntensity(
        SimConfig config, in SurfaceUnderWheel surface, in TyreScrub scrub, float scrubTravelM)
    {
        var slidePowerM2S3 = scrubTravelM >= config.Marks.OnsetM ? scrub.SlidePowerM2S3 : 0f;
        var intensity = MarkIntensity(slidePowerM2S3 + scrub.PloughPowerM2S3, surface.MarkThresholdM2S3);

        return scrub.Ploughing ? MathF.Max(intensity, config.Marks.PloughFloor) : intensity;
    }

    /// <summary>
    /// How far past the threshold, as a share of it, a mark goes from nothing to darkest. Wide,
    /// because the bar itself is minor: what just clears it is a tyre beginning to slide and should
    /// read as a scuff, and the black end belongs to a proper slide several times over it.
    /// </summary>
    const float MarkSaturationSpan = 4f;
}

