using System.Numerics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Bench;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A watch over every body kept on the device</b> (<see cref="DeviceTownWatch"/>) asked the watch's own arithmetic
/// (<see cref="WatchRuns"/>): the same steps of the same bodies — some inside something, cars past their grants and going
/// deeper, walkers afoot standing and walking, indoors and down, cars driven — read the same.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceTownWatchTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>More bodies than one workgroup sweeps, so each step's whole is read off several.</summary>
    const int Walkers = 300, Cars = 340, Steps = 16;

    const float TickSeconds = 1f / 60f;

    static readonly TownWatchTerms Terms = new(SoakProbe.OverlapAllowanceM, SoakProbe.PastTheGrantAllowanceM, StuckProbe.MovedM);

    /// <summary>
    /// <b>Every step reads what the watch's own arithmetic reads</b> — the deepest, longest and furthest with the first body
    /// that had each, and the longest any walker got nowhere, exactly; the metres driven to a rounding, summed in another
    /// order — going on from the runs it was handed, and saying what it has against each body noted.
    /// </summary>
    [Fact]
    public void EveryStepReadsWhatTheWatchsArithmeticReads()
    {
        const int bodies = Walkers + Cars;
        var vk = device.Vk;
        var random = new Random(7);

        // Which body row each body is, shuffled, so nothing reads a body at its own number.
        var rows = Enumerable.Range(0, bodies).OrderBy(_ => random.Next()).ToArray();
        using var overlap = device.Sized<uint>(bodies);
        using var pose = device.Sized<Vector4>(bodies);
        using var motion = device.Sized<Vector4>(bodies);
        using var walkerBodies = device.Holding<int>(rows.AsSpan(0, Walkers));
        using var carBodies = device.Holding<DeviceBodyPhase.CarBodyRow>([.. rows[Walkers..].Select(row => new DeviceBodyPhase.CarBodyRow(row, 0u))]);
        using var walkers = device.Sized<DeviceWalkerTick.WalkerRow>(Walkers);
        using var cars = device.Sized<DeviceTownWatch.CarRow>(Cars);
        using var watch = new DeviceTownWatch(
            vk, Terms, TickSeconds, Walkers, Cars, new DeviceTownWatch.Tables(overlap, pose, motion, walkerBodies, carBodies, walkers, cars));
        using var batch = new ComputeBatch(vk);

        var runs = new WatchRuns(Walkers, Cars, Terms);
        var overlapM = new float[bodies];
        var pastM = new float[bodies];
        var atM = new Vector2[Walkers];
        var afoot = new bool[Walkers];
        int[] noted = [3, 17, Walkers + 5, Walkers + 200];
        for (var step = 0; step < Steps; step++)
        {
            var drivenM = 0f;
            for (var body = 0; body < bodies; body++)
            {
                // Few depths, so bodies tie and the first that had one is asked.
                overlapM[body] = Pick(random, 0f, 0f, 0.005f, 0.02f, 0.03f);
                overlap.Span<uint>()[rows[body]] = BitConverter.SingleToUInt32Bits(overlapM[body]);
            }

            for (var walker = 0; walker < Walkers; walker++)
            {
                var flags = Pick(random, Walking, Walking, Walking, Walking | Inside, Walking | Wounded, 0u);
                afoot[walker] = flags == Walking;
                atM[walker] += Pick(random, Vector2.Zero, Vector2.Zero, new Vector2(2f * StuckProbe.MovedM, 0f));
                pose.Span<Vector4>()[rows[walker]] = new Vector4(atM[walker], 0f, 0f);
                walkers.Span<DeviceWalkerTick.WalkerRow>()[walker] = new DeviceWalkerTick.WalkerRow(atM[walker], default, default, default, 0f, 70f, flags, 0f);
            }

            for (var car = 0; car < Cars; car++)
            {
                ref var past = ref pastM[Walkers + car];
                past = Pick(random, 0f, past, past + 0.005f, past + 0.05f, 0.5f);
                var driven = random.Next(3) > 0;
                var velocityMps = new Vector2(random.Next(-15, 16), random.Next(-15, 16));
                motion.Span<Vector4>()[rows[Walkers + car]] = new Vector4(velocityMps, 0f, 0f);
                cars.Span<DeviceTownWatch.CarRow>()[car] = new DeviceTownWatch.CarRow(past, driven ? 1u : 0u);
                if (driven) drivenM += velocityMps.Length() * TickSeconds;
            }

            var expected = runs.Step(overlapM, pastM, atM, afoot, drivenM);

            // The first step is the town's own, and the device goes on from the runs it left.
            if (step == 0)
            {
                watch.Seed(runs.Runs);
                continue;
            }

            watch.Note(noted);
            watch.Record(batch.Begin());
            batch.SubmitAndWait();
            watch.Read();

            Assert.Equal(expected with { DrivenM = 0f }, watch.Watched with { DrivenM = 0f });
            Assert.Equal(expected.DrivenM, watch.Watched.DrivenM, expected.DrivenM * 1e-5f);
            Assert.Equal(noted.Select(runs.NoteOf), watch.Notes.ToArray());
        }
    }

    const uint Walking = DeviceWalkerTick.WalkerRow.Walking;
    const uint Inside = DeviceWalkerTick.WalkerRow.Inside;
    const uint Wounded = DeviceWalkerTick.WalkerRow.Wounded;

    static T Pick<T>(Random random, params T[] from) => from[random.Next(from.Length)];
}
