using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The wind waves the water is drawn swaying under</b>: three trains of sine waves, the longest down the prevailing
/// wind and two across it (<see cref="ViewFigures.WaterWaveLongM"/>), whose summed height moves the water's picture
/// downwind and lights it at the crests. Read by the ground's stages (<c>ground.vert</c>, <c>ground.frag</c>,
/// <c>town.wgsl</c>) from <see cref="CameraView.Waves"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A train's phase is linear in the place</b>, so the vertex stage works it out at each corner and the interpolation
/// between them is exact. The fragment stage pays three sines and three lumps of value noise, and no texture read.
/// </para>
/// <para>
/// <b>Nothing repeats</b>: each train's crests are bent off their lines by lumps of their own
/// (<see cref="ViewFigures.WaterWaveBendM"/>), and all of them run higher where the water's patches show its lively look
/// (<see cref="ViewFigures.WaterWaveSwellShare"/>) — straight crests of one height cross in a lattice the eye finds.
/// </para>
/// <para>
/// <b>The waterline laps</b>: where the water fades out onto the shore, its cover is moved up the shore under a crest
/// and back under a trough (<see cref="ViewFigures.WaterWaveSwashM"/>), most at the fade's middle and not at its ends,
/// as a fade's ragging is — so the fade still starts and finishes where it is laid. A line of foam rides the moved
/// middle (<see cref="ViewFigures.WaterWaveFoamShare"/>), whitest as a crest runs up: the fade is only as wide as
/// <see cref="ViewFigures.ShoreFadeM"/>, and a soft edge moving within it is hard to see on its own.
/// </para>
/// <para>
/// <b>Each runs at the speed deep water gives its length</b>, ω = √(g k): the wavelength is authored and the speed derived,
/// so a longer train is a faster one, as a sea's swell is. The phase is wrapped here, in double, and never handed over as
/// a time: a float clock a day long would step the crests.
/// </para>
/// <para>
/// <b>The prevailing wind and not the wind of each moment</b>, as the drift is (<see cref="GroundPatches.DriftAt"/>). A
/// paused town's water stands.
/// </para>
/// </remarks>
/// <param name="NumberXPerM">Each train's wavenumber along the town's x, in radians a metre; then the sway downwind
/// under the tallest crest along x, in metres.</param>
/// <param name="NumberYPerM">And along its y.</param>
/// <param name="PhaseRad">How far each train has run at this tick, in radians within one turn.</param>
/// <param name="BendRad">How far each train's crests are bent at most, in radians of its phase; then one over the width
/// of the lumps that bend them, in metres.</param>
/// <param name="Look">
/// How much lighter a crest is drawn (<see cref="ViewFigures.WaterWaveCrestLight"/>), the fewest pixels a train is drawn
/// across (<see cref="ViewFigures.WaterWaveLeastPx"/>) and how far the water's patches move the waves' height
/// (<see cref="ViewFigures.WaterWaveSwellShare"/>).
/// </param>
/// <param name="Shore">How far a crest moves the water's fade up the shore, as a share of its cover
/// (<see cref="SimConfig.WaterWaveSwashShare"/>), and how much of the waterline is foam under a crest
/// (<see cref="ViewFigures.WaterWaveFoamShare"/>).</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct WaterWaves(
    Vector4 NumberXPerM, Vector4 NumberYPerM, Vector4 PhaseRad, Vector4 BendRad, Vector4 Look, Vector4 Shore)
{
    /// <summary>The waves at <paramref name="tick"/>.</summary>
    public static WaterWaves At(Wind wind, SimConfig config, long tick)
    {
        var view = config.View;
        var elapsedS = tick / (double)config.Sim.TickRateHz;
        var gravityMps2 = config.Tyre.StandardGravityMps2;
        var (along0, phase0) = Train(view.WaterWaveLongM, wind.PrevailingRad, gravityMps2, elapsedS);
        var (along1, phase1) = Train(view.WaterWaveMidM, wind.PrevailingRad + config.WaterWaveMidOffWindRad, gravityMps2, elapsedS);
        var (along2, phase2) = Train(view.WaterWaveShortM, wind.PrevailingRad + config.WaterWaveShortOffWindRad, gravityMps2, elapsedS);
        var (sin, cos) = MathF.SinCos(wind.PrevailingRad);
        var bendM = view.WaterWaveBendM;
        return new WaterWaves(
            new Vector4(along0.X, along1.X, along2.X, cos * view.WaterWaveSwayM),
            new Vector4(along0.Y, along1.Y, along2.Y, sin * view.WaterWaveSwayM),
            new Vector4(phase0, phase1, phase2, 0f),
            new Vector4(bendM * along0.Length(), bendM * along1.Length(), bendM * along2.Length(), 1f / view.WaterWaveBendAcrossM),
            new Vector4(view.WaterWaveCrestLight, view.WaterWaveLeastPx, view.WaterWaveSwellShare, 0f),
            new Vector4(config.WaterWaveSwashShare, view.WaterWaveFoamShare, 0f, 0f));
    }

    /// <summary>One train <paramref name="lengthM"/> crest to crest running toward <paramref name="towardRad"/>: its
    /// wavenumber and how far it has run after <paramref name="elapsedS"/>.</summary>
    static (Vector2 NumberPerM, float PhaseRad) Train(float lengthM, float towardRad, float gravityMps2, double elapsedS)
    {
        var numberPerM = 2f * MathF.PI / lengthM;
        var radPerS = Math.Sqrt(gravityMps2 * numberPerM);
        var (sin, cos) = MathF.SinCos(towardRad);
        return (new Vector2(cos, sin) * numberPerM, (float)Math.IEEERemainder(radPerS * elapsedS, 2.0 * Math.PI));
    }
}
