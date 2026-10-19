using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Rail;

/// <summary>Where a line crosses one track: how far along the line, the track, how far along it, and how square the two meet.</summary>
internal readonly record struct CrossingMeeting(float AlongLineM, int Track, float AlongTrackM, float Sin);

/// <summary>
/// The stretch of a line one railway line's crossing holds (RAIL-1), in the line's own metres, and the meetings it is
/// closed by — <see cref="FirstMeeting"/> to <see cref="LastMeeting"/>, both counted. A driven line's also says which
/// barrier it meets coming in (<see cref="LevelCrossings.Approach"/>, −1 for none) and how far along it that stands.
/// </summary>
internal readonly record struct CrossingStretch(float FromM, float ToM, int FirstMeeting, int LastMeeting, int Approach, float BarrierM);

/// <summary>How a line meets a level crossing: driven one way, walked both, or painted down a road both ways are driven.</summary>
internal enum CrossingLineKind : byte
{
    Driven,
    Walked,
    Painted,
}

/// <summary>
/// <b>Where a line across the railway at grade is held, or painted as a junction is</b> (RAIL-1, RAIL-7): a driven line
/// from its stop bar, a setback short of the barrier it meets coming in, to the ballast's far edge; a walked one from a
/// clearance short of the nearest rail to as far past the furthest, a walk being walked both ways; a painted one from
/// stop bar to stop bar. The holds, the ground's bars and its lines all read it here, so a bar is where its hold starts.
/// </summary>
/// <remarks>The meetings of one line with one railway line near each other are one stretch: tracks a spacing apart, a slant's worth over.</remarks>
internal static class CrossingStretches
{
    /// <param name="found">A working list for <see cref="TrackChords.Crossings"/>.</param>
    /// <param name="meetings">Filled with every meeting of the line, in order along it.</param>
    /// <param name="into">Filled with every stretch, over <paramref name="meetings"/>.</param>
    public static void Along(
        ReadOnlySpan<ArcSeg> line, float lengthM, CrossingLineKind kind, TrackChords chords, LevelCrossings crossings,
        CityPlan.RailArrays rails, RailFigures rail,
        List<(float AlongPieceM, int Track, float AlongTrackM, float Sin)> found, List<CrossingMeeting> meetings, List<CrossingStretch> into)
    {
        meetings.Clear();
        into.Clear();
        var pieceStartM = 0f;
        foreach (var piece in line)
        {
            chords.Crossings(piece, found);
            foreach (var (alongPieceM, track, alongTrackM, sin) in found) meetings.Add(new CrossingMeeting(pieceStartM + alongPieceM, track, alongTrackM, sin));

            pieceStartM += piece.LengthM;
        }

        meetings.Sort((one, other) => one.AlongLineM.CompareTo(other.AlongLineM));
        var (ballastHalfM, railOffM) = (rail.BallastWidthM * 0.5f, (rail.GaugeM * 0.5f) + rail.RailHeadWidthM);
        for (var first = 0; first < meetings.Count;)
        {
            var railway = rails.Line[meetings[first].Track];
            var last = first;
            while (last + 1 < meetings.Count && rails.Line[meetings[last + 1].Track] == railway
                   && meetings[last + 1].AlongLineM - meetings[last].AlongLineM
                   <= LevelCrossings.OneCrossingSpacings * rail.TrackSpacingM / meetings[last + 1].Sin)
            {
                last++;
            }

            var (firstM, lastM) = (meetings[first].AlongLineM, meetings[last].AlongLineM);
            var (nearestRailM, furthestRailM) = (firstM - (railOffM / meetings[first].Sin), lastM + (railOffM / meetings[last].Sin));
            var (approach, barrierM) = (-1, float.NaN);
            float nearM, farM;
            if (kind == CrossingLineKind.Walked)
            {
                (nearM, farM) = (nearestRailM - rail.BarrierRailClearanceM, furthestRailM + rail.BarrierRailClearanceM);
            }
            else
            {
                var at = Spline.SampleAt(line, firstM);
                approach = BarrierMet(crossings, railway, at.PositionM, at.Direction, out var backM);
                barrierM = approach < 0 ? nearestRailM - rail.BarrierRailClearanceM : firstM - backM;
                nearM = barrierM - rail.StopLineSetbackM;
                farM = lastM + (ballastHalfM / meetings[last].Sin);
                if (kind == CrossingLineKind.Painted)
                {
                    at = Spline.SampleAt(line, lastM);
                    farM = BarrierMet(crossings, railway, at.PositionM, -at.Direction, out var aheadM) < 0
                        ? furthestRailM + rail.BarrierRailClearanceM + rail.StopLineSetbackM
                        : lastM + aheadM + rail.StopLineSetbackM;
                }
            }

            into.Add(new CrossingStretch(MathF.Max(0f, nearM), MathF.Min(lengthM, farM), first, last, approach, barrierM));
            first = last + 1;
        }
    }

    /// <summary>
    /// <b>Which barrier of a railway line's crossings a line coming in <paramref name="way"/> to <paramref name="atM"/> meets</b>
    /// (<see cref="LevelCrossings.Approach"/>), and how far back along it that stands: of those it runs through behind the place,
    /// coming in their way, the one it runs through nearest its middle. −1 where it meets none.
    /// </summary>
    static int BarrierMet(LevelCrossings crossings, int railway, Vector2 atM, Vector2 way, out float backM)
    {
        var (met, leastAcrossM) = (-1, float.PositiveInfinity);
        backM = float.NaN;
        for (var crossing = 0; crossing < crossings.Crossings; crossing++)
        {
            if (crossings.Line[crossing] != railway) continue;

            foreach (var withTheRoad in (ReadOnlySpan<bool>)[true, false])
            {
                var barrier = crossings.Barrier(crossing, withTheRoad);
                var square = Vector2.Dot(way, barrier.Travel);
                if (square <= 0f) continue;

                var behindM = Vector2.Dot(atM - barrier.MiddleM, barrier.Travel) / square;
                var acrossM = MathF.Abs(Vector2.Dot(atM - (way * behindM) - barrier.MiddleM, barrier.TowardTheKerb));
                if (behindM <= 0f || acrossM >= leastAcrossM) continue;

                (met, leastAcrossM, backM) = (LevelCrossings.Approach(crossing, withTheRoad), acrossM, behindM);
            }
        }

        return met;
    }
}
