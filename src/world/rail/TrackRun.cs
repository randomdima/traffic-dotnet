using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Rail;

/// <summary>
/// <b>Where a train is on its track</b>, as the railway is told it by whatever runs the trains: the track, how far along
/// it the train's front is, how long the train is, which way it runs the track, and how it moves along it. Data handed
/// down, so the railway never learns what a train is.
/// </summary>
/// <param name="Forward">Whether it runs the track from its first point toward its last.</param>
/// <param name="SpeedMps">How fast it moves the way it runs the track, back as negative.</param>
/// <param name="MostMps2">
/// The most it may be pulled at, either way, by whoever drives it: nought for a driver holding its pace, which never
/// pulls past it (TRN-12).
/// </param>
internal readonly record struct TrackRun(int Track, float FrontM, float LengthM, bool Forward, float SpeedMps, float MostMps2 = 0f)
{
    /// <summary>
    /// <b>Whether this train closes a crossing</b> standing at a distance along its track and reaching a distance either
    /// way along it (RAIL-1): from a warning time before either end of it can reach its near edge until a clearing time
    /// after its trailing end has left its far one — now, or at any time over the next <paramref name="laterS"/>. It reaches
    /// as far as it could be pulled in that time, so a train standing under a hand shuts a crossing it could reach.
    /// </summary>
    public bool Closes(float atM, float reachM, RailFigures rail, float laterS)
    {
        var aheadM = Forward ? atM - FrontM : FrontM - atM;
        var warnS = rail.WarningS + laterS;
        var onClearM = MathF.Max(SpeedMps, 0f) * rail.ClearS;
        var backClearM = MathF.Max(-SpeedMps, 0f) * rail.ClearS;
        return aheadM - reachM <= ReachM(SpeedMps, MostMps2, warnS) + backClearM
               && aheadM + LengthM + reachM + onClearM + ReachM(-SpeedMps, MostMps2, warnS) >= 0f;
    }

    /// <summary>
    /// How far on its way a train moving at <paramref name="speedMps"/> — nought or less for one going the other way — can
    /// be in <paramref name="seconds"/>, pulled the whole time at <paramref name="mostMps2"/>.
    /// </summary>
    public static float ReachM(float speedMps, float mostMps2, float seconds) =>
        (MathF.Max(speedMps, 0f) * seconds) + (0.5f * mostMps2 * seconds * seconds);
}
