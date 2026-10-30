using System.Numerics;
using TrafficSimulation.Multiplayer.Wire;
using Xunit;

namespace TrafficSimulation.Tests.Multiplayer.Wire;

/// <summary>
/// <b>A snapshot is a difference from a baseline the player holds</b>, and read against that baseline it gives back
/// exactly the area it was written from — and a message that is not one gives back nothing (MP-10).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class SnapshotCodecTests
{
    static readonly WireShape Shape = new(
        positionQuantumM: 0.01f, angleQuantumDeg: 0.1f, smallAngleQuantumDeg: 0.5f, worldSizeM: new Vector2(800f, 600f),
        movers: new MoverIds(Cars: 100, People: 200, Wagons: 10), smallMoveM: 10f);

    static MoverState Car(int id, int x, int y, int heading = 0, byte lamps = 0, byte driver = 0, short steer = -40) =>
        new()
        {
            Id = id, X = x, Y = y, Heading = (ushort)heading, Lamps = lamps, Driver = driver, Look = (ushort)(id + 7), Steer = steer,
            TiltAlong = 3,
        };

    static MoverState[] Read(ReadOnlySpan<byte> message, ReadOnlySpan<MoverState> baseline)
    {
        var into = new MoverState[64];
        var r = new BitReader(message);
        var count = SnapshotCodec.Read(ref r, Shape, baseline, into, new MoverState[64], new int[64]);
        return count < 0 || !r.Finished ? [] : into[..count];
    }

    static byte[] Write(ReadOnlySpan<MoverState> now, ReadOnlySpan<MoverState> baseline)
    {
        var buffer = new byte[4096];
        var w = new BitWriter(buffer);
        SnapshotCodec.Write(ref w, Shape, now, baseline);
        return buffer[..w.Finish()];
    }

    [Fact]
    public void AWholeAreaReadsBackAsWritten()
    {
        MoverState[] now = [Car(3, 100, 200, 17, 5), Car(40, 79_999, 59_999), Car(250, 0, 0)];

        Assert.Equal(now, Read(Write(now, []), []));
    }

    [Fact]
    public void LeftChangedAndEnteredLaidOverTheBaselineGiveTheAreaNow()
    {
        MoverState[] baseline = [Car(1, 500, 500), Car(2, 600, 600), Car(9, 700, 700), Car(30, 800, 800)];
        MoverState[] now =
        [
            Car(1, 500, 500, driver: 3), Car(2, 650, 610, heading: 90), Car(12, 1_000, 2_000), Car(30, 70_000, 800, lamps: 2),
        ];

        Assert.Equal(now, Read(Write(now, baseline), baseline));
    }

    /// <summary>A move of every width the codec has — tiny, short, to the margin, and past it as a place — reads back to where the mover stands.</summary>
    [Theory]
    [InlineData(5, -31)]
    [InlineData(200, -255)]
    [InlineData(-700, 999)]
    [InlineData(40_000, 3)]
    public void AMoveOfAnyWidthReadsBackToThePlace(int dx, int dy)
    {
        MoverState[] baseline = [Car(4, 30_000, 30_000)];
        MoverState[] now = [Car(4, 30_000 + dx, 30_000 + dy)];

        Assert.Equal(now, Read(Write(now, baseline), baseline));
    }

    [Fact]
    public void AHeadingTurnedThroughNorthReadsBackTheShortWayRound()
    {
        MoverState[] baseline = [Car(4, 100, 100, heading: Shape.HeadingSteps - 3)];
        MoverState[] now = [Car(4, 100, 100, heading: 2)];

        Assert.Equal(now, Read(Write(now, baseline), baseline));
    }

    [Theory]
    [InlineData(-38)]
    [InlineData(60)]
    public void ARoadWheelNudgedOrSwungReadsBackToItsAngle(short steer)
    {
        MoverState[] baseline = [Car(4, 100, 100)];
        MoverState[] now = [Car(4, 100, 100, steer: steer)];

        Assert.Equal(now, Read(Write(now, baseline), baseline));
    }

    [Fact]
    public void AnAreaUnchangedSinceItsBaselineCostsOnlyItsTwoCounts()
    {
        MoverState[] same = [Car(1, 500, 500), Car(2, 600, 600), Car(99, 700, 700)];

        Assert.Equal((2 * Shape.IdBits + 7) / 8, Write(same, same).Length);
    }

    [Fact]
    public void ATruncatedSnapshotIsNotOne()
    {
        MoverState[] now = [Car(3, 100, 200), Car(40, 7_000, 9_000)];
        var message = Write(now, []);

        Assert.Empty(Read(message[..^2], []));
    }

    [Fact]
    public void ANumberPastTheLastMoverIsNotOne()
    {
        var buffer = new byte[64];
        var w = new BitWriter(buffer);
        w.Write(0, Shape.IdBits);
        w.Write(1, Shape.IdBits);
        w.Write(3, 2);
        w.Write((uint)Shape.Movers.Count, Shape.IdBits);
        w.Write(0, 6);
        w.Write(0, 16);

        Assert.Empty(Read(buffer[..w.Finish()], []));
    }

    [Fact]
    public void AMoverSaidToHaveLeftThatTheBaselineNeverHeldIsNotOne()
    {
        MoverState[] baseline = [Car(1, 500, 500)];
        var message = Write([], [Car(5, 1, 1)]);

        Assert.Empty(Read(message, baseline));
    }
}
