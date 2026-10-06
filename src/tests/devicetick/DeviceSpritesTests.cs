using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.TrafficLight.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The frame's moving sprites emitted on the device</b> (<see cref="DeviceSprites"/>), held to what the host fills the
/// same two runs with (<see cref="TownSprites.FillMoving"/>): the same instances in the same order, field for field.
/// </summary>
/// <remarks>
/// <b>Exact but for a tow arm's bearing</b>, which is the device's own arctangent; where the arm stands is not, since
/// its direction is a quotient by a root and both are rounded as the host rounds them. Nothing else in a sprite is a
/// transcendental — a car's forward is laid from the host's — so every other field is compared bit for bit.
/// </remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceSpritesTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Every look the town is drawn in, measured once for the class.</summary>
    static readonly Lazy<TownSprites> Looks = new(() =>
    {
        var looks = TownSprites.Load(Config);
        looks.ReadAspects(SheetAtlas.Pack(looks.Sheets));
        return looks;
    });

    /// <summary>The interface a framing is laid out on: a desktop window's, as the sprite probe frames one.</summary>
    static readonly Vector2 UiPx = new(1600f, 900f);

    /// <summary>Twenty seconds of the fixture town, a frame of each framing every second.</summary>
    const int Ticks = 1_200;

    const int TicksAFrame = 60;

    /// <summary>
    /// <b>A running town is drawn as the host draws it</b>, at every framing a window opens on: close on a lit junction,
    /// behind a car, the whole town, a corner of it and the bridge — every car part by part as it drives, and the heads
    /// as their lights turn.
    /// </summary>
    /// <remarks>
    /// The fixture stands nobody and has nothing above its ground, so the walkers and the level above are asked of staged
    /// rosters (<see cref="EveryWalkerIsDrawnAsTheHostDrawsIt"/>, <see cref="EveryCarIsDrawnAsTheHostDrawsIt"/>).
    /// </remarks>
    [Fact]
    public void ARunningTownIsDrawnAsTheHostDrawsIt()
    {
        var looks = Looks.Value;
        var plan = Towns.Of(Towns.Fixture);
        using var world = new TownWorld(plan, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var room = TownSprites.RoomFor(plan, Config);
        using var onDevice = DeviceSprites.For(device.Vk, Config, world, looks, room);
        var over = new SpriteInstance[room.Over];
        var above = new SpriteInstance[Math.Max(1, room.Above)];

        var differ = new List<string>();
        var drawn = 0;
        while (loop.Tick < Ticks)
        {
            loop.Advance(TicksAFrame);
            onDevice.Lay(world);
            foreach (var (name, centreM, pxPerM) in Framings(world, plan))
            {
                var spanM = UiPx / pxPerM;
                var (overCount, aboveCount) = looks.FillMoving(world, Config, centreM, spanM, pxPerM, over, above);
                onDevice.Emit(centreM, spanM, pxPerM, world.ElapsedS);
                var frame = $"tick {loop.Tick} {name}";
                Compare($"{frame} over", over.AsSpan(0, overCount), onDevice.Over, looks, differ);
                Compare($"{frame} above", above.AsSpan(0, aboveCount), onDevice.Above, looks, differ);
                drawn += overCount + aboveCount;
            }
        }

        Assert.True(differ.Count == 0, $"{differ.Count} differences over {drawn} instances: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>
    /// <b>Every walker is drawn as the host draws it</b>, over every look and every way a walker is drawn: standing,
    /// mid-stride, lying in the road and put away inside — headed every way round, a few of them exactly on the edge
    /// between two facings, across a view that cuts some of them off.
    /// </summary>
    [Fact]
    public void EveryWalkerIsDrawnAsTheHostDrawsIt()
    {
        const int Count = 2_000;
        var looks = Looks.Value;
        var random = new Random(29);
        var people = new PersonFleet(Count);
        var octantRad = MathF.PI * 0.25f;
        for (var person = 0; person < Count; person++)
        {
            var headingRad = person % 10 == 0
                ? (-MathF.PI * 0.5f) - ((random.Next(-16, 16) + 0.5f) * octantRad)
                : (random.NextSingle() - 0.5f) * 30f;
            people.Add(
                default, new Vector2(random.NextSingle() * 200f, random.NextSingle() * 200f), headingRad, 70f, 0.25f,
                (byte)random.Next(256), new Rng((ulong)person, 1), reckless: false);
            people.Walking[person] = random.NextSingle() < 0.5f;
            people.Wounded[person] = random.NextSingle() < 0.25f;
            people.DistanceWalkedM[person] = random.NextSingle() * 1_000f;
            if (random.NextSingle() < 0.1f) people.Inside[person] = new Contained(ContainerKind.Building, 0);
        }

        var room = new SpriteRoom(1, 1, Count, 0);
        var noCars = new CarFleet(1, arcsPerCar: 1, CarBuilds.OfTheFleet(Config, looks.Cars));
        using var onDevice = new DeviceSprites(device.Vk, Config, looks, room, noCars, Count, SignalHeads.Nothing, default, levelled: false);
        onDevice.LayPeople(people);
        onDevice.LayCars(noCars, new RecoveryDuty(1), [], -1);

        var centreM = new Vector2(100f, 100f);
        var spanM = new Vector2(150f, 120f);
        var host = new SpriteInstance[Count];
        var written = PersonSprites.Fill(
            people, Enumerable.Range(0, Count).ToArray(), looks.People, looks.Aspects, looks.FirstDownSheet, centreM, spanM, host);
        onDevice.Emit(centreM, spanM, pixelsPerMetre: 10f, elapsedS: 0f);

        var differ = new List<string>();
        Compare("walkers", host.AsSpan(0, written), onDevice.Over, looks, differ);
        Assert.True(differ.Count == 0, $"{differ.Count} differences over {written} walkers: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>
    /// <b>Every car is drawn as the host draws it</b>, part by part, over every look: braking, reversing, on a call, at
    /// work, held by a hand, wrecked, on a pass at every stage of it, about to turn either way or straight on at a
    /// junction, towing and towed by either end — on the ground, on a level above and on a level neither run draws, at
    /// framings near enough for every part and far enough that the tyres and then the glows drop out.
    /// </summary>
    [Fact]
    public void EveryCarIsDrawnAsTheHostDrawsIt()
    {
        const int Count = 2_000;
        var looks = Looks.Value;
        var cars = StagedCars(Count, looks.Cars, out var recovery);
        Selection[] hands = [new Selection(SelectionKind.Car, 3), new Selection(SelectionKind.Person, 4), new Selection(SelectionKind.Car, 5)];
        const int AlsoDriven = 7;
        const float ElapsedS = 123.4f;

        var most = Count * (1 + TyreModel.SteeredWheels + CarLamps.Most + 1);
        var room = new SpriteRoom(1, 1, most, most);
        using var onDevice = new DeviceSprites(device.Vk, Config, looks, room, cars, 1, SignalHeads.Nothing, default, levelled: true);
        onDevice.LayPeople(new PersonFleet(1));
        onDevice.LayCars(cars, recovery, hands, AlsoDriven);

        var every = Enumerable.Range(0, Count).ToArray();
        var over = new SpriteInstance[most];
        var above = new SpriteInstance[most];
        var differ = new List<string>();
        var drawn = 0;
        var centreM = new Vector2(150f, 150f);
        foreach (var pxPerM in (ReadOnlySpan<float>)[20f, 4f, 1f])
        {
            var spanM = UiPx / pxPerM;
            var leastPartM = Config.View.CarPartLeastPx / pxPerM;
            var overCount = looks.FillCars(
                cars, recovery, every, Config, ElapsedS, hands, AlsoDriven, centreM, spanM, leastPartM, over, CityPlan.RoadArrays.Ground);
            var aboveCount = looks.FillCars(
                cars, recovery, every, Config, ElapsedS, hands, AlsoDriven, centreM, spanM, leastPartM, above, CityPlan.RoadArrays.Over);
            onDevice.Emit(centreM, spanM, pxPerM, ElapsedS);

            Compare($"{pxPerM} px/m over", over.AsSpan(0, overCount), onDevice.Over, looks, differ);
            Compare($"{pxPerM} px/m above", above.AsSpan(0, aboveCount), onDevice.Above, looks, differ);
            drawn += overCount + aboveCount;
        }

        Assert.True(differ.Count == 0, $"{differ.Count} differences over {drawn} instances: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>Where a window looks at the town: a lit junction, the first car, the whole of it, its corner and its bridge.</summary>
    static IEnumerable<(string Name, Vector2 CentreM, float PxPerM)> Framings(TownWorld world, CityPlan plan)
    {
        var streetPxPerM = MathF.Min(UiPx.X, UiPx.Y) / 45f;
        var heads = world.Heads.Heads;
        yield return ("junction", heads.Length > 0 ? heads[0].CentreM : plan.Junctions.CentreM[0], streetPxPerM);
        if (world.Cars.Count > 0) yield return ("car", world.Cars.PositionM[0], streetPxPerM);

        var townPxPerM = MathF.Min(UiPx.X / plan.WorldSizeM.X, UiPx.Y / plan.WorldSizeM.Y);
        yield return ("town", plan.WorldSizeM * 0.5f, townPxPerM);
        yield return ("corner", Vector2.Zero, streetPxPerM);

        var bridges = plan.Bridges;
        if (bridges.Road.Length == 0) yield break;

        var road = bridges.Road[0];
        var alongM = (bridges.FromM[0] + bridges.ToM[0]) * 0.5f;
        foreach (var piece in plan.Roads.Segments.AsSpan(plan.Roads.SegmentOffsets[road], plan.Roads.SegmentOffsets[road + 1] - plan.Roads.SegmentOffsets[road]))
        {
            if (alongM <= piece.LengthM)
            {
                yield return ("bridge", piece.PointAtM(alongM), MathF.Min(UiPx.X, UiPx.Y) / 150f);
                yield break;
            }

            alongM -= piece.LengthM;
        }
    }

    /// <summary>
    /// A fleet of every look standing about a square, each with a random hand of what its lamps answer, a short bent
    /// line, a level, and the recovery vehicles among them each towing another by either end or nothing.
    /// </summary>
    static CarFleet StagedCars(int count, CarCatalog catalogue, out RecoveryDuty recovery)
    {
        const int ArcsPerCar = 8;
        var random = new Random(31);
        float Between(float least, float most) => least + (random.NextSingle() * (most - least));
        bool Chance(float share) => random.NextSingle() < share;

        var cars = new CarFleet(count, ArcsPerCar, CarBuilds.OfTheFleet(Config, catalogue));
        recovery = new RecoveryDuty(count);
        for (var car = 0; car < count; car++)
        {
            cars.Add(
                default, new Vector2(Between(0f, 300f), Between(0f, 300f)), Between(-10f, 10f), (byte)random.Next(256), false,
                new Rng((ulong)car, 1));
            cars.Driven[car] = Chance(0.8f);
            cars.Broken[car] = Chance(0.1f);
            cars.BlueLight[car] = Chance(0.15f);
            cars.AtWork[car] = Chance(0.15f);
            cars.Command[car] = new DriveCommand(Between(-0.6f, 0.6f), 0f, Between(0f, 2f * Config.Lamps.BrakeMps2), false, Chance(0.2f));
            cars.ProgressM[car] = Between(0f, 40f);
            cars.AlongMps[car] = Between(-3f, 15f);
            cars.FuseJitter[car] = Between(0.8f, 1.2f);
            cars.TurningAtTheBox[car] = Chance(0.6f);
            cars.ToTheBoxM[car] = Between(0f, 2f * Config.Lamps.JunctionAheadM);
            cars.LineIsReverse[car] = Chance(0.2f);
            cars.Channels[car] = Chance(0.3f) ? (byte)(1 << CityPlan.RoadArrays.Over) : Chance(0.05f) ? (byte)(1 << 2) : CityPlan.RoadArrays.GroundChannel;
            if (Chance(0.2f)) cars.Context[car] = DriveContext.Clear with { PassAsideM = Chance(0.5f) ? -3f : 3f };
            if (Chance(0.3f))
            {
                var outM = cars.ProgressM[car] + Between(-20f, 10f);
                cars.Pass[car] = new Overtake(
                    1, outM, Between(5f, 15f), outM + Between(10f, 30f), Between(5f, 15f), Chance(0.5f) ? -3.5f : 3.5f, 0f, 0f, 0f,
                    Chance(0.8f));
            }

            var arcs = cars.LineArcsOf(car);
            var lengthM = 0f;
            var laid = random.Next(1, ArcsPerCar + 1);
            for (var arc = 0; arc < laid; arc++)
            {
                arcs[arc] = new ArcSeg(Vector2.Zero, 0f, Between(1f, 15f), Between(-0.12f, 0.12f));
                lengthM += arcs[arc].LengthM;
            }

            cars.Line[car] = new DrivenLine(laid, 1, lengthM);
        }

        for (var car = 0; car < count; car++)
        {
            if (catalogue.BeamOf(cars.Variant[car]) is null || !Chance(0.6f)) continue;

            var towed = random.Next(count);
            if (towed == car) continue;

            recovery.Towing[car] = towed;
            recovery.HeldByTheTail[towed] = Chance(0.5f);
        }

        return cars;
    }

    static void Compare(string frame, ReadOnlySpan<SpriteInstance> host, ReadOnlySpan<SpriteInstance> onDevice, TownSprites looks, List<string> differ)
    {
        if (host.Length != onDevice.Length) differ.Add($"{frame}: the host drew {host.Length}, the device {onDevice.Length}");

        for (var at = 0; at < Math.Min(host.Length, onDevice.Length); at++)
        {
            if (!Same(host[at], onDevice[at], looks)) differ.Add($"{frame} #{at}: host {host[at]}; device {onDevice[at]}");
        }
    }

    static bool Same(in SpriteInstance host, in SpriteInstance onDevice, TownSprites looks)
    {
        var arm = host.Sheet >= looks.FirstBeamSheet && host.Sheet < looks.FirstBuildingSheet;
        return host with { HeadingRad = 0f } == onDevice with { HeadingRad = 0f }
               && (arm ? Near(host.HeadingRad, onDevice.HeadingRad) : host.HeadingRad == onDevice.HeadingRad);
    }

    static bool Near(float host, float onDevice) =>
        host == onDevice || MathF.Abs(host - onDevice) <= 1e-2f + (1e-3f * MathF.Abs(host));
}
