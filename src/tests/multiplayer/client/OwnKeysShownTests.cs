using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Multiplayer.Client;
using TrafficSimulation.Multiplayer.Wire;
using Xunit;

namespace TrafficSimulation.Tests.Multiplayer.Client;

/// <summary>
/// MP-7: <b>a player's own keys are shown on what it is at once</b> — its car's wheels and pedal lamps, the way its body
/// faces — by the server's own hand, over where the server says it stands.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class OwnKeysShownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Welcome Town = new()
    {
        Map = "Test",
        PlanDigest = 1,
        TickRateHz = Config.Sim.TickRateHz,
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
        Seats = 16,
    };

    static readonly WireShape Shape = Town.Shape;

    const double TickS = 1d / 60d;

    /// <summary>A car going east at a metre a tick, or a body standing facing east, whose lamps are lit.</summary>
    static MoverState Mover(int id, uint tick) => new()
    {
        Id = id, X = Shape.ToPosition(100f + tick), Y = Shape.ToPosition(100f), Look = 1, Lamps = (byte)CarLampSet.Head,
    };

    static Replica Holding(int own)
    {
        var replica = new Replica(Town, Config);
        for (uint sequence = 1; sequence <= 2; sequence++)
        {
            var tick = 27 + (3 * sequence);
            var buffer = new byte[1024];
            var w = new BitWriter(buffer);
            new SnapshotHeader(sequence, Baseline: 0, tick, InputApplied: 0, own, SeatState.Playing).Write(ref w, Shape);
            w.Write(false);
            SnapshotCodec.Write(ref w, Shape, new[] { Mover(own, tick) }, []);
            Assert.True(replica.Take(buffer[..w.Finish()], out _));
        }

        return replica;
    }

    /// <summary>
    /// The keys held from the tick <paramref name="from"/> for <paramref name="ticks"/> of the hand, a frame a tick, the
    /// area posed afresh each frame as a page poses it; the tick held to.
    /// </summary>
    static int Hold(Replica replica, OwnKeysShown shown, InputMessage keys, int ticks, int from = 0)
    {
        for (var frame = from; frame <= from + ticks; frame++)
        {
            replica.Pose(31.5);
            // Half a tick past each tick after the first frame, so no frame stands on a tick's boundary to the last bit.
            shown.Show(replica, keys, frame == 0 ? 0d : (frame + 0.5) * TickS, longestReplayS: 1d);
        }

        return from + ticks;
    }

    [Fact]
    public void TheWheelKeyWindsTheOwnCarsWheelsAtItsBuildsRateFromTheFrameItIsHeld()
    {
        var replica = Holding(Town.Movers.OfCar(0));
        var shown = new OwnKeysShown(Config);

        Hold(replica, shown, new InputMessage(0, 0, Throttle: 0f, Steer: 1f, Handbrake: false, Acts: 0), ticks: 5);

        var build = replica.Cars.BuildOf(0);
        Assert.Equal(MathF.Min(build.MaxSteerRad, build.SteerRateRadPerS * 5 * Config.TickSeconds), replica.Cars.Command[0].SteerRad, 1e-5f);
    }

    [Fact]
    public void TheBrakeKeyLightsTheOwnCarsBrakeLampsBeforeAnySnapshotSaysSo()
    {
        var replica = Holding(Town.Movers.OfCar(0));
        var shown = new OwnKeysShown(Config);

        Hold(replica, shown, new InputMessage(0, 0, Throttle: -1f, Steer: 0f, Handbrake: false, Acts: 0), ticks: 30);

        Assert.Equal(CarLampSet.Head | CarLampSet.Brake, replica.Cars.Lamps[0]);
    }

    [Fact]
    public void ABodyFacesItsKeysAtItsTurnRateWhileOneIsHeldAndTheServersWayOnceLetGo()
    {
        var replica = Holding(Town.Movers.OfPerson(0));
        var shown = new OwnKeysShown(Config);
        var mostRad = Config.PersonTurnRateDegPerS * MathF.PI / 180f * Config.TickSeconds;

        var heldTo = Hold(replica, shown, new InputMessage(0, 0, Throttle: 1f, Steer: 0f, Handbrake: false, Acts: 0), ticks: 2);
        var heldRad = replica.People.HeadingRad[0];
        Hold(replica, shown, default, ticks: 1, from: heldTo + 1);

        Assert.Equal((-MathF.Min(MathF.PI / 2f, 2 * mostRad), 0f), (heldRad, replica.People.HeadingRad[0]));
    }
}
