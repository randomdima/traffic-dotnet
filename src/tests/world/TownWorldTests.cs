using System.Numerics;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The town as a running simulation, over the maps this engine ships. What is being asked is the
/// walking skeleton's own exit condition: a person walks, the ground it is on changes how fast, and
/// an order is obeyed and then let go of.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P1)]
public class TownWorldTests
{
    static TownWorld Open(string map, bool standStatics = true) =>
        new(Towns.Of(map), SimConfig.Shipped(), standStatics);

    [Theory]
    [MemberData(nameof(Towns.EveryTown), MemberType = typeof(Towns))]
    public void EveryShippedTownStandsUpAndTicks(string map)
    {
        using var world = Open(map);
        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());

        loop.Advance(600);

        Assert.Equal(600, loop.Tick);
        for (var person = 0; person < world.People.Count; person++)
        {
            Assert.False(float.IsNaN(world.People.PositionM[person].X), $"walker {person} left the world");
        }
    }

    [Fact]
    public void SelectionIsAKindAndAnIndexAndNothingElse()
    {
        using var world = Open(Towns.Fixture);

        world.Select(new Selection(SelectionKind.Car, 0));
        Assert.Equal(new Selection(SelectionKind.Car, 0), world.Lead);
        Assert.Equal(1, world.SelectedCount);

        // Out of the roster is nothing at all, which is what makes a stale index harmless rather
        // than a body somebody else is looking at.
        world.Select(new Selection(SelectionKind.Car, world.Cars.Count + 10));
        Assert.Equal(0, world.SelectedCount);

        // CTL-1: a car under the pointer is what is picked, and nothing is picked off the map.
        Assert.Equal(new Selection(SelectionKind.Car, 0), world.Pick(world.Cars.PositionM[0]));
        Assert.False(world.Pick(new Vector2(-1_000f, -1_000f)).Any);
        Assert.Equal(-1, world.PersonAt(new Vector2(-1_000f, -1_000f)));
    }

    /// <summary>
    /// CTL-5: a hand at the wheel produces the same kind of command a follower does, so everything
    /// under the behaviour still binds — and CTL-5b, that letting go coasts rather than handing the
    /// car back.
    /// </summary>
    [Fact]
    public void AHandAtTheWheelDrivesThroughTheSameSeamTheFollowerUses()
    {
        using var world = Open(Towns.Fixture);
        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());

        world.Select(new Selection(SelectionKind.Car, 0));
        world.Hands(new HandInput(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero));
        loop.Advance(60);

        Assert.True(world.HandsOn);
        Assert.True(Selection.Holds(world.HandDriven, SelectionKind.Car, 0));
        Assert.True(world.Cars.Command[0].ThrottleMps2 > 0f, "the throttle key should reach the tyres as a pedal");
        Assert.True(world.Cars.VelocityMps[0].Length() > 0f, "a car under power should be moving");

        // CTL-5c: the beacon is the one thing the hand runs outside the car, and it buys nothing —
        // the road is still not told.
        Assert.False(world.Cars.BlueLight[0], "a hand at the wheel granted itself the road");

        // A change of selection gives up the wheel, so nothing drives on out of sight.
        world.Select(default);
        Assert.False(world.HandsOn);
        Assert.True(world.HandDriven.IsEmpty);
    }

    /// <summary>
    /// CTL-5b and CTL-4: <b>the player's hand puts a car in manual mode</b>, so letting go leaves it standing
    /// rather than back on the leg it was driving, and only the reset hands it back.
    /// </summary>
    [Fact]
    public void LettingGoOfTheWheelLeavesTheCarWaitingUntilTheReset()
    {
        using var world = Open(Towns.Fixture);
        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());
        var car = ADrivenCar(world);
        var unit = new Selection(SelectionKind.Car, car);

        world.Select(unit);
        world.Hands(new HandInput(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero));
        loop.Advance(30);
        world.Select(default);
        loop.Advance(2);

        Assert.True(world.IsUnderOrders(car), "letting go of the wheel handed the car back");
        Assert.False(world.Cars.Driven[car], "a car let go of drove on along the leg it had before the hand");

        world.Select(unit);
        world.ReleaseHands();
        Assert.False(world.IsUnderOrders(car), "the reset left the car under the player");
    }

    /// <summary>CTL-5d: <b>a second driver's hand is not the player's</b>, so it leaves no manual mode behind it.</summary>
    [Fact]
    public void ASecondDriversHandLeavesTheCarItsOwn()
    {
        using var world = Open(Towns.Fixture);
        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());
        var car = ADrivenCar(world);

        world.HandOnCar(0, car, new HandInput(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero));
        loop.Advance(30);

        Assert.False(world.IsUnderOrders(car));
    }

    /// <summary>CTL-5d, MP-3: <b>each seat's keys reach its own car and no other</b>.</summary>
    [Fact]
    public void EachSeatDrivesOnlyItsOwnCar()
    {
        using var world = Open(Towns.Fixture);
        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());
        var first = ADrivenCar(world);
        var second = AnotherDrivenCar(world, first);
        var floored = new HandInput(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero);
        var braked = new HandInput(Held: true, Throttle: 0f, Steer: 0f, Handbrake: true, WalkDirection: Vector2.Zero);

        world.HandOnCar(0, first, floored);
        world.HandOnCar(1, second, braked);
        loop.Advance(30);

        Assert.True(world.Cars.Command[first].ThrottleMps2 > 0f, "the first seat's throttle did not reach its car");
        Assert.False(world.Cars.Command[first].Handbrake, "the second seat's handbrake reached the first seat's car");
        Assert.True(world.Cars.Command[second].Handbrake, "the second seat's handbrake did not reach its car");
        Assert.Equal(0f, world.Cars.Command[second].ThrottleMps2);
    }

    /// <summary>CTL-5e: <b>a floored hand's boost asks for its pull share of the car's own pull</b>.</summary>
    [Fact]
    public void ABoostedHandAsksForItsShareMoreOfTheCarsPull()
    {
        using var world = Open(Towns.Fixture);
        var config = SimConfig.Shipped();
        var loop = new SimLoop<TownWorld>(world, config);
        var car = ADrivenCar(world);
        var build = world.Cars.BuildOf(car);

        world.HandOnCar(0, car, new HandInput(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero, Boost: true));
        loop.Advance(60);

        Assert.Equal(build.AccelerationMps2 * config.Control.HandBoostPullShare, world.Cars.Command[car].ThrottleMps2, 3);
    }

    static int AnotherDrivenCar(TownWorld world, int not)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (car != not && world.Cars.Driven[car] && !world.Cars.Broken[car]) return car;
        }

        Assert.Fail("the fixture stands one driven car only");
        return -1;
    }

    static int ADrivenCar(TownWorld world)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Driven[car] && !world.Cars.Broken[car]) return car;
        }

        Assert.Fail("the fixture stands no driven car");
        return -1;
    }

    /// <summary>
    /// CTL-1b: <b>one hand and many units</b>. The same command reaches every selected car through the
    /// same seam, and each answers it with its own body — which is what makes a group of cars driven at
    /// once still a group of cars and not one car with four pictures.
    /// </summary>
    [Fact]
    public void OneHandDrivesEverySelectedCar()
    {
        using var world = Open(Towns.Fixture);
        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());
        Assert.True(world.Cars.Count >= 2, "the fixture stands fewer cars than a group needs");

        world.Select(new Selection(SelectionKind.Car, 0));
        world.SelectAlso(new Selection(SelectionKind.Car, 1));
        world.Hands(new HandInput(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero));
        loop.Advance(60);

        for (var car = 0; car < 2; car++)
        {
            Assert.True(
                world.Cars.Command[car].ThrottleMps2 > 0f, $"car {car} was selected and took none of the throttle");
            Assert.True(world.Cars.VelocityMps[car].Length() > 0f, $"car {car} was under power and did not move");
        }
    }

    /// <summary>
    /// CAR-3a: <b>a key is a pedal being pushed and a wheel being wound, never either of them arriving.</b>
    /// The travel is the body's and is the same travel the follower is held to, so what a hand gets is the
    /// car the town's own drivers are driving — and a press can be held part way, which is the whole of what
    /// makes a car with digital controls drivable.
    /// </summary>
    [Fact]
    public void AKeyPressWindsTheWheelOnRatherThanSelectingALock()
    {
        var config = SimConfig.Shipped();
        using var world = Open(Towns.Fixture);
        var loop = new SimLoop<TownWorld>(world, config);

        world.Select(new Selection(SelectionKind.Car, 0));
        world.Hands(new HandInput(Held: true, Throttle: 1f, Steer: 1f, Handbrake: false, WalkDirection: Vector2.Zero));

        ref readonly var build = ref world.Cars.BuildOf(0);
        loop.Advance();
        var afterOneTick = world.Cars.Command[0];
        Assert.True(
            afterOneTick.SteerRad < build.MaxSteerRad * 0.5f,
            $"one tick of the key put the wheel at {afterOneTick.SteerRad:F3} of {build.MaxSteerRad:F3} rad");
        Assert.True(
            afterOneTick.ThrottleMps2 < build.AccelerationMps2 * 0.5f,
            $"one tick of the key put the throttle at {afterOneTick.ThrottleMps2:F2} of "
            + $"{build.AccelerationMps2:F2} m/s²");

        // And both arrive: what the travel costs is a moment and never the demand itself.
        loop.Advance((int)MathF.Round(config.Driving.WheelTravelS / config.TickSeconds));
        Assert.Equal(build.MaxSteerRad, world.Cars.Command[0].SteerRad, 1e-3f);
        Assert.Equal(build.AccelerationMps2, world.Cars.Command[0].ThrottleMps2, 1e-2f);
    }

    /// <summary>
    /// The <c>Pause</c> key: the decide loop is skipped while the bodies keep stepping, and nothing
    /// is unwound — no stuck clock runs up while the town stands still.
    /// </summary>
    [Fact]
    public void HoldingTheAgentsStopsThemDecidingAndLeavesTheirStateAlone()
    {
        using var world = Open(Towns.Fixture);
        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());
        loop.Advance(120);

        var line = world.Cars.Line[0];
        var progressM = world.Cars.ProgressM[0];

        world.HoldAgents = true;
        loop.Advance(600);

        // The body goes on stepping — it is under no hand and the solver is not held — but nothing decided
        // for it, so the line it was given is the line it is still driving.
        Assert.Equal(line, world.Cars.Line[0]);
        Assert.True(world.Cars.ProgressM[0] >= progressM, "the car was wound back rather than left alone");
    }

    /// <summary>
    /// <b>A figure turned reaches the town that is standing</b> (<see cref="TrimFigures"/>): every look is
    /// built again and the cars on the road take it, without the map being laid a second time. What the
    /// panel is for is watching one thing change while everything else holds still, and a town torn down
    /// and stood up again is a different town with the same name.
    /// </summary>
    [Fact]
    public void AFigureTurnedReachesTheStandingTownWithoutRelayingIt()
    {
        var figures = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(Towns.Fixture), figures);
        var loop = new SimLoop<TownWorld>(world, figures);
        loop.Advance(120);

        Assert.True(world.Cars.Count > 0, "the fixture town stands cars");
        var car = 0;
        var wasGrip = world.Cars.BuildOf(car).GripMps2;
        var wasMass = world.Cars.MassKgOf(car);
        var wasAt = world.Cars.PositionM[car];
        var bodies = world.StaticBodyCount;

        figures.Trim.Friction = 2f;
        world.FiguresChanged();

        Assert.Equal(wasGrip * 2f, world.Cars.BuildOf(car).GripMps2, 3);

        // And what the car itself is came through untouched, because no dial speaks for a body.
        Assert.Equal(wasMass, world.Cars.MassKgOf(car), 3);

        // And the town itself did not move: the same bodies, in the same places, mid-whatever they were in.
        Assert.Equal(wasAt, world.Cars.PositionM[car]);
        Assert.Equal(bodies, world.StaticBodyCount);

        loop.Advance(60);
        Assert.Equal(180, loop.Tick);
    }
}
