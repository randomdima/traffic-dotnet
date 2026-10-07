using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.CityGen.Traced;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;
using static TrafficSimulation.App.DeviceTick.DeviceAtlas;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>Which ways a body stands over, asked on the device</b> (<c>world/road/kernels/atlas.glsl</c>): the same ways, in
/// the same order, over the same stretches as
/// <see cref="RibbonAtlas.UnderBox(Vector2, Vector2, float, float, Span{WayCover}, byte)"/> and
/// <see cref="RibbonAtlas.UnderDisc(Vector2, float, Span{WayCover}, byte)"/> find off the same atlas — compared exactly,
/// since nothing the read does is a transcendental: a box is handed the host's own forward unit vector.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class AtlasKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly byte Ground = CityPlan.RoadArrays.GroundChannel;

    static readonly byte Over = CityPlan.RoadArrays.ChannelOf(CityPlan.RoadArrays.Over);

    /// <summary>The channels a body is asked on: the ground's, the deck's, or both at a bridgehead.</summary>
    static readonly byte[] Channels = [Ground, Over, (byte)(Ground | Over)];

    /// <summary>Where a body is stood against the town's ways.</summary>
    enum Stand
    {
        /// <summary>Over a lane, a join or a pavement, anywhere along it and either side of its band.</summary>
        OnAWay,

        /// <summary>Over the paint of a zebra, where a walk crosses the traffic.</summary>
        OnACrossing,

        /// <summary>Over either end of a way, where it meets the next at a junction or a corner.</summary>
        AtAnEnd,

        /// <summary>Anywhere over the town and a margin round it — mostly on nothing.</summary>
        Anywhere,

        /// <summary>
        /// On a lattice point, square to the lattice and a whole number of steps across, so every edge lies on a row or
        /// a column of points and a horizontal edge takes the host's own branch for one.
        /// </summary>
        OnTheLattice,
    }

    /// <summary>
    /// A couple of thousand bodies over a town: boxes car-sized and some a lorry's, at every heading, or discs a walker's
    /// size and a little either side — on, off and across its ways, its junctions and its crossings, on every channel.
    /// </summary>
    static BodyRow[] Staged(TownWorld world, int count, int seed, bool discs)
    {
        var random = new Random(seed);
        float Between(float least, float most) => least + (random.NextSingle() * (most - least));

        var lines = new WayLines(world.Roads, world.Walking, world.Ways, world.CrossingEdges, Config);
        var crossings = Enumerable.Range(0, lines.WayCount).Where(lines.IsTheCrossing).ToArray();
        var level = world.Atlas.Table.Level;
        var stepM = level.CellM;
        var sizeM = world.Plan.WorldSizeM;
        var bodies = new BodyRow[count];
        for (var at = 0; at < count; at++)
        {
            var stand = (Stand)random.Next(5);
            if (stand == Stand.OnACrossing && crossings.Length == 0) stand = Stand.OnAWay;

            Vector2 centreM;
            var headingRad = Between(-MathF.PI, MathF.PI);
            if (stand is Stand.Anywhere or Stand.OnTheLattice)
            {
                centreM = new Vector2(Between(-0.05f, 1.05f) * sizeM.X, Between(-0.05f, 1.05f) * sizeM.Y);
            }
            else
            {
                var way = stand == Stand.OnACrossing ? crossings[random.Next(crossings.Length)] : random.Next(lines.WayCount);
                var line = lines.LineOf(way, out var widthM);
                var lengthM = Spline.TotalLengthM(line);
                var alongM = stand == Stand.AtAnEnd
                    ? (random.Next(2) == 0 ? 0f : lengthM) + Between(-2f, 2f)
                    : Between(0f, lengthM);
                var sample = Spline.SampleAt(line, alongM);
                var acrossM = Between(-1f, 1f) * ((widthM * 0.5f) + 1.5f);
                centreM = sample.PositionM + (Heading.RightOf(Heading.Unit(sample.HeadingRad)) * acrossM);
                if (random.Next(2) == 0) headingRad = sample.HeadingRad + Between(-0.3f, 0.3f);
            }

            var forward = Heading.Unit(headingRad);
            var halfLengthM = random.Next(8) == 0 ? Between(4f, 7f) : Between(1.8f, 2.6f);
            var halfWidthM = Between(0.8f, 1.3f);
            var radiusM = Between(0.15f, 0.6f);
            if (stand == Stand.OnTheLattice)
            {
                centreM = level.MiddleM(level.CellOf(centreM.X), level.CellOf(centreM.Y));
                forward = random.Next(4) switch { 0 => Vector2.UnitX, 1 => Vector2.UnitY, 2 => -Vector2.UnitX, _ => -Vector2.UnitY };
                halfLengthM = stepM * random.Next(3, 6);
                halfWidthM = stepM * random.Next(1, 3);
                radiusM = stepM * random.Next(1, 3);
            }

            var channels = Channels[random.Next(Channels.Length)];
            bodies[at] = discs
                ? BodyRow.Disc(centreM, radiusM, channels)
                : BodyRow.Box(centreM, forward, halfLengthM, halfWidthM, channels);
        }

        return bodies;
    }

    /// <summary>What the device finds under each body: how many ways and how many dropped, and the ways in rows of the room.</summary>
    (CountRow[] Counts, WayCover[] Covers) OnTheDevice(RibbonAtlas atlas, BodyRow[] bodies)
    {
        using var deviceAtlas = new DeviceAtlas(device.Vk, atlas.Table);
        using var asked = device.Holding<BodyRow>(bodies);
        using var counts = device.Sized<CountRow>(bodies.Length);
        using var covers = device.Sized<WayCover>(bodies.Length * RibbonAtlas.MostWaysUnderABody);
        GpuBuffer[] buffers = [asked, counts, covers, .. deviceAtlas.Tables];
        using var kernel = new ComputeKernel(device.Vk, "atlas_under.comp", buffers.Length, (uint)Marshal.SizeOf<AskPush>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var recording = batch.Begin();
        kernel.Dispatch(recording, set, (uint)((bodies.Length + 63) / 64), deviceAtlas.PushFor(bodies.Length));
        batch.SubmitAndWait();

        return (counts.Span<CountRow>().ToArray(), covers.Span<WayCover>().ToArray());
    }

    /// <summary>
    /// Every body held to the host's uncached read: the same ways in the same order with the same stretches, and as many
    /// dropped. Returns how many bodies were found over more than one way, which is what says the stage reached ground
    /// where ways meet.
    /// </summary>
    int AssertTheSame(RibbonAtlas atlas, BodyRow[] bodies)
    {
        var (counts, covers) = OnTheDevice(atlas, bodies);

        Span<WayCover> host = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        var differ = new List<string>();
        var overMore = 0;
        for (var at = 0; at < bodies.Length; at++)
        {
            var body = bodies[at];
            var droppedBefore = atlas.Dropped;
            var count = body.IsADisc != 0
                ? atlas.UnderDisc(body.CentreM, body.HalfLengthM, host, (byte)body.Channels)
                : atlas.UnderBox(body.CentreM, body.Forward, body.HalfLengthM, body.HalfWidthM, host, (byte)body.Channels);
            var dropped = atlas.Dropped - droppedBefore;
            var onDevice = covers.AsSpan(at * RibbonAtlas.MostWaysUnderABody, (int)Math.Min(counts[at].Count, (uint)RibbonAtlas.MostWaysUnderABody));
            if (counts[at].Count != count || counts[at].Dropped != dropped || !onDevice.SequenceEqual(host[..count]))
            {
                differ.Add(
                    $"{body}: host {count} (+{dropped}) [{string.Join(", ", host[..count].ToArray())}], " +
                    $"device {counts[at].Count} (+{counts[at].Dropped}) [{string.Join(", ", onDevice.ToArray())}]");
            }

            if (count > 1) overMore++;
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {bodies.Length} differ: {string.Join("; ", differ.Take(4))}");
        return overMore;
    }

    /// <summary>
    /// <b>Two thousand boxes over the fixture town are over exactly the ways the host finds</b> — every heading, on and off
    /// its ways, across its junctions and zebras and square on its lattice, on every channel.
    /// </summary>
    [Fact]
    public void EveryBoxIsOverTheWaysTheHostFinds()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);

        Assert.NotEqual(0, AssertTheSame(world.Atlas, Staged(world, 2400, 11, discs: false)));
    }

    /// <summary><b>The same for two thousand discs</b>, a walker's size and a little either side.</summary>
    [Fact]
    public void EveryDiscIsOverTheWaysTheHostFinds()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);

        Assert.NotEqual(0, AssertTheSame(world.Atlas, Staged(world, 2400, 13, discs: true)));
    }

    /// <summary>
    /// <b>On a deck over a street, a body on either channel or both is over the ways the host finds</b> — the town whose
    /// atlas carries every way's channels, so a body is read onto its own channels' ways alone.
    /// </summary>
    [Fact]
    public void ABodyOnADeckOverAStreetIsOverTheWaysTheHostFindsOnItsChannels()
    {
        using var world = new TownWorld(TracedPlanTests.OverAStreet(), Config);
        Assert.False(world.Atlas.Table.WayChannels.IsEmpty);

        BodyRow[] bodies = [.. Staged(world, 1200, 17, discs: false), .. Staged(world, 1200, 19, discs: true)];

        Assert.NotEqual(0, AssertTheSame(world.Atlas, bodies));
    }

    /// <summary>
    /// <b>Two thousand cars' wheels over the fixture town stand on exactly the ways the host finds</b>
    /// (<see cref="RibbonAtlas.UnderWheels"/>) — each wheel on a way or not as the host has it, and the car on the same
    /// ways in the same order over the same stretches.
    /// </summary>
    [Fact]
    public void EveryCarsWheelsStandOnTheWaysTheHostFinds()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);

        Assert.NotEqual(0, AssertTheSameWheels(world.Atlas, OnWheels(Staged(world, 2400, 23, discs: false), world.Atlas.StepM)));
    }

    /// <summary><b>The same on a deck over a street</b>, every car on either channel or both.</summary>
    [Fact]
    public void ACarsWheelsOnADeckOverAStreetStandOnTheWaysTheHostFindsOnItsChannels()
    {
        using var world = new TownWorld(TracedPlanTests.OverAStreet(), Config);

        Assert.NotEqual(0, AssertTheSameWheels(world.Atlas, OnWheels(Staged(world, 2400, 29, discs: false), world.Atlas.StepM)));
    }

    /// <summary>
    /// Each staged box as a car's four wheels, half a lattice step in from its corners — so a box square on the lattice
    /// stands every wheel on the edge between two points — with its body held a share of its length either side of each.
    /// </summary>
    static WheelsRow[] OnWheels(BodyRow[] boxes, float stepM) =>
    [
        .. boxes.Select(box =>
        {
            var alongM = box.Forward * (box.HalfLengthM - (stepM * 0.5f));
            var acrossM = Heading.RightOf(box.Forward) * (box.HalfWidthM - (stepM * 0.5f));
            return new WheelsRow(
                box.CentreM + alongM + acrossM, box.CentreM + alongM - acrossM, box.CentreM - alongM + acrossM,
                box.CentreM - alongM - acrossM, box.HalfLengthM * 0.3f, box.Channels);
        }),
    ];

    /// <summary>
    /// Every car held to the host's read of its wheels: which wheels stand on a way, the same ways in the same order with
    /// the same stretches, and as many dropped. Returns how many cars were found over more than one way.
    /// </summary>
    int AssertTheSameWheels(RibbonAtlas atlas, WheelsRow[] cars)
    {
        using var deviceAtlas = new DeviceAtlas(device.Vk, atlas.Table);
        using var asked = device.Holding<WheelsRow>(cars);
        using var counts = device.Sized<UInt4>(cars.Length);
        using var covers = device.Sized<WayCover>(cars.Length * RibbonAtlas.MostWaysUnderABody);
        GpuBuffer[] buffers = [asked, counts, covers, .. deviceAtlas.Tables];
        using var kernel = new ComputeKernel(device.Vk, "atlas_wheels.comp", buffers.Length, (uint)Marshal.SizeOf<AskPush>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        kernel.Dispatch(batch.Begin(), set, (uint)((cars.Length + 63) / 64), deviceAtlas.PushFor(cars.Length));
        batch.SubmitAndWait();
        var found = counts.Span<UInt4>().ToArray();
        var onTheDevice = covers.Span<WayCover>().ToArray();

        Span<WayCover> host = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        Span<Vector2> wheelsM = stackalloc Vector2[4];
        var differ = new List<string>();
        var overMore = 0;
        for (var at = 0; at < cars.Length; at++)
        {
            var car = cars[at];
            (wheelsM[0], wheelsM[1], wheelsM[2], wheelsM[3]) = (car.Wheel0M, car.Wheel1M, car.Wheel2M, car.Wheel3M);
            var droppedBefore = atlas.Dropped;
            var count = atlas.UnderWheels(wheelsM, car.AroundAWheelM, host, out var onAWay, (byte)car.Channels);
            var dropped = atlas.Dropped - droppedBefore;

            var onDevice = onTheDevice.AsSpan(at * RibbonAtlas.MostWaysUnderABody, (int)Math.Min(found[at].X, (uint)RibbonAtlas.MostWaysUnderABody));
            if (found[at] != new UInt4((uint)count, (uint)dropped, onAWay, 0u) || !onDevice.SequenceEqual(host[..count]))
            {
                differ.Add(
                    $"{car}: host {count} (+{dropped}, wheels {onAWay}) [{string.Join(", ", host[..count].ToArray())}], " +
                    $"device {found[at]} [{string.Join(", ", onDevice.ToArray())}]");
            }

            if (count > 1) overMore++;
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {cars.Length} differ: {string.Join("; ", differ.Take(4))}");
        return overMore;
    }
}
