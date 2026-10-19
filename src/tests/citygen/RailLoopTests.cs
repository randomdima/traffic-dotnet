using System.Numerics;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>A brief's loop as it is authored</b> (RAIL-11, RAIL-2, RAIL-8): the suite's own city with a double track turned
/// three quarters round its middle, as every authored map's is.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class RailLoopTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const float ThreeQuartersDeg = 270f;

    static readonly Lazy<TownMap.RailArrays> Tracks = new(() =>
    {
        var draw = new Rng(Towns.CitySeed, Streams.Railway);
        return RailLines.Author(Towns.Brief(Towns.CitySeed, railTracks: 2, railLoopDeg: ThreeQuartersDeg), Config, ref draw);
    });

    /// <summary>
    /// <b>No track of the loop bends tighter than the least radius</b>, its inner one included. The points are floats, so
    /// a circle through three of them is read to within the chord tolerance they are laid to, a stride of them apart.
    /// </summary>
    [Fact]
    public void NoTrackOfTheLoopBendsTighterThanItsLeastRadius()
    {
        const int Stride = 10;
        var tracks = Tracks.Value;
        for (var track = 0; track < tracks.Count; track++)
        {
            var lineM = tracks.TrackOf(track);
            for (var point = Stride; point + Stride < lineM.Length; point++)
            {
                var radiusM = RadiusM(lineM[point - Stride], lineM[point], lineM[point + Stride]);
                Assert.True(
                    radiusM >= Config.Rail.LeastRadiusM - Config.Rail.ChordToleranceM,
                    $"track {track} bends at {radiusM:F2} m at point {point}");
            }
        }
    }

    /// <summary>
    /// <b>Wherever a chord of the line crosses another, one of the two is on the bridge</b> (RAIL-8): the way out passes
    /// over the way in, and the tracks meet nowhere else (RAIL-2).
    /// </summary>
    [Fact]
    public void WhereTheLineCrossesItselfOneOfTheTwoIsOnTheBridge()
    {
        var tracks = Tracks.Value;
        var crossings = 0;
        for (var one = 0; one < tracks.Count; one++)
        {
            for (var other = one; other < tracks.Count; other++)
            {
                var oneM = tracks.TrackOf(one);
                var otherM = tracks.TrackOf(other);
                for (var a = 0; a + 1 < oneM.Length; a++)
                {
                    for (var b = one == other ? a + 2 : 0; b + 1 < otherM.Length; b++)
                    {
                        if (!Crosses(oneM[a], oneM[a + 1], otherM[b], otherM[b + 1])) continue;

                        crossings++;
                        var (aLevel, bLevel) = (LevelOf(tracks, one, a), LevelOf(tracks, other, b));
                        Assert.True(
                            aLevel != bLevel,
                            $"track {one} chord {a} crosses track {other} chord {b}, both at level {aLevel}");
                    }
                }
            }
        }

        // A double line crossing itself crosses each track's way in with each track's way out.
        Assert.Equal(tracks.Count * tracks.Count, crossings);
    }

    /// <summary>
    /// <b>A train climbs to the bridge no steeper than the ruling grade</b> (RAIL-11): from either end of the bridge to
    /// where it passes over the track, as far as climbing to its clearance at that grade takes.
    /// </summary>
    [Fact]
    public void ATrainClimbsToTheBridgeNoSteeperThanTheRulingGrade()
    {
        var climbM = Config.Rail.BridgeClearanceM / Config.Rail.GradeMostShare;
        var tracks = Tracks.Value;
        for (var track = 0; track < tracks.Count; track++)
        {
            Assert.True(ClimbsAtMostTheRulingGrade(tracks, track, climbM), $"no bridge of track {track} passes over it");
        }
    }

    /// <summary>Whether the track's bridge climbs at least <paramref name="climbM"/> to where it passes over the track, and comes down in as much.</summary>
    static bool ClimbsAtMostTheRulingGrade(TownMap.RailArrays tracks, int track, float climbM)
    {
        var lineM = tracks.TrackOf(track);
        for (var over = 0; over + 1 < lineM.Length; over++)
        {
            if (LevelOf(tracks, track, over) == TownMap.RailArrays.AtGrade) continue;

            for (var under = 0; under + 1 < lineM.Length; under++)
            {
                if (!Crosses(lineM[over], lineM[over + 1], lineM[under], lineM[under + 1])) continue;

                var (from, to) = (over, over);
                while (from > 0 && LevelOf(tracks, track, from - 1) != TownMap.RailArrays.AtGrade) from--;
                while (to + 2 < lineM.Length && LevelOf(tracks, track, to + 1) != TownMap.RailArrays.AtGrade) to++;
                var intoM = Vector2.Distance(lineM[over], lineM[over + 1]) * ShareAlong(lineM[over], lineM[over + 1], lineM[under], lineM[under + 1]);
                var upM = AlongM(lineM, from, over) + intoM;
                var downM = AlongM(lineM, over, to + 1) - intoM;
                Assert.True(upM >= climbM, $"track {track}'s bridge climbs {upM:F1} m to the crossing");
                Assert.True(downM >= climbM, $"track {track}'s bridge comes down in {downM:F1} m");
                return true;
            }
        }

        return false;
    }

    /// <summary>How far along a chord, as a share of it, another chord crosses it.</summary>
    static float ShareAlong(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Cross(Vector2 one, Vector2 other) => (one.X * other.Y) - (one.Y * other.X);

        return Cross(c - a, d - c) / Cross(b - a, d - c);
    }

    static float AlongM(ReadOnlySpan<Vector2> lineM, int from, int to)
    {
        var alongM = 0f;
        for (var point = from; point < to; point++) alongM += Vector2.Distance(lineM[point], lineM[point + 1]);
        return alongM;
    }

    /// <summary>
    /// <b>The loop is short enough that every train is over itself on it</b> (RAIL-11): along its track, the way out's
    /// pass over the way in is nearer the way in's pass under it than any train is long.
    /// </summary>
    [Theory]
    [InlineData("Electric", "Coach")]
    [InlineData("Diesel", "Tank")]
    [InlineData("Diesel", "Coal")]
    public void EveryTrainIsOverItselfOnTheLoop(string leadKind, string wagonKind)
    {
        var (lead, wagon) = (Enum.Parse<VehicleKind>(leadKind), Enum.Parse<VehicleKind>(wagonKind));
        var wagons = wagon switch
        {
            VehicleKind.Coach => Config.Train.IntercityCoaches,
            VehicleKind.Tank => Config.Train.OilWagons,
            _ => Config.Train.CoalWagons,
        };
        var catalog = TrainCatalog.Shared;
        var trainM = catalog.ShapeOf(lead).FootprintM.X + (wagons * catalog.ShapeOf(wagon).FootprintM.X);

        var lineM = Tracks.Value.TrackOf(0);
        for (var a = 0; a + 1 < lineM.Length; a++)
        {
            for (var b = a + 2; b + 1 < lineM.Length; b++)
            {
                if (!Crosses(lineM[a], lineM[a + 1], lineM[b], lineM[b + 1])) continue;

                var betweenM = 0f;
                for (var chord = a; chord < b; chord++) betweenM += Vector2.Distance(lineM[chord], lineM[chord + 1]);
                Assert.True(betweenM < trainM, $"the loop runs {betweenM:F0} m between its passes, and a {wagonKind} train is {trainM:F0} m");
                return;
            }
        }

        Assert.Fail("the line never crosses itself");
    }

    static byte LevelOf(TownMap.RailArrays tracks, int track, int chord) => tracks.Level[tracks.PointOffsets[track] + chord];

    static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Side(Vector2 from, Vector2 to, Vector2 at) => ((to.X - from.X) * (at.Y - from.Y)) - ((to.Y - from.Y) * (at.X - from.X));

        return Side(a, b, c) * Side(a, b, d) < 0f && Side(c, d, a) * Side(c, d, b) < 0f;
    }

    static double RadiusM(Vector2 a, Vector2 b, Vector2 c)
    {
        double abX = b.X - a.X, abY = b.Y - a.Y, acX = c.X - a.X, acY = c.Y - a.Y, bcX = c.X - b.X, bcY = c.Y - b.Y;
        var twiceArea = Math.Abs((abX * acY) - (abY * acX));
        return twiceArea == 0
            ? double.PositiveInfinity
            : Math.Sqrt(((abX * abX) + (abY * abY)) * ((bcX * bcX) + (bcY * bcY)) * ((acX * acX) + (acY * acY))) / (2 * twiceArea);
    }
}
