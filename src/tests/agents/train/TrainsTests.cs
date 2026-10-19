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
/// <b>The trains' controller</b> (TRN-1…9): when a train is sent, where and how it is put down, what its driver asks,
/// and when it is taken out of the world — over a world stood in by kinematics, each vehicle moving on as it was put down.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class TrainsTests
{
    const float MapM = 1000f;

    /// <summary>The map's middle row, which the main track runs along from far west to far east.</summary>
    const float RowM = 500f;

    const float RunOffM = 600f;

    /// <summary>Trains sent at once, intercity every time, and the next a long wait later.</summary>
    static readonly SimConfig Config = new()
    {
        Train = new TrainFigures { FirstLeastS = 0.5f, FirstMostS = 0.5f, IntercityShare = 1f, OilShare = 0f, GapLeastS = 1000f, GapMostS = 1000f },
    };

    static float SpeedMps => Config.Train.IntercitySpeedMps;

    static CityPlan.RailArrays Straight(TownMap.TrackRuns runs, bool bridgedEast = false) => new()
    {
        Line = [0], Runs = [runs], PointOffsets = [0, 3],
        PointM = [new(-RunOffM, RowM), new(MapM * 0.5f, RowM), new(MapM + RunOffM, RowM)],
        Level = bridgedEast ? [TownMap.RailArrays.AtGrade, TownMap.RailArrays.Above, TownMap.RailArrays.Above] : new byte[3],
        Ballast = CityPlan.RingArrays.None,
    };

    /// <summary>
    /// <b>The world stood in by kinematics</b>: every vehicle put down moves on along its heading at the speed it was put
    /// down at, and is out of the world once contained — what a train on a straight looks like to its controller.
    /// </summary>
    sealed class Rig
    {
        public readonly Trains Trains;
        public readonly RailGraph Graph;
        public readonly RailVehicleSeen[] Seen;
        public readonly bool[] InWorld;
        public readonly byte[] Channels;
        public readonly TrainOrders Orders = new();
        public readonly List<TrainOrder> Given = [];

        public Rig(CityPlan.RailArrays rails)
        {
            Graph = RailGraph.Of(rails, new Vector2(MapM));
            Trains = new Trains(Graph, rails, new Vector2(MapM), TrainCatalog.Shared, Config, agentSeed: 1);
            Seen = new RailVehicleSeen[Trains.WagonCount];
            InWorld = new bool[Trains.WagonCount];
            Channels = new byte[Trains.WagonCount];
        }

        public void Run(float forS, bool keep = false)
        {
            for (var tick = (int)MathF.Round(forS / Config.TickSeconds); tick > 0; tick--) Tick(keep);
        }

        public void Tick(bool keep = false)
        {
            Orders.Clear();
            Trains.Advance(Seen, Config.TickSeconds, held: false, Orders);
            foreach (var order in Orders.Written)
            {
                if (keep) Given.Add(order);
                switch (order.Kind)
                {
                    case TrainOrderKind.Place:
                        var forward = new Vector2(MathF.Cos(order.HeadingRad), MathF.Sin(order.HeadingRad));
                        Seen[order.Wagon] = new RailVehicleSeen(order.AtM, forward, forward * order.SpeedMps);
                        InWorld[order.Wagon] = true;
                        break;

                    case TrainOrderKind.Contain:
                        InWorld[order.Wagon] = false;
                        break;

                    case TrainOrderKind.Channels:
                        Channels[order.Wagon] = (byte)order.Other;
                        break;
                }
            }

            for (var wagon = 0; wagon < Seen.Length; wagon++)
            {
                if (!InWorld[wagon]) continue;

                var seen = Seen[wagon];
                Seen[wagon] = seen with { PositionM = seen.PositionM + (seen.VelocityMps * Config.TickSeconds) };
            }
        }
    }

    /// <summary>
    /// <b>A train is put down on its run-in moving at its line's speed and coupled front to back</b> (TRN-7): each vehicle
    /// on the track, laid along it a coupling's gap behind the one ahead, its front a warning's run short of the map (RAIL-1).
    /// </summary>
    [Fact]
    public void ATrainIsPutDownOnItsRunInAtItsLinesSpeedAndCoupled()
    {
        var rig = new Rig(Straight(TownMap.TrackRuns.Forward));
        rig.Run(1f, keep: true);

        var placed = rig.Given.Where(order => order.Kind == TrainOrderKind.Place).ToArray();
        Assert.Equal(1 + Config.Train.IntercityCoaches, placed.Length);
        Assert.All(placed, order => Assert.Equal(SpeedMps, order.SpeedMps));
        Assert.All(placed, order => Assert.Equal(RowM, order.AtM.Y, 3));
        Assert.All(placed, order => Assert.Equal(0f, order.HeadingRad, 4));

        var loco = TrainCatalog.Shared.ShapeOf(VehicleKind.Electric);
        var coach = TrainCatalog.Shared.ShapeOf(VehicleKind.Coach);
        var locoFrontM = placed[0].AtM.X + (loco.FootprintM.X * 0.5f);
        Assert.Equal(-SpeedMps * Config.Rail.WarningS, locoFrontM, 1);
        Assert.Equal(
            (loco.FootprintM.X * 0.5f) + Config.Train.CouplingGapM + (coach.FootprintM.X * 0.5f), placed[0].AtM.X - placed[1].AtM.X, 3);

        var couples = rig.Given.Where(order => order.Kind == TrainOrderKind.Couple).ToArray();
        Assert.Equal(-1, couples[0].Other);
        for (var vehicle = 1; vehicle < couples.Length; vehicle++) Assert.Equal(couples[vehicle - 1].Wagon, couples[vehicle].Other);
    }

    /// <summary><b>A train whose last vehicle has left the map is taken out of the world whole</b> and uncoupled (TRN-7).</summary>
    [Fact]
    public void ATrainWhoseTailHasLeftTheMapIsTakenOutWhole()
    {
        var rig = new Rig(Straight(TownMap.TrackRuns.Forward));
        rig.Run(1f);
        Assert.Contains(true, rig.InWorld);

        var runM = (SpeedMps * Config.Rail.WarningS) + MapM + 200f;
        rig.Run(runM / SpeedMps, keep: true);

        Assert.DoesNotContain(true, rig.InWorld);
        Assert.Contains(rig.Given, order => order.Kind == TrainOrderKind.Couple && order.Other == -1);
        Assert.Equal(0, rig.Trains.Runs.Length);
    }

    /// <summary><b>Any of a vehicle over a bridge puts it on the bridge's channel</b> (RAIL-8).</summary>
    [Fact]
    public void AVehicleAnyOfWhichIsOverABridgeIsOnTheBridgesChannel()
    {
        var rig = new Rig(Straight(TownMap.TrackRuns.Forward, bridgedEast: true));
        rig.Run(1f);
        var loco = rig.Trains.VehiclesOf(0)[0];
        var halfM = TrainCatalog.Shared.ShapeOf(VehicleKind.Electric).FootprintM.X * 0.5f;
        for (var tick = 0; tick < 100_000 && rig.Seen[loco].PositionM.X + halfM < (MapM * 0.5f) + 1f; tick++) rig.Tick();

        Assert.Equal(CityPlan.RailArrays.BridgeChannel, rig.Channels[loco]);
    }

    /// <summary>
    /// <b>Two trains are never out on legs that share ground</b> (TRN-5): a single track run both ways has a train a way
    /// onto it, and while one is out the other waits.
    /// </summary>
    [Fact]
    public void ASecondTrainWaitsWhileTheFirstIsOutOnTheTrackTheyShare()
    {
        var rig = new Rig(Straight(TownMap.TrackRuns.Both));
        Assert.Equal(2, rig.Trains.Count);

        rig.Run(10f);
        Assert.Equal(1, rig.Trains.Sent);
        Assert.True(rig.Trains.WaitedForGround > 0, "the second train never waited for the first");

        rig.Run(((SpeedMps * Config.Rail.WarningS) + MapM + 400f) / SpeedMps);
        Assert.Equal(2, rig.Trains.Sent);
    }

    /// <summary>
    /// <b>The driver sets a facing switch on its leg the set-ahead distance before its front reaches it</b> (TRN-9), and
    /// not before.
    /// </summary>
    [Fact]
    public void TheDriverSetsAFacingSwitchAheadOfItsFront()
    {
        var rails = new CityPlan.RailArrays
        {
            Line = [0, 0], Runs = [TownMap.TrackRuns.Forward, TownMap.TrackRuns.Forward], PointOffsets = [0, 2, 4],
            PointM = [new(-RunOffM, RowM), new(MapM + RunOffM, RowM), new(MapM * 0.5f, RowM), new(MapM + RunOffM, RowM + 700f)],
            Level = new byte[4], Ballast = CityPlan.RingArrays.None,
        };
        var rig = new Rig(rails);
        rig.Run(1f, keep: true);
        Assert.DoesNotContain(rig.Given, order => order.Kind == TrainOrderKind.Switch);

        var loco = rig.Trains.VehiclesOf(0)[0];
        var halfM = TrainCatalog.Shared.ShapeOf(VehicleKind.Electric).FootprintM.X * 0.5f;
        var setAtM = (MapM * 0.5f) - Config.Rail.SwitchSetAheadM;
        var frontM = 0f;
        for (var tick = 0; tick < 100_000 && !rig.Given.Exists(order => order.Kind == TrainOrderKind.Switch); tick++)
        {
            frontM = rig.Seen[loco].PositionM.X + halfM;
            rig.Tick(keep: true);
        }

        Assert.InRange(frontM, setAtM - 0.01f, setAtM + (SpeedMps * Config.TickSeconds) + 0.01f);
    }

    [Fact]
    public void DrivingATrainAllocatesNothing()
    {
        var rig = new Rig(Straight(TownMap.TrackRuns.Forward));
        rig.Run(1f);
        var before = GC.GetAllocatedBytesForCurrentThread();
        rig.Run(20f);

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
