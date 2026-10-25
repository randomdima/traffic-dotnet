using System.Numerics;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// CAR-54 on the device: <b>the track a car's smoke is laid along is the way the car drove</b> (<see cref="DeviceSmoke"/>,
/// <c>smoke_track.comp</c>) — the last second of it, at eight points a second, each where the car stood when the step that
/// laid it ended.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P8)]
public class DeviceSmokeTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Long enough for the town's people to reach their cars and drive them.</summary>
    const int Ticks = 600;

    /// <summary>
    /// Every point of every car's track is where the car stood at the step that laid it, as the view of that step shows
    /// it, and a car with a whole ring holds the last second of its way at eight points a second.
    /// </summary>
    [Fact]
    public void ACarsTrackIsWhereItStoodAtEachPoint()
    {
        using var town = new TownWorld(Towns.Built, Config);
        town.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var world = (DeviceWorld)town.World;
        world.LayTheSmoke();
        var loop = new SimLoop<TownWorld>(town, Config);

        // Where each car stood as each step left it: the view of a step is the town's a tick later (WorldLayer.StepsAhead).
        var stoodM = new Dictionary<long, Vector2[]>();
        for (var tick = 1; tick <= Ticks; tick++)
        {
            loop.Advance();
            stoodM[tick - WorldLayer.StepsAhead] = town.Cars.PositionM.AsSpan(0, town.Cars.Count).ToArray();
        }

        var (heads, points) = world.ReadTheSmoke();
        var dtS = 1f / Config.Sim.TickRateHz;
        var ring = Config.Smoke.TrackPoints;
        var (laid, whole) = (0, 0);
        for (var car = 0; car < town.Cars.Count; car++)
        {
            var head = heads[car];
            for (var back = 0; back < head.Count; back++)
            {
                var point = points[(car * ring) + (int)((head.Newest + ring - back) % ring)];
                var step = (long)MathF.Round(point.TimeS / dtS);
                if (!stoodM.TryGetValue(step, out var at)) continue;

                Assert.True(Vector2.Distance(point.PositionM, at[car]) < 1e-3f, $"car {car}'s point at {point.TimeS:F3} s is {point.PositionM}, and it stood at {at[car]}");
                laid++;
            }

            if (head.Count < ring) continue;

            var newestS = points[(car * ring) + (int)head.Newest].TimeS;
            var oldestS = points[(car * ring) + (int)((head.Newest + 1) % ring)].TimeS;
            Assert.InRange(newestS - oldestS, ((ring - 1) * Config.Smoke.TrackEveryS) - dtS, ((ring - 1) * Config.Smoke.TrackEveryS) + dtS);
            whole++;
        }

        Assert.True(laid > 0 && whole > 0, $"{laid} points and {whole} whole rings checked");
    }

    /// <summary>
    /// A car's smoke is aged off the moment of the view the host shows — the step before the newest
    /// (<see cref="WorldLayer.StepsAhead"/>) — so its newest puff leaves the pipe where the car is drawn and not a step
    /// ahead of it.
    /// </summary>
    [Fact]
    public void ACarsSmokeIsDrawnAtTheMomentItsViewShows()
    {
        using var town = new TownWorld(Towns.Built, Config);
        town.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var world = (DeviceWorld)town.World;
        world.LayTheSmoke();
        var loop = new SimLoop<TownWorld>(town, Config);
        for (var tick = 1; tick <= Ticks; tick++) loop.Advance();

        var (heads, _) = world.ReadTheSmoke();
        var dtS = 1f / Config.Sim.TickRateHz;
        var tracked = 0;
        for (var car = 0; car < town.Cars.Count; car++)
        {
            // A track begun at the newest step has no step before it to show.
            if (heads[car].Count < 2) continue;

            Assert.Equal(Ticks - WorldLayer.StepsAhead, (long)MathF.Round(heads[car].ShownS / dtS));
            tracked++;
        }

        Assert.True(tracked > 0, "no car had a track of two points");
    }

    /// <summary>
    /// TRN-15: a rail vehicle standing in the world lays a track where it stands, smoking, past every car's — each of the
    /// showroom's, where every look stands once and none is sent.
    /// </summary>
    [Fact]
    public void ARailVehicleInTheWorldLaysATrackWhereItStands()
    {
        using var town = new TownWorld(Towns.Of(ShowroomPlan.Name), Config);
        town.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var world = (DeviceWorld)town.World;
        world.LayTheSmoke();
        var loop = new SimLoop<TownWorld>(town, Config);
        for (var tick = 1; tick <= Ticks; tick++) loop.Advance();

        var (heads, _) = world.ReadTheSmoke();
        var wagons = town.World.View.Wagons;
        var first = world.Smoke!.FirstWagon;
        var standing = 0;
        for (var wagon = 0; wagon < wagons.Length; wagon++)
        {
            if (!wagons[wagon].IsInWorld) continue;

            var head = heads[first + wagon];
            Assert.True(head.Live.Running > 0f, $"wagon {wagon} stands in the world and its track says it does not run");
            Assert.True(
                Vector2.Distance(head.Live.PositionM, wagons[wagon].PositionM) < 0.5f,
                $"wagon {wagon}'s track is at {head.Live.PositionM}, and it stands at {wagons[wagon].PositionM}");
            standing++;
        }

        Assert.True(standing > 0, "no rail vehicle stood in the world");
    }

    /// <summary>
    /// TRN-15: a diesel standing in the world, framed closer than its neighbours' smoke reaches, has puffs drawn off its
    /// stack, and an electric none.
    /// </summary>
    [Fact]
    public void ADieselStandingInTheWorldSmokesAndAnElectricDoesNot()
    {
        var plan = Towns.Of(ShowroomPlan.Name);
        using var town = new TownWorld(plan, Config);
        town.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var world = (DeviceWorld)town.World;
        var looks = TownSprites.Load(Config);
        looks.Lay(plan, town.Uses, Config);
        var sprites = world.DrawWith(town, looks, looks.RoomFor(plan, Config));
        var loop = new SimLoop<TownWorld>(town, Config);
        for (var tick = 1; tick <= Ticks; tick++) loop.Advance();

        var wagons = town.World.View.Wagons.ToArray();
        int Puffs(VehicleKind kind)
        {
            var wagon = Array.FindIndex(wagons, w => w.IsInWorld && looks.Rolling.Vehicles[w.Look].Kind == kind);
            Assert.True(wagon >= 0, $"no {kind} stood in the world");
            sprites.Emit(wagons[wagon].PositionM, new Vector2(4f, 2f), 40f, town.ElapsedS);
            var puffs = 0;
            foreach (var instance in sprites.Over) puffs += instance.Sheet == looks.SmokeSheet ? 1 : 0;
            return puffs;
        }

        Assert.True(Puffs(VehicleKind.Diesel) > 0, "a diesel standing in the world drew no smoke");
        Assert.Equal(0, Puffs(VehicleKind.Electric));
    }

    /// <summary>A town with a railway and no cars, whose trains come out soon.</summary>
    static readonly Lazy<CityPlan> Railway = new(() => Towns.LayFresh(Towns.Brief(seed: 3, cars: 0, railTracks: 2)));

    /// <summary>
    /// TRN-15: <b>a diesel pulling hard under way smokes from its stack</b> — a puff half as thick as the thickest it draws
    /// covers the stack, and the train has not run off the root of its own smoke before the smoke shows.
    /// </summary>
    [Fact]
    public void ADieselPullingHardSmokesFromItsStack()
    {
        var config = new SimConfig
        {
            Train = new TrainFigures { FirstLeastS = 0.1f, FirstMostS = 0.5f }, Chimneys = new ChimneyFigures { SmokingShare = 0f },
        };
        var plan = Railway.Value;
        using var town = new TownWorld(plan, config);
        town.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var world = (DeviceWorld)town.World;
        var looks = TownSprites.Load(config);
        looks.Lay(plan, town.Uses, config);
        var sprites = world.DrawWith(town, looks, looks.RoomFor(plan, config));
        var loop = new SimLoop<TownWorld>(town, config);

        int LeadOf(int train) => town.Trains.VehiclesOf(train).Length > 0 ? town.Trains.VehiclesOf(train)[0] : -1;
        bool UnderWay(int train) =>
            LeadOf(train) >= 0 && town.World.View.Wagons[LeadOf(train)] is { IsInWorld: true } lead
            && looks.Rolling.Vehicles[lead.Look].ExhaustsM.Length > 0 && lead.VelocityMps.Length() > MovingMps;

        var diesel = -1;
        for (var tick = 0; tick < 6_000 && diesel < 0; tick++)
        {
            loop.Advance();
            for (var train = 0; train < town.Trains.Count && diesel < 0; train++) diesel = UnderWay(train) ? train : -1;
        }

        Assert.True(diesel >= 0, "no train led by a diesel got under way");
        town.Select(new Selection(SelectionKind.Locomotive, diesel));
        var pull = new HandInput(true, 1f, 0f, false, Vector2.Zero);
        for (var tick = 0; tick < PullTicks; tick++)
        {
            town.Hands(pull);
            loop.Advance();
        }

        var view = town.World.View.Wagons[LeadOf(diesel)];
        Assert.True(view.VelocityMps.Length() > MovingMps, $"the diesel is at {view.VelocityMps.Length():F1} m/s");
        var (sin, cos) = MathF.SinCos(view.HeadingRad);
        var forward = new Vector2(cos, sin);
        var stackM = looks.Rolling.Vehicles[view.Look].ExhaustsM[0];
        var stackAtM = view.PositionM + (forward * stackM.X) + (new Vector2(-forward.Y, forward.X) * stackM.Y);
        sprites.Emit(stackAtM, new Vector2(30f, 30f), 40f, town.ElapsedS);

        var (nearestM, puffs) = NearestHalfThickM(sprites.Over, looks.SmokeSheet, stackAtM);
        Assert.True(puffs > 0, $"a diesel at {view.VelocityMps.Length():F1} m/s drew no smoke");
        Assert.True(
            nearestM < config.Smoke.StackFullBirthWidthM / 2f,
            $"at {view.VelocityMps.Length():F1} m/s its smoke is half as thick as its thickest {nearestM:F2} m off its stack, of {puffs} puffs");
    }

    /// <summary>
    /// OBJ-7: <b>a chimney's plume is thick at its pot</b> — a puff half as thick as the plume's thickest stands on the pot,
    /// within its half-width, however the wind blows and wherever the moment falls between two puffs leaving, so the
    /// plume rises off the pot and not off the roof downwind of it.
    /// </summary>
    [Fact]
    public void AChimneysPlumeIsThickAtItsPot()
    {
        var config = new SimConfig { Chimneys = new ChimneyFigures { SmokingShare = 1f } };
        var plan = Towns.Built;
        using var town = new TownWorld(plan, config);
        town.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var world = (DeviceWorld)town.World;
        var looks = TownSprites.Load(config);
        looks.Lay(plan, town.Uses, config);
        Assert.NotEmpty(looks.Chimneys);
        var sprites = world.DrawWith(town, looks, looks.RoomFor(plan, config));
        var loop = new SimLoop<TownWorld>(town, config);
        for (var tick = 0; tick < Ticks; tick++) loop.Advance();

        var pot = looks.Chimneys[0];

        // Asked a tick apart across one puff's leaving to the next, so the root is seen where it is furthest off too.
        var ticks = (int)MathF.Ceiling(config.Chimneys.PuffEveryS / config.TickSeconds);
        for (var tick = 0; tick < ticks; tick++)
        {
            loop.Advance();
            sprites.Emit(pot.AtM, new Vector2(6f, 6f), 80f, town.ElapsedS);
            var (nearestM, puffs) = NearestHalfThickM(sprites.Over, looks.SmokeSheet, pot.AtM);
            Assert.True(puffs > 0, $"the pot at {pot.AtM} burning drew no smoke");
            Assert.True(
                nearestM < pot.PotWidthM / 2f,
                $"at {town.ElapsedS:F2} s its plume is half as thick as its thickest {nearestM:F2} m off its pot, of {puffs} puffs");
        }
    }

    /// <summary>How fast a train is taken to be under way.</summary>
    const float MovingMps = 5f;

    /// <summary>Long enough for a puff off a diesel pulling hard to have lived its whole life.</summary>
    const int PullTicks = 300;

    /// <summary>
    /// How far from <paramref name="fromM"/> the nearest puff drawn in <paramref name="over"/> stands that is at least half as
    /// thick as the thickest of them, and how many puffs were drawn.
    /// </summary>
    static (float NearestM, int Puffs) NearestHalfThickM(ReadOnlySpan<SpriteInstance> over, int smokeSheet, Vector2 fromM)
    {
        var (thickest, puffs) = (0u, 0);
        foreach (var instance in over)
        {
            if (instance.Sheet != smokeSheet) continue;
            thickest = Math.Max(thickest, instance.PackedTint >> 24);
            puffs++;
        }

        var nearestM = float.MaxValue;
        foreach (var instance in over)
        {
            if (instance.Sheet == smokeSheet && 2 * (instance.PackedTint >> 24) >= thickest)
                nearestM = MathF.Min(nearestM, Vector2.Distance(instance.CentreM, fromM));
        }

        return (nearestM, puffs);
    }
}
