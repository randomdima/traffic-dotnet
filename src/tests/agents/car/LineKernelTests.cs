using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>The line read on the device</b> (<c>agents/car/control/kernels/line.glsl</c>): what <see cref="LineReader"/> makes of
/// a car on its line — where it is, the lane it has left shifted off the line, how far off the line it stands and
/// whether the line has run short — asked of the kernel over the fleet's own line layout on the fixture town.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class LineKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const int Count = 2000;

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct PoseRow(Vector2 PositionM, Vector2 Forward);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CarLineRow(uint ArcCount, uint LaneCount, float LengthM, uint FinalApproach)
    {
        public static CarLineRow Of(CarFleet cars, int car) => new(
            (uint)cars.Line[car].ArcCount, (uint)cars.Line[car].LaneCount, cars.Line[car].LengthM,
            cars.StopsForBayOf(car) != CarFleet.NoBay || cars.StopsAtItsPlaceOf(car) ? 1u : 0u);
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Figures(uint CarCount, uint ArcsPerCar, uint MostLanes, float RoundingM);

    /// <summary>
    /// <b>Two thousand cars on the fixture town's lines are read as the host reads them</b>: the same ones pass into
    /// their next lane and leave the same line behind them, arc for arc and metre for metre, and the same ones find
    /// their line short — where they are along it and how far off it, to the bit.
    /// </summary>
    [Fact]
    public void TheLinesAreReadAsTheHostReadsThem()
    {
        var graph = RoadGraph.Build(Towns.Of(Towns.Fixture), Config);
        var builds = CarBuilds.OfTheFleet(Config, CarCatalog.Shared);
        var arcsPerCar = LineAssembler.ArcsFor(graph);
        var cars = new CarFleet(Count, arcsPerCar, builds);
        var rearAxles = new Vector2[Count];
        var forwards = new Vector2[Count];
        var runsOn = new uint[Count];
        Stage(graph, cars, rearAxles, forwards, runsOn, new Random(7));

        var mostLanes = LineAssembler.MostLanes;
        var arcs = new ArcRow[Count * arcsPerCar];
        var entries = new float[Count * arcsPerCar];
        for (var car = 0; car < Count; car++)
        {
            for (var arc = 0; arc < cars.Line[car].ArcCount; arc++)
            {
                arcs[(car * arcsPerCar) + arc] = ArcRow.Of(cars.LineOf(car)[arc]);
                entries[(car * arcsPerCar) + arc] = cars.EntriesOf(car)[arc];
            }
        }

        using var buildBuffer = device.Holding<CarBuildRow>(
            Enumerable.Range(0, CarCatalog.Shared.SheetCount).Select(variant => CarBuildRow.Of(builds.Of(variant))).ToArray());
        using var buildOf = device.Holding<uint>(cars.Variant.Take(Count).Select(variant => (uint)variant).ToArray());
        using var poses = device.Holding<PoseRow>(rearAxles.Select((_, car) => new PoseRow(cars.PositionM[car], forwards[car])).ToArray());
        using var lasts = device.Holding<float>(cars.ProgressM.AsSpan(0, Count));
        using var roadRunsOn = device.Holding<uint>(runsOn);
        using var lineArcs = device.Holding<ArcRow>(arcs);
        using var entryBuffer = device.Holding<float>(entries);
        using var chain = device.Holding<int>(Slots(cars, car => cars.ChainOf(car)));
        using var starts = device.Holding<float>(Slots(cars, car => cars.LaneStartsOf(car)));
        using var ends = device.Holding<float>(Slots(cars, car => cars.LaneEndsOf(car)));
        using var firstArcs = device.Holding<uint>(Slots(cars, car => cars.LaneFirstArcsOf(car)).Select(arc => (uint)arc).ToArray());
        using var breaks = device.Holding<uint>(Slots(cars, car => cars.JoinBreaksOf(car)).Select(join => join ? 1u : 0u).ToArray());
        using var lines = device.Holding<CarLineRow>(Enumerable.Range(0, Count).Select(car => CarLineRow.Of(cars, car)).ToArray());
        using var readings = device.Sized<Vector4>(Count);
        using var runsShort = device.Sized<uint>(Count);

        GpuBuffer[] buffers =
            [buildBuffer, buildOf, poses, lasts, roadRunsOn, lineArcs, entryBuffer, chain, starts, ends, firstArcs, breaks, lines, readings, runsShort];
        using var kernel = new ComputeKernel(device.Vk, "read_line.comp", buffers.Length, (uint)Marshal.SizeOf<Figures>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        kernel.Dispatch(batch.Begin(), set, (Count + 63) / 64, new Figures(Count, (uint)arcsPerCar, (uint)mostLanes, LineTolerance.RoundingM));
        batch.SubmitAndWait();

        var differ = new List<string>();
        for (var car = 0; car < Count; car++)
        {
            // Off the pose, as the device and the town's own tick have it: the staged rear axle is a last bit from it.
            var rearAxleM = CarFollower.RearAxleM(cars.BuildOf(car), cars.PositionM[car], forwards[car]);
            var advance = LineReader.Advance(cars, car, rearAxleM);
            var offLineM = CarFollower.OffLineM(cars.LineOf(car), rearAxleM, advance.ProgressM);
            var reading = readings.Span<Vector4>()[car];
            var line = lines.Span<CarLineRow>()[car];
            var slots = car * mostLanes;
            var laid = car * arcsPerCar;
            var same =
                Same(advance.ProgressM, reading.X) && Same(advance.CoveredM, reading.Y) && advance.ShiftM == reading.Z
                && Same(offLineM, reading.W) && line == CarLineRow.Of(cars, car)
                && LineReader.RunsShort(cars, car, advance.ProgressM, runsOn[car] != 0u) == (runsShort.Span<uint>()[car] != 0u);
            for (var slot = 0; same && slot < cars.Line[car].LaneCount; slot++)
            {
                same = chain.Span<int>()[slots + slot] == cars.ChainOf(car)[slot]
                       && starts.Span<float>()[slots + slot] == cars.LaneStartsOf(car)[slot]
                       && ends.Span<float>()[slots + slot] == cars.LaneEndsOf(car)[slot]
                       && firstArcs.Span<uint>()[slots + slot] == (uint)cars.LaneFirstArcsOf(car)[slot]
                       && (slot + 1 >= cars.Line[car].LaneCount || breaks.Span<uint>()[slots + slot] == (cars.JoinBreaksOf(car)[slot] ? 1u : 0u));
            }

            for (var arc = 0; same && arc < cars.Line[car].ArcCount; arc++)
            {
                same = lineArcs.Span<ArcRow>()[laid + arc] == ArcRow.Of(cars.LineOf(car)[arc])
                       && entryBuffer.Span<float>()[laid + arc] == cars.EntriesOf(car)[arc];
            }

            if (!same) differ.Add($"car {car}: host {advance} off {offLineM}, device {reading} {line}");
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {Count} read differently: {string.Join("; ", differ.Take(5))}");
    }

    /// <summary>
    /// Every car's line laid over a run of the fixture town's lanes as the town lays one, and its rear axle placed beside
    /// it a little ahead of where it last was — on and off the line, across into its next lane and short of it.
    /// </summary>
    static void Stage(RoadGraph graph, CarFleet cars, Vector2[] rearAxles, Vector2[] forwards, uint[] runsOn, Random random)
    {
        float Between(float least, float most) => least + (random.NextSingle() * (most - least));
        Span<int> lanes = stackalloc int[5];
        while (cars.Count < Count)
        {
            lanes[0] = random.Next(graph.LaneCount);
            var count = 1;
            var wanted = 1 + random.Next(lanes.Length);
            while (count < wanted && graph.LanesFrom(lanes[count - 1]).Length > 0)
            {
                var onward = graph.LanesFrom(lanes[count - 1]);
                lanes[count++] = onward[random.Next(onward.Length)];
            }

            var car = cars.Add(default, Vector2.Zero, 0f, (byte)random.Next(CarCatalog.Shared.SheetCount), false, new Rng(1, (ulong)cars.Count));
            var line = LineAssembler.Assemble(
                graph, lanes[..count], cars.LineArcsOf(car), cars.LaneStartsOf(car), cars.LaneEndsOf(car),
                random.Next(4) == 0 ? Between(0f, graph.LaneLengthM[lanes[count - 1]]) : float.PositiveInfinity,
                cars.LaneFirstArcsOf(car));
            cars.SetLine(car, line);
            lanes[..line.LaneCount].CopyTo(cars.ChainOf(car));
            CornerLimits.Lay(cars.LineOf(car), cars.LineEntriesOf(car), Config);
            for (var slot = 0; slot + 1 < line.LaneCount; slot++)
            {
                cars.JoinBreaksOf(car)[slot] = graph.BreaksTheLine(graph.ConnectorBetween(lanes[slot], lanes[slot + 1]));
            }

            cars.StopsForBay[car] = random.Next(6) == 0 ? random.Next(100) : CarFleet.NoBay;
            cars.StopsAtItsPlace[car] = random.Next(6) == 0;
            runsOn[car] = random.Next(4) == 0 ? 0u : 1u;

            ref readonly var build = ref cars.BuildOf(car);
            var atM = Between(0f, line.LengthM);
            if (line.ArcCount == 0)
            {
                cars.SetLine(car, default);
                continue;
            }

            var on = Spline.SampleAt(cars.LineOf(car), atM);
            var asideM = random.Next(8) == 0 ? Between(-8f, 8f) : Between(-1.5f, 1.5f);
            rearAxles[car] = on.PositionM + (on.Right * asideM);
            forwards[car] = Heading.Unit(on.HeadingRad + Between(-0.3f, 0.3f));
            cars.PositionM[car] = rearAxles[car] + (forwards[car] * build.CentreAheadOfAxleM);
            cars.ProgressM[car] = MathF.Max(0f, atM - Between(0f, 1.5f));
        }
    }

    static T[] Slots<T>(CarFleet cars, Func<int, Span<T>> of)
    {
        var all = new T[Count * LineAssembler.MostLanes];
        for (var car = 0; car < Count; car++) of(car).CopyTo(all.AsSpan(car * LineAssembler.MostLanes));
        return all;
    }

    /// <summary>The same float to the bit.</summary>
    static bool Same(float host, float onDevice) => BitConverter.SingleToInt32Bits(host) == BitConverter.SingleToInt32Bits(onDevice);
}
