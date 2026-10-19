using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Rail;

/// <summary>
/// <b>Where a train is on its track</b>, as the railway is told it by whatever runs the trains: the track, how far along
/// it the train's front is, how long the train is, which way it runs the track and how fast. Data handed down, so the
/// railway never learns what a train is.
/// </summary>
/// <param name="Forward">Whether it runs the track from its first point toward its last.</param>
internal readonly record struct TrackRun(int Track, float FrontM, float LengthM, bool Forward, float SpeedMps)
{
    /// <summary>
    /// <b>Whether this train closes a crossing</b> standing at a distance along its track and reaching a distance either
    /// way along it (RAIL-1): from a warning time before the front reaches its near edge until a clearing time after the
    /// last wagon has left its far one — now, or at any time over the next <paramref name="laterS"/>.
    /// </summary>
    public bool Closes(float atM, float reachM, RailFigures rail, float laterS)
    {
        var aheadM = Forward ? atM - FrontM : FrontM - atM;
        return aheadM - reachM <= SpeedMps * (rail.WarningS + laterS)
               && aheadM + LengthM + reachM + (SpeedMps * rail.ClearS) >= 0f;
    }
}
