using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Multiplayer.Client;
using TrafficSimulation.Multiplayer.Wire;
using Xunit;

namespace TrafficSimulation.Tests.Multiplayer.Client;

/// <summary>
/// MP-7: <b>a player draws each mover between the two snapshots either side of the instant drawn</b>, and past the
/// newest carries it on for no longer than it may.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class ReplicaTests
{
    static readonly Welcome Town = new()
    {
        Map = "Test",
        PlanDigest = 1,
        TickRateHz = 60,
        TicksPerSnapshot = 3,
        ServerTick = 0,
        WorldSizeM = new Vector2(500f, 500f),
        Movers = new MoverIds(Cars: 4, People: 2, Wagons: 0),
        PositionQuantumM = 0.01f,
        AngleQuantumDeg = 0.1f,
        SmallAngleQuantumDeg = 0.5f,
        SmallMoveM = 8f,
        WidestViewM = 160f,
        InterpolationDelayS = 0.15f,
        LongestExtrapolationS = 0.25f,
    };

    static readonly WireShape Shape = Town.Shape;

    static MoverState Car(int car, float xM) => new()
    {
        Id = Town.Movers.OfCar(car), X = Shape.ToPosition(xM), Y = Shape.ToPosition(100f), Look = 1,
    };

    static byte[] Snapshot(uint sequence, uint tick, params MoverState[] movers)
    {
        var buffer = new byte[1024];
        var w = new BitWriter(buffer);
        new SnapshotHeader(sequence, Baseline: 0, tick, InputApplied: 0, OwnMover: 0, SeatState.Playing).Write(ref w, Shape);
        w.Write(false);
        SnapshotCodec.Write(ref w, Shape, movers, []);
        return buffer[..w.Finish()];
    }

    static Replica Holding(params byte[][] snapshots)
    {
        var replica = new Replica(Town, SimConfig.Shipped());
        foreach (var snapshot in snapshots) Assert.True(replica.Take(snapshot, out _));
        return replica;
    }

    [Fact]
    public void AMoverIsDrawnBetweenTheSnapshotsEitherSideOfTheInstant()
    {
        var replica = Holding(Snapshot(1, 30, Car(0, 100f)), Snapshot(2, 33, Car(0, 103f)));

        replica.Pose(31.5);

        Assert.Equal(101.5f, replica.Cars.PositionM[0].X, 0.01f);
    }

    [Fact]
    public void PastTheNewestAMoverIsCarriedOnNoLongerThanItMayBe()
    {
        var replica = Holding(Snapshot(1, 30, Car(0, 100f)), Snapshot(2, 33, Car(0, 103f)));

        replica.Pose(33 + (10 * Town.TickRateHz));

        // A metre a tick, carried on for the longest extrapolation and no further.
        Assert.Equal(103f + (Town.LongestExtrapolationS * Town.TickRateHz), replica.Cars.PositionM[0].X, 0.01f);
    }

    /// <summary>
    /// <b>A car at a steady speed is drawn at a steady speed</b> though its snapshots arrive up to 30 ms late, unevenly:
    /// each frame of a 60 Hz page moves it the same distance to within a centimetre.
    /// </summary>
    [Fact]
    public void ASteadyCarIsDrawnSteadilyThoughItsSnapshotsArriveUnevenly()
    {
        const double speedMps = 12d;
        const double frameS = 1d / 60d;
        var replica = new Replica(Town, SimConfig.Shipped());
        var clock = new ServerClock(32, Town.InterpolationDelayS, SimConfig.Shipped().Multiplayer.PlayoutSlewShare);
        var lateS = new[] { 0.004, 0.027, 0.011, 0.030, 0.002, 0.019, 0.008, 0.024 };
        var steps = new List<float>();
        uint sent = 0;
        var lastM = float.NaN;
        for (var frame = 0; frame < 600; frame++)
        {
            var localS = 100d + frame * frameS;
            while (true)
            {
                var tick = (sent + 1) * (uint)Town.TicksPerSnapshot;
                var arrivesS = 100d + tick / (double)Town.TickRateHz + lateS[sent % lateS.Length];
                if (arrivesS > localS) break;

                sent++;
                Assert.True(replica.Take(Snapshot(sent, tick, Car(0, (float)(speedMps * tick / Town.TickRateHz))), out _));
                clock.Arrived(tick / (double)Town.TickRateHz, arrivesS);
            }

            var drawnS = clock.Drawn(localS);
            if (double.IsNaN(drawnS)) continue;

            replica.Pose(drawnS * Town.TickRateHz);
            var atM = replica.Cars.PositionM[0].X;
            if (frame > 120 && !float.IsNaN(lastM)) steps.Add(atM - lastM);
            lastM = atM;
        }

        var expectedM = (float)(speedMps * frameS);
        Assert.All(steps, stepM => Assert.InRange(stepM, expectedM - 0.01f, expectedM + 0.01f));
    }

    [Fact]
    public void AMoverTheNewerSnapshotDoesNotHoldIsNotDrawn()
    {
        var replica = Holding(Snapshot(1, 30, Car(0, 100f), Car(2, 200f)), Snapshot(2, 33, Car(0, 103f)));

        replica.Pose(32);

        Assert.Equal([0], replica.CarsDrawn.ToArray());
    }
}
