using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>A served town's players as people of it</b> (MP-4, MP-13): each stood up on the pavement as a body of its own,
/// walked by its keys, in at the wheel of the nearest car with whoever drove it put out, out again at once, and stood up
/// as somebody new once it is down.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
public class PlayerBodiesTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly HandInput Standing = new(Held: true, Throttle: 0f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero);

    static TownWorld Served() => new(Towns.Of(Towns.Fixture), Config, served: true);

    static Vector2 Middle(TownWorld world) => world.Plan.WorldSizeM * 0.5f;

    /// <summary>MP-4: <b>a seat is stood up on its feet, on nothing</b>, as a body nobody else is.</summary>
    [Fact]
    public void ASeatIsStoodUpOnFootAsABodyOfItsOwn()
    {
        using var world = Served();

        Assert.True(world.SpawnPlayer(0, Middle(world)), "the fixture's middle had no pavement free to stand on");
        Assert.True(world.SpawnPlayer(1, Middle(world)), "the fixture's middle had room for one player only");

        var (first, second) = (world.BodyOfSeat(0), world.BodyOfSeat(1));
        Assert.NotEqual(first, second);
        Assert.Equal(ContainerKind.None, world.People.Inside[first].Kind);
        Assert.Equal((0, 1), (world.SeatOfPerson(first), world.SeatOfPerson(second)));
    }

    /// <summary>MP-3: <b>the keys walk a body on foot</b> the way they point.</summary>
    [Fact]
    public void TheKeysWalkABodyOnFoot()
    {
        using var world = Served();
        var loop = new SimLoop<TownWorld>(world, Config);
        world.SpawnPlayer(0, Middle(world));
        var body = world.BodyOfSeat(0);
        var fromM = world.People.PositionM[body];

        for (var tick = 0; tick < 120; tick++)
        {
            world.HandOnPlayer(0, Standing with { WalkDirection = Vector2.UnitX });
            loop.Advance();
        }

        var movedM = world.People.PositionM[body] - fromM;
        Assert.True(movedM.X > 1f && MathF.Abs(movedM.Y) < movedM.X, $"the body moved {movedM} m");
    }

    /// <summary>
    /// MP-13: <b>a press beside a car puts the player at its wheel and whoever drove it out</b> — and the keys drive it
    /// from then on.
    /// </summary>
    [Fact]
    public void APressBesideACarPutsThePlayerAtItsWheelAndItsDriverOut()
    {
        using var world = new TownWorld(Towns.Built, Config, served: true);
        var loop = new SimLoop<TownWorld>(world, Config);
        var car = ACarWithADriver(world, loop);
        var driver = world.Containment.DriverOf(car);
        var body = WalkedUpTo(world, loop, seat: 0, car);

        Assert.True(world.PlayerActs(0), "a press beside the car got the player into nothing");

        Assert.Equal(body, world.Containment.DriverOf(car));
        Assert.Equal(ContainerKind.None, world.People.Inside[driver].Kind);

        world.HandOnPlayer(0, Standing with { Throttle = 1f });
        loop.Advance(30);
        Assert.True(world.Cars.Command[car].ThrottleMps2 > 0f, "the player's throttle never reached the car it took");
    }

    /// <summary>MP-13: <b>a press at the wheel puts the player out beside the car at once</b>, on foot and walked by its keys again.</summary>
    [Fact]
    public void APressAtTheWheelPutsThePlayerOutBesideTheCar()
    {
        using var world = Served();
        var loop = new SimLoop<TownWorld>(world, Config);
        var car = AWholeCar(world);
        var body = WalkedUpTo(world, loop, seat: 0, car);
        world.PlayerActs(0);

        Assert.True(world.PlayerActs(0), "a press at the wheel did not get the player out");

        Assert.Equal(ContainerKind.None, world.People.Inside[body].Kind);
        Assert.Equal(-1, world.SeatOfCar(car));
        var offM = Vector2.Distance(world.People.PositionM[body], world.Cars.PositionM[car]);
        Assert.True(offM < world.Cars.BuildOf(car).HalfLengthM + Config.PersonExitSearchRadiusM, $"the player was put down {offM} m from the car");
    }

    /// <summary>
    /// MP-13: <b>a player that drove a car off is put out beside the car where it now stands</b>, and not where it got in
    /// — a car taken out of its bay keeps the bay as its own.
    /// </summary>
    [Fact]
    public void APlayerGettingOutOfACarItDroveIsPutDownBesideItWhereItStands()
    {
        using var world = Served();
        var loop = new SimLoop<TownWorld>(world, Config);
        var car = AWholeCar(world);
        WalkedUpTo(world, loop, seat: 0, car);
        world.PlayerActs(0);
        var takenAtM = world.Cars.PositionM[car];
        for (var tick = 0; tick < 180; tick++)
        {
            world.HandOnPlayer(0, Standing with { Throttle = 1f });
            loop.Advance();
        }

        Assert.True(Vector2.Distance(world.Cars.PositionM[car], takenAtM) > 2f * Config.PersonExitSearchRadiusM, "the car was never driven off");

        world.PlayerActs(0);

        var offM = Vector2.Distance(world.People.PositionM[world.BodyOfSeat(0)], world.Cars.PositionM[car]);
        Assert.True(offM < world.Cars.BuildOf(car).HalfLengthM + Config.PersonExitSearchRadiusM, $"the player was put down {offM} m from the car");
    }

    /// <summary>MP-13: <b>another player is put out as a town's driver is</b>, and is that player still, on foot.</summary>
    [Fact]
    public void APlayerTakingAnothersCarPutsTheOtherOut()
    {
        using var world = Served();
        var loop = new SimLoop<TownWorld>(world, Config);
        var car = AWholeCar(world);
        var first = WalkedUpTo(world, loop, seat: 0, car);
        world.PlayerActs(0);
        var second = WalkedUpTo(world, loop, seat: 1, car);

        Assert.True(world.PlayerActs(1));

        Assert.Equal(second, world.Containment.DriverOf(car));
        Assert.Equal(ContainerKind.None, world.People.Inside[first].Kind);
        Assert.Equal(0, world.SeatOfPerson(first));
    }

    /// <summary>MP-4: <b>a player down is stood up as somebody new</b>, and the body it was is left lying for the town.</summary>
    [Fact]
    public void APlayerDownIsStoodUpAsSomebodyNew()
    {
        using var world = Served();
        world.SpawnPlayer(0, Middle(world));
        var fallen = world.BodyOfSeat(0);
        world.People.Wounded[fallen] = true;
        Assert.True(world.IsDown(0));

        Assert.True(world.SpawnPlayer(0, Middle(world)));

        Assert.NotEqual(fallen, world.BodyOfSeat(0));
        Assert.False(world.IsDown(0));
        Assert.True(world.People.Wounded[fallen], "the body the player left was stood up with it");
    }

    /// <summary>
    /// The first car the town has somebody at the wheel of, run for up to a minute to find one, then held where it stands:
    /// the town's decisions stop while the players' bodies are still walked (<see cref="TownWorld.HoldAgents"/>).
    /// </summary>
    static int ACarWithADriver(TownWorld world, SimLoop<TownWorld> loop)
    {
        for (var tick = 0; tick < 3_600; tick++)
        {
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Containment.DriverOf(car) < 0 || world.Cars.Broken[car]) continue;

                world.HoldAgents = true;
                return car;
            }

            loop.Advance();
        }

        Assert.Fail("the town put nobody at a wheel in a minute");
        return -1;
    }

    /// <summary>The first car the town stood whole, held where it stands as <see cref="ACarWithADriver"/> is.</summary>
    static int AWholeCar(TownWorld world)
    {
        world.HoldAgents = true;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!world.Cars.Broken[car]) return car;
        }

        Assert.Fail("the fixture stood no car whole");
        return -1;
    }

    /// <summary>A seat stood up on the pavement nearest a car and walked at it until it is within reach of its body.</summary>
    static int WalkedUpTo(TownWorld world, SimLoop<TownWorld> loop, int seat, int car)
    {
        Assert.True(world.SpawnPlayer(seat, world.Cars.PositionM[car]), "nowhere free on the pavement by the car");
        var body = world.BodyOfSeat(seat);
        var reachM = world.Cars.BuildOf(car).FlankM + Config.Multiplayer.BoardReachM;
        for (var tick = 0; tick < 1_200; tick++)
        {
            var toM = world.Cars.PositionM[car] - world.People.PositionM[body];
            if (toM.Length() <= reachM) return body;

            world.HandOnPlayer(seat, Standing with { WalkDirection = toM });
            loop.Advance();
        }

        Assert.Fail($"the player walked at car {car} for twenty seconds and never reached it");
        return -1;
    }
}
