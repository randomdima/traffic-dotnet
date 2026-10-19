using System.Numerics;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.Agents.Train.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Rail;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Train;

/// <summary>
/// <b>A train run along a straight track across a map of its own</b> (TRN-1…8): when it is sent, where its wagons stand,
/// how fast it runs, and that it leaves.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class TrainsTests
{
    const float MapM = 1000f;

    /// <summary>The map's middle row, which the track runs along from far west to far east.</summary>
    const float RowM = 500f;

    const float RunOffM = 600f;

    /// <summary>A line sending its first train at once, an intercity every time, and the next a long wait later.</summary>
    static readonly SimConfig Config = new()
    {
        Train = new TrainFigures { FirstLeastS = 0.5f, FirstMostS = 0.5f, IntercityShare = 1f, OilShare = 0f, GapLeastS = 1000f, GapMostS = 1000f },
    };

    /// <param name="bridgedEast">Whether the track's east half, from the map's middle on, stands on a bridge.</param>
    static (Trains Trains, TrackLine Track) Laid(bool bridgedEast = false)
    {
        var rails = new CityPlan.RailArrays
        {
            Line = [0], Runs = [TownMap.TrackRuns.Forward], PointOffsets = [0, 3],
            PointM = [new(-RunOffM, RowM), new(MapM * 0.5f, RowM), new(MapM + RunOffM, RowM)],
            Level = bridgedEast ? [TownMap.RailArrays.AtGrade, TownMap.RailArrays.Above, TownMap.RailArrays.Above] : new byte[3],
            Ballast = CityPlan.RingArrays.None,
        };
        var tracks = TrackLine.Of(rails);
        return (new Trains(rails, tracks, new Vector2(MapM), TrainCatalog.Shared, Config, agentSeed: 1), tracks[0]);
    }

    /// <summary>The slot a line's locomotive of this kind stands in: the first of the line's slots.</summary>
    const int Locomotive = 0;

    static void Run(Trains trains, float forS)
    {
        for (var tick = (int)MathF.Round(forS / Config.TickSeconds); tick > 0; tick--) trains.Advance(Config.TickSeconds);
    }

    [Fact]
    public void AWagonOffTheMapIsOutOfTheWorld()
    {
        var (trains, _) = Laid();
        Run(trains, 1f);

        Assert.False(trains.InWorld(Locomotive));
    }

    [Fact]
    public void ALocomotiveComesIntoTheWorldAsItsFrontReachesTheMap()
    {
        var (trains, _) = Laid();
        var halfM = TrainCatalog.Shared.ShapeOf(VehicleKind.Electric).FootprintM.X * 0.5f;
        var tickM = Config.Train.IntercitySpeedMps * Config.TickSeconds;
        for (var tick = 0; tick < 1_000_000 && !trains.InWorld(Locomotive); tick++) trains.Advance(Config.TickSeconds);

        Assert.InRange(trains.PositionM(Locomotive).X, -halfM, -halfM + tickM);
    }

    [Fact]
    public void AWagonStandsAlongItsTrack()
    {
        var (trains, _) = Laid();
        Run(trains, 60f);

        Assert.Equal(RowM, trains.PositionM(Locomotive).Y, 3);
        Assert.Equal(0f, trains.HeadingRad(Locomotive), 4);
    }

    [Fact]
    public void ATrainRunsAtItsKindsSpeed()
    {
        var (trains, _) = Laid();
        Run(trains, 50f);
        var wasM = trains.PositionM(Locomotive);
        Run(trains, 1f);

        Assert.Equal(Config.Train.IntercitySpeedMps, trains.PositionM(Locomotive).X - wasM.X, 1);
    }

    [Fact]
    public void ATrainWhoseLastWagonHasLeftTheMapIsOutOfTheWorld()
    {
        var (trains, _) = Laid();

        // Two warnings short of the map, across it, and its whole length past it.
        var runM = (Config.Train.IntercitySpeedMps * Config.Rail.WarningS * 2f) + MapM + 400f;
        Run(trains, Config.Train.FirstMostS + (runM / Config.Train.IntercitySpeedMps));

        for (var wagon = 0; wagon < trains.WagonCount; wagon++) Assert.False(trains.InWorld(wagon));
        Assert.Equal(0, trains.Runs.Length);
    }

    [Fact]
    public void AWagonWhoseFrontIsOverABridgeIsOnTheBridgesChannel()
    {
        var (trains, _) = Laid(bridgedEast: true);
        var quarterM = TrainCatalog.Shared.ShapeOf(VehicleKind.Electric).FootprintM.X * 0.25f;
        for (var tick = 0; tick < 1_000_000 && trains.PositionM(Locomotive).X < (MapM * 0.5f) - quarterM; tick++) trains.Advance(Config.TickSeconds);

        Assert.Equal(CityPlan.RailArrays.BridgeChannel, trains.ChannelsOf(Locomotive));
    }

    [Fact]
    public void RunningATrainAllocatesNothing()
    {
        var (trains, _) = Laid();
        Run(trains, 1f);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Run(trains, 120f);

        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ACrossingClosesAWarningAheadOfTheFront()
    {
        var run = new TrackRun(0, FrontM: 100f, LengthM: 150f, Forward: true, SpeedMps: 20f);
        var warningM = 20f * Config.Rail.WarningS;

        Assert.True(run.Closes(100f + warningM + 2f - 0.01f, reachM: 2f, Config.Rail, laterS: 0f));
        Assert.False(run.Closes(100f + warningM + 2f + 0.01f, reachM: 2f, Config.Rail, laterS: 0f));
    }

    [Fact]
    public void ACrossingOpensAClearingTimeAfterTheLastWagon()
    {
        var run = new TrackRun(0, FrontM: 500f, LengthM: 150f, Forward: true, SpeedMps: 20f);
        var clearedM = 500f - 150f - 2f - (20f * Config.Rail.ClearS);

        Assert.True(run.Closes(clearedM + 0.01f, reachM: 2f, Config.Rail, laterS: 0f));
        Assert.False(run.Closes(clearedM - 0.01f, reachM: 2f, Config.Rail, laterS: 0f));
    }
}
