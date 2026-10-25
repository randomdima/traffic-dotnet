using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The clouds' shadows</b> (OBJ-8): one triangle over the whole target (<c>cloud.vert</c>, <c>cloud.frag</c>), drawn
/// over everything that stands and under the interface, darkening the town wherever the clouds' field
/// (<see cref="Clouds"/>) is thick enough to be cloud. The clouds themselves are never drawn.
/// </summary>
/// <remarks>
/// <para>
/// <b>The field is <c>cloud.frag</c>'s</b>: six octaves of value noise, each half as wide as the last and turned off it,
/// over the town's plane and the clouds' lives, and bent by two coarse ones of their own — <see cref="FieldMean"/>,
/// <see cref="FieldSpread"/> and <see cref="FieldSlope"/> are that field's own, measured once over 300 000 samples of
/// it, and change with it.
/// </para>
/// <para>
/// <b>A cover is a threshold</b>: the field is near enough normal, so the ground under cloud is where it stands above the
/// quantile of the clear share, taken by Tukey's lambda approximation of the probit — within 0.006 of the field's own
/// quantile from a fifth of the sky covered to three fifths. <b>A shadow begins half the penumbra outside that edge</b>
/// (<see cref="SimConfig.CloudPenumbraM"/>) <b>and is deepest half of it past <see cref="CloudFigures.ThickensOverM"/>
/// inside</b>: the field moves by <see cref="FieldSlope"/> a cloud's width at its edges, so each length is that share of
/// itself in clouds.
/// </para>
/// </remarks>
internal static class CloudShadows
{
    const float FieldMean = 0.5f;
    const float FieldSpread = 0.117f;
    const float FieldSlope = 0.66f;

    /// <summary>Tukey's lambda approximation of the probit (Tukey 1960): <c>z(p) ≈ Scale · (p^λ − (1 − p)^λ)</c>.</summary>
    const float ProbitScale = 4.91f;

    const float ProbitLambda = 0.14f;

    /// <summary>The clouds' shadows at <paramref name="tick"/>, as the cloud stages read them (<see cref="CameraView.Clouds"/>).</summary>
    /// <exception cref="ArgumentException">A cover that is no share, or a shadow whose edge has no width.</exception>
    public static CloudShadow At(Clouds clouds, SimConfig config, long tick)
    {
        var figures = config.Clouds;
        if (figures.CoverShare is < 0f or > 1f)
        {
            throw new ArgumentException($"The sky is {figures.CoverShare} covered: a cover is a share, between nought and one.");
        }

        var penumbraM = config.CloudPenumbraM;
        if (penumbraM <= 0f)
        {
            throw new ArgumentException($"A cloud's shadow blurs over {penumbraM} m: a cloud above the town under a sun of some width blurs over some.");
        }

        var elapsedS = tick / (double)config.Sim.TickRateHz;
        var perAcrossM = 1f / figures.AcrossM;
        var edge = FieldMean + (FieldSpread * Probit(1f - figures.CoverShare));
        var perM = perAcrossM * FieldSlope;
        var shade = figures.CoverShare > 0f ? figures.ShadeShare : 0f;
        return new CloudShadow(
            new Vector4(clouds.FieldAtM(elapsedS), clouds.LivesAt(elapsedS), 0f),
            new Vector4(
                perAcrossM, edge - (penumbraM / 2f * perM), edge + (((penumbraM / 2f) + figures.ThickensOverM) * perM), shade));
    }

    /// <summary>The standard normal's quantile at <paramref name="p"/>, to Tukey's approximation; the ground's patches
    /// take their looks' threshold by it too (<see cref="GroundPatches"/>).</summary>
    internal static float Probit(float p) => ProbitScale * (MathF.Pow(p, ProbitLambda) - MathF.Pow(1f - p, ProbitLambda));
}

/// <summary>
/// <b>The clouds' shadows as the cloud stages read them</b> (<see cref="CloudShadows.At"/>): where the town's origin is in
/// the field and how many lives it has lived, then a cloud's width as one over it, where in the field a shadow begins and
/// where it is deepest, and how much light it takes there. A shade of nought draws none.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CloudShadow(Vector4 Field, Vector4 Look);
