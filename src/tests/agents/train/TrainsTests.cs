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
/// <b>The trains' controller</b> (TRN-5…12): when a train is sent, where and how it is put down, what its driver asks,
/// and when it is taken out of the world — over a world stood in by kinematics, each vehicle moving on as it was put down.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class TrainsTests
{
    const float MapM = 1000f;

    /// <summary>The map's middle row, which the main track runs along from far west to far east.</summary>
    const float RowM = 500f;

    static float RunOffM => Config.Rail.RunOffM;

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
        public readonly TrainOrders Orders;
        public readonly List<TrainOrder> Given = [];

        public Rig(CityPlan.RailArrays rails, ulong seed = 1)
        {
            Graph = RailGraph.Of(rails, new Vector2(MapM));
            Trains = new Trains(Graph, rails, new Vector2(MapM), TrainCatalog.Shared, Config, agentSeed: seed);
            Orders = new TrainOrders(Trains.MostOrdersATick);
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

        /// <summary>Every vehicle of a train set rolling at a speed along the way it points, back as negative.</summary>
        public void Roll(int train, float alongMps)
        {
            foreach (var wagon in Trains.VehiclesOf(train)) Seen[wagon] = Seen[wagon] with { VelocityMps = Seen[wagon].Forward * alongMps };
        }

        /// <summary>Ticks until a train's front has come to a distance along its leg, the way it is rolling.</summary>
        public void RunUntilFront(int train, float legM)
        {
            var on = Trains.FrontM(train) < legM;
            for (var tick = 0; tick < 100_000 && (Trains.FrontM(train) < legM) == on && Trains.Running(train); tick++) Tick();
        }

        /// <summary>The orders of one tick with a hand on a train.</summary>
        public TrainOrder[] TickHanded(int train, in TrainControls controls)
        {
            Trains.Hand(train, controls);
            Given.Clear();
            Tick(keep: true);
            return [.. Given];
        }
    }

    /// <summary>A main line west to east, and a branch off it at the map's middle turning right to the south-east (TRN-9, TRN-11).</summary>
    static CityPlan.RailArrays Forked() => new()
    {
        Line = [0, 0], Runs = [TownMap.TrackRuns.Forward, TownMap.TrackRuns.Forward], PointOffsets = [0, 2, 4],
        PointM = [new(-RunOffM, RowM), new(MapM + RunOffM, RowM), new(MapM * 0.5f, RowM), new(MapM + RunOffM, RowM + 700f)],
        Level = new byte[4], Ballast = CityPlan.RingArrays.None,
    };

    static TrainVehicle Electric => TrainCatalog.Shared.ShapeOf(VehicleKind.Electric);

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
            (loco.FootprintM.X * 0.5f) + (coach.FootprintM.X * 0.5f), placed[0].AtM.X - placed[1].AtM.X, 3);

        var couples = rig.Given.Where(order => order.Kind == TrainOrderKind.Couple).ToArray();
        Assert.Equal(-1, couples[0].Other);
        for (var vehicle = 1; vehicle < couples.Length; vehicle++) Assert.Equal(couples[vehicle - 1].Wagon, couples[vehicle].Other);
    }

    /// <summary>
    /// The main line west to the map's middle, then a quarter circle at a radius turning south, and on south off the map:
    /// chords a tolerance off the circle, as a track is laid.
    /// </summary>
    static CityPlan.RailArrays Curved(float radiusM)
    {
        var stepRad = MathF.Sqrt(8f * Config.Rail.ChordToleranceM / radiusM);
        var centreM = new Vector2(MapM * 0.5f, RowM + radiusM);
        List<Vector2> pointM = [new(-RunOffM, RowM)];
        for (var turnRad = 0f; turnRad < MathF.PI * 0.5f; turnRad += stepRad)
        {
            pointM.Add(centreM + (new Vector2(MathF.Sin(turnRad), -MathF.Cos(turnRad)) * radiusM));
        }

        pointM.Add(centreM + new Vector2(radiusM, 0f));
        pointM.Add(new Vector2(centreM.X + radiusM, MapM + RunOffM));
        return new CityPlan.RailArrays
        {
            Line = [0], Runs = [TownMap.TrackRuns.Forward], PointOffsets = [0, pointM.Count], PointM = [.. pointM],
            Level = new byte[pointM.Count], Ballast = CityPlan.RingArrays.None,
        };
    }

    /// <summary>
    /// <b>A curve slows the pace a train wants, in time for its brake</b> (TRN-12): its kind's on the straight, dipping
    /// first where its whole brake from its pace still brings it to the curve's speed by the curve — a tick's run either
    /// way — and on the curve no faster than pulls it sideways by its driver's most.
    /// </summary>
    [Fact]
    public void ACurveSlowsThePaceATrainWantsInTimeForItsBrake()
    {
        var radiusM = Config.Rail.LeastRadiusM;
        var rig = new Rig(Curved(radiusM));
        rig.Run(1f);
        Assert.Equal(SpeedMps, rig.Trains.WantedMps(0));

        var curveStartsM = RunOffM + (MapM * 0.5f);
        var curveMps2 = Config.Train.CurveSideAccelerationMps2 * radiusM;
        var brakingMps2 = Config.Train.WheelRailFriction * Config.Tyre.StandardGravityMps2;
        var dippedAtM = float.NaN;
        for (var tick = 0; tick < 100_000 && rig.Trains.FrontM(0) < curveStartsM && float.IsNaN(dippedAtM); tick++)
        {
            rig.Tick();
            if (rig.Trains.WantedMps(0) < SpeedMps) dippedAtM = rig.Trains.FrontM(0);
        }

        var tickM = SpeedMps * Config.TickSeconds;
        var arrivesMps2 = (SpeedMps * SpeedMps) - (2f * brakingMps2 * (curveStartsM - dippedAtM));
        Assert.InRange(arrivesMps2, curveMps2 - (2f * brakingMps2 * tickM), curveMps2 + (2f * brakingMps2 * tickM));

        rig.RunUntilFront(0, curveStartsM + (radiusM * 0.25f));
        var wantedMps = rig.Trains.WantedMps(0);
        Assert.True(wantedMps * wantedMps / radiusM <= Config.Train.CurveSideAccelerationMps2 * 1.001f, $"the curve is wanted at {wantedMps:F2} m/s");
    }

    /// <summary><b>A train whose last vehicle has left the map is taken out of the world whole</b> and uncoupled (TRN-7).</summary>
    [Fact]
    public void ATrainWhoseTailHasLeftTheMapIsTakenOutWhole()
    {
        var rig = new Rig(Straight(TownMap.TrackRuns.Forward));
        rig.Run(1f);
        Assert.Contains(true, rig.InWorld);

        // Its front off the run-in, over the map and a train's length on, and a second more.
        var trainM = Electric.FootprintM.X + (Config.Train.IntercityCoaches * TrainCatalog.Shared.ShapeOf(VehicleKind.Coach).FootprintM.X);
        var runM = (SpeedMps * Config.Rail.WarningS) + MapM + trainM + SpeedMps;
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

    /// <summary><b>A hand's brake is every vehicle's</b> (TRN-10): each one's wheels told to brake on the tick it is asked.</summary>
    [Fact]
    public void AHandsBrakeIsGivenToEveryVehicle()
    {
        var rig = new Rig(Straight(TownMap.TrackRuns.Forward));
        rig.Run(1f);

        var orders = rig.TickHanded(0, new TrainControls(0f, Reverse: false, Brake: true));

        foreach (var wagon in rig.Trains.VehiclesOf(0))
        {
            Assert.Contains(orders, order => order.Kind == TrainOrderKind.Drive && order.Wagon == wagon && order.Command.Handbrake);
        }
    }

    /// <summary>
    /// <b>A hand's pull is its share of the locomotive's tractive effort</b> (TRN-10), asked as an acceleration of the
    /// locomotive's own mass, and back along the leg where it is reversed.
    /// </summary>
    [Fact]
    public void AHandsPullIsItsShareOfTheLocomotivesTractiveEffort()
    {
        var rig = new Rig(Straight(TownMap.TrackRuns.Forward));
        rig.Run(1f);
        var loco = rig.Trains.VehiclesOf(0)[0];

        var orders = rig.TickHanded(0, new TrainControls(0.5f, Reverse: true, Brake: false));

        var drive = Assert.Single(orders, order => order.Kind == TrainOrderKind.Drive && order.Wagon == loco);
        Assert.Equal(0.5f * Electric.TractiveEffortN / Electric.MassKg, drive.Command.ThrottleMps2, 4);
        Assert.True(drive.Command.Reverse);
        Assert.False(drive.Command.Handbrake);
    }

    /// <summary>
    /// <b>A train backing is read back along its leg</b> (TRN-9): its front, rolled back out of one lane into the lane
    /// before it — two tracks laid end to end at the map's middle — stands where its locomotive does.
    /// </summary>
    [Fact]
    public void ATrainBackingIsReadBackAlongItsLegIntoTheLaneBefore()
    {
        var rails = new CityPlan.RailArrays
        {
            Line = [0, 0], Runs = [TownMap.TrackRuns.Forward, TownMap.TrackRuns.Forward], PointOffsets = [0, 2, 4],
            PointM = [new(-RunOffM, RowM), new(MapM * 0.5f, RowM), new(MapM * 0.5f, RowM), new(MapM + RunOffM, RowM)],
            Level = new byte[4], Ballast = CityPlan.RingArrays.None,
        };
        var rig = new Rig(rails);
        rig.Run(1f);
        var jointM = rig.Trains.LegStartM(0, 1);
        rig.RunUntilFront(0, jointM + 50f);
        rig.Roll(0, -SpeedMps);
        rig.RunUntilFront(0, jointM - 50f);

        var loco = rig.Trains.VehiclesOf(0)[0];
        rig.Tick();
        Assert.Equal(rig.Seen[loco].PositionM.X - (SpeedMps * -Config.TickSeconds) + (Electric.FootprintM.X * 0.5f) + RunOffM, rig.Trains.FrontM(0), 2);
    }

    /// <summary><b>A train backed off the map by the way it came is taken out of the world whole</b> (TRN-7).</summary>
    [Fact]
    public void ATrainBackedOffTheMapIsTakenOutWhole()
    {
        var rig = new Rig(Straight(TownMap.TrackRuns.Forward));
        rig.Run(1f);
        rig.RunUntilFront(0, RunOffM + 100f);
        rig.Roll(0, -SpeedMps);

        rig.Run((100f + 50f) / SpeedMps);

        Assert.False(rig.Trains.Running(0));
        Assert.DoesNotContain(true, rig.InWorld);
    }

    /// <summary>
    /// <b>The driver sets a switch it trails through as well as one it faces</b> (TRN-9): to the branch its leg comes in
    /// by, so the train backing over it runs back onto the lane it came off.
    /// </summary>
    [Fact]
    public void TheDriverSetsASwitchItTrailsThroughToTheBranchItComesInBy()
    {
        // The branch runs into the main line from the north-west rather than out of it: the main line's east half is the
        // common end, and both the main line's west half and the branch come into the node by a branch of it.
        var rails = new CityPlan.RailArrays
        {
            Line = [0, 0], Runs = [TownMap.TrackRuns.Forward, TownMap.TrackRuns.Forward], PointOffsets = [0, 2, 4],
            PointM = [new(-RunOffM, RowM), new(MapM + RunOffM, RowM), new(-RunOffM, RowM - 700f), new(MapM * 0.5f, RowM)],
            Level = new byte[4], Ballast = CityPlan.RingArrays.None,
        };
        var rig = new Rig(rails);
        rig.Run(1f);
        var train = rig.Trains.Running(0) ? 0 : 1;
        rig.RunUntilFront(train, rig.Trains.LegStartM(train, 1) - Config.Rail.SwitchSetAheadM - 5f);
        rig.Given.Clear();
        rig.Run(1f, keep: true);

        var set = Assert.Single(rig.Given, order => order.Kind == TrainOrderKind.Switch);
        var node = rig.Graph.NodeOfSwitch[set.Wagon];
        Assert.Equal(rig.Graph.ArrivesBy(rig.Trains.LegOf(train)[0]), rig.Graph.Ends[rig.Graph.EndOffsets[node] + 1 + set.Other]);
    }

    /// <summary>
    /// <b>A switch ahead thrown to the branch beside its own routes the leg down that branch</b> (TRN-11): set at once to
    /// it, and the leg's end the branch's loose end.
    /// </summary>
    [Fact]
    public void AThrownSwitchRoutesTheLegDownTheBranchBesideIt()
    {
        var rig = new Rig(Forked());
        rig.Run(1f);
        rig.RunUntilFront(0, RunOffM + (MapM * 0.5f) - (Config.Rail.SwitchSetAheadM * 0.5f));
        var at = rig.Trains.NextSwitchAhead(0, out _);
        var (_, was, _) = rig.Trains.SwitchOnTheLeg(0, at);
        var endedAt = rig.Graph.NodeAhead(rig.Trains.LegOf(0)[^1]);

        var orders = rig.TickHanded(0, new TrainControls(0f, Reverse: false, Brake: false, Branch: was == 0 ? 1 : -1));

        var (_, now, _) = rig.Trains.SwitchOnTheLeg(0, at);
        Assert.Equal(1 - was, now);
        Assert.Contains(orders, order => order.Kind == TrainOrderKind.Switch && order.Other == now);
        var endsAt = rig.Graph.NodeAhead(rig.Trains.LegOf(0)[^1]);
        Assert.NotEqual(endedAt, endsAt);
        Assert.True(rig.Graph.IsPortal[endsAt]);
    }

    /// <summary>
    /// <b>A throw onto ground another train's leg holds is refused</b> (TRN-11, TRN-5): the leg kept and the switch left
    /// as it is. A second track joins the branch further on, and its train is out on it while the first runs the main line.
    /// </summary>
    [Fact]
    public void AThrowOntoGroundAnotherLegHoldsIsRefused()
    {
        var rails = new CityPlan.RailArrays
        {
            Line = [0, 0, 0], Runs = [TownMap.TrackRuns.Forward, TownMap.TrackRuns.Forward, TownMap.TrackRuns.Forward],
            PointOffsets = [0, 2, 4, 6],
            PointM =
            [
                new(-RunOffM, RowM), new(MapM + RunOffM, RowM), new(MapM * 0.5f, RowM), new(MapM + RunOffM, RowM + 700f),
                new(-RunOffM, RowM + 700f), new(((MapM * 1.5f) + RunOffM) * 0.5f, RowM + 350f),
            ],
            Level = new byte[6], Ballast = CityPlan.RingArrays.None,
        };

        // Staged at the first seed that sends both trains at once with the main line's train running east: which leg a
        // train draws is its seed's (TRN-6).
        for (ulong seed = 1; seed < 64; seed++)
        {
            var rig = new Rig(rails, seed);
            rig.Run(1f);
            if (!rig.Trains.Running(0) || !rig.Trains.Running(1)) continue;

            var main = rig.Graph.NodeM[rig.Graph.NodeBehind(rig.Trains.LegOf(0)[0])].Y == RowM ? 0 : 1;

            rig.RunUntilFront(main, RunOffM + (MapM * 0.5f) - (Config.Rail.SwitchSetAheadM * 0.5f));
            var at = rig.Trains.NextSwitchAhead(main, out _);
            if (at < 0) continue;

            var leg = rig.Trains.LegOf(main).ToArray();
            var (_, was, _) = rig.Trains.SwitchOnTheLeg(main, at);
            var orders = rig.TickHanded(main, new TrainControls(0f, Reverse: false, Brake: false, Branch: was == 0 ? 1 : -1));

            Assert.Equal(leg, rig.Trains.LegOf(main).ToArray());
            Assert.Equal(was, rig.Trains.SwitchOnTheLeg(main, at).Position);
            Assert.DoesNotContain(orders, order => order.Kind == TrainOrderKind.Switch);
            return;
        }

        Assert.Fail("no seed sent both trains out at once");
    }

    /// <summary>
    /// <b>A train standing under a hand closes a crossing it could be pulled onto in the warning time</b> (RAIL-1): the
    /// whole of its traction, from rest, and not a metre further.
    /// </summary>
    [Fact]
    public void AStandingTrainClosesACrossingItCouldBePulledOntoInTheWarning()
    {
        const float mostMps2 = 0.5f;
        var run = new TrackRun(0, FrontM: 100f, LengthM: 150f, Forward: true, SpeedMps: 0f, MostMps2: mostMps2);
        var reachM = 0.5f * mostMps2 * Config.Rail.WarningS * Config.Rail.WarningS;

        Assert.True(run.Closes(100f + reachM + 2f - 0.01f, reachM: 2f, Config.Rail, laterS: 0f));
        Assert.False(run.Closes(100f + reachM + 2f + 0.01f, reachM: 2f, Config.Rail, laterS: 0f));
    }

    /// <summary><b>A train backing closes a crossing a warning behind its tail</b> (RAIL-1): its tail is the end that gets there.</summary>
    [Fact]
    public void ABackingTrainClosesACrossingAWarningBehindItsTail()
    {
        var run = new TrackRun(0, FrontM: 500f, LengthM: 150f, Forward: true, SpeedMps: -20f);
        var warnedM = 500f - 150f - 2f - (20f * Config.Rail.WarningS);

        Assert.True(run.Closes(warnedM + 0.01f, reachM: 2f, Config.Rail, laterS: 0f));
        Assert.False(run.Closes(warnedM - 0.01f, reachM: 2f, Config.Rail, laterS: 0f));
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
