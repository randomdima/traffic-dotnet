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
public class TownWorldTests
{
    static TownWorld Open(string map, bool standStatics = true) =>
        new(Towns.Of(map), SimConfig.Shipped(), standStatics);

    static int AWalkerOutside(TownWorld world, SimLoop<TownWorld> loop) =>
        OutOfDoors.AWalker(world, loop, SimConfig.Shipped());

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
    public void AWalkerCoversGroundOnItsOwn()
    {
        using var world = Open(Towns.Fixture);
        Assert.NotEqual(0, world.People.Count);

        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());
        loop.Advance(600);

        var walked = 0f;
        for (var person = 0; person < world.People.Count; person++) walked += world.People.DistanceWalkedM[person];

        Assert.True(walked > world.People.Count, $"ten seconds of walking covered {walked:F1} m between them");
    }

    /// <summary>
    /// TER-2 as the exit condition asks for it — <b>visibly</b> slower on grass, by the coefficient
    /// and not by some amount. The same body, the same follower and the same tick count: the only
    /// difference is what it is standing on.
    /// </summary>
    [Fact]
    public void AWalkerSlowsOnGrassByTheTerrainCoefficient()
    {
        var config = SimConfig.Shipped();

        var paved = WalkProbe.Measure(config, config.Terrain.PavedCoefficient, onFeet: true);
        var grass = WalkProbe.Measure(config, config.Terrain.GrassCoefficient, onFeet: true);

        Assert.Equal(config.PersonWalkSpeedMps * config.Terrain.PavedCoefficient, paved.PaceMps, 1);
        Assert.Equal(config.PersonWalkSpeedMps * config.Terrain.GrassCoefficient, grass.PaceMps, 1);
        Assert.Equal(config.Terrain.GrassCoefficient / config.Terrain.PavedCoefficient, grass.PaceMps / paved.PaceMps, 2);
    }

    /// <summary>
    /// The normative relation, whose satisfying number is not
    /// the requirement: <b>a walker reaches its pace, and loses it, inside a fifth of its own body</b>.
    /// If the walk speed is ever retuned, this is what says whether the grip was retuned with it.
    /// </summary>
    [Fact]
    public void AWalkerReachesItsPaceInsideAFifthOfItsOwnBody()
    {
        var config = SimConfig.Shipped();
        var aFifthOfABody = config.PersonDiameterM / 5f;

        var run = WalkProbe.Measure(config, config.Terrain.PavedCoefficient, onFeet: true);

        Assert.True(run.ContinuousM <= aFifthOfABody,
            $"v²/2a is {run.ContinuousM:F3} m against a fifth of a body at {aFifthOfABody:F3} m");
        Assert.True(run.StopM <= aFifthOfABody, $"the stop measured {run.StopM:F3} m");
    }

    /// <summary>
    /// Off its feet a walker stops on the ground rather than on its soles, which is a lower grip and a
    /// longer stop. <b>How far a casualty is actually sent is a question about the strike</b> and is asked
    /// through the solver in <c>CrashCaseTests</c>, where there is a strike to ask it of.
    /// </summary>
    [Fact]
    public void OffItsFeetAWalkerStopsOnTheGroundAndNotOnItsSoles()
    {
        var config = SimConfig.Shipped();

        var onFeet = WalkProbe.Measure(config, config.Terrain.PavedCoefficient, onFeet: true);
        var offFeet = WalkProbe.Measure(config, config.Terrain.PavedCoefficient, onFeet: false);

        Assert.True(offFeet.StopM > onFeet.StopM,
            $"off its feet a body stopped in {offFeet.StopM:F3} m against {onFeet.StopM:F3} m on them");
    }

    /// <summary>CTL-2 and CTL-4: an order pins the goal, and a finished order ends in idle awaiting the next.</summary>
    [Fact]
    public void AnOrderIsObeyedAndThenLetGoOf()
    {
        using var world = Open(Towns.Fixture, standStatics: false);
        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());

        var walker = AWalkerOutside(world, loop);
        var from = world.People.PositionM[walker];

        // A point on the ground and not a building: right-clicking one of those is CTL-3, which is a
        // different order — walk there *and enter* — and is asserted on its own below.
        var toM = from + new Vector2(6f, 0f);
        foreach (var side in new[] { new Vector2(6f, 0f), new Vector2(-6f, 0f), new Vector2(0f, 6f), new Vector2(0f, -6f) })
        {
            if (world.BuildingAt(from + side) >= 0 || !world.Terrain.At(from + side).Walkable) continue;

            toM = from + side;
            break;
        }
        world.Order(walker, toM);
        loop.Advance(1);

        Assert.True(world.People.Manual[walker]);

        // The order is the walk's <em>goal</em>; what the follower is aimed at is the next point of the
        // line laid to it, which is the whole of what "everything below goal selection is untouched" means.
        Assert.Equal(toM, world.People.GoalM[walker]);

        loop.Advance(600);

        Assert.True((world.People.PositionM[walker] - toM).Length() < 2f,
            $"ordered to {toM}, ended at {world.People.PositionM[walker]}");

        // CTL-4: it idles awaiting the next order rather than picking somewhere of its own, and it is
        // still under orders until the reset says otherwise.
        Assert.False(world.People.Walking[walker]);
        Assert.True(world.People.Manual[walker]);

        world.ReleaseOrder(walker);
        loop.Advance(120);

        Assert.False(world.People.Manual[walker]);
        Assert.True(world.People.Walking[walker], "released, it should be choosing for itself again");
    }

    [Fact]
    public void SelectionIsAKindAndAnIndexAndNothingElse()
    {
        using var world = Open(Towns.Fixture);
        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());
        var walker = AWalkerOutside(world, loop);

        world.Select(new Selection(SelectionKind.Person, 0));
        Assert.Equal(new Selection(SelectionKind.Person, 0), world.Lead);
        Assert.Equal(1, world.SelectedCount);

        // Out of the roster is nothing at all, which is what makes a stale index harmless rather
        // than a walker somebody else is looking at.
        world.Select(new Selection(SelectionKind.Person, world.People.Count + 10));
        Assert.Equal(0, world.SelectedCount);

        // Asked of a body that is in the town: one inside a building is not there to be clicked on.
        Assert.Equal(walker, world.PersonAt(world.People.PositionM[walker]));
        Assert.Equal(-1, world.PersonAt(new Vector2(-1_000f, -1_000f)));

        // CTL-1: a car under the pointer is picked before a walker, since a walker under a car is
        // not visible to be clicked on.
        Assert.Equal(
            new Selection(SelectionKind.Car, 0), world.Pick(world.Cars.PositionM[0]));
        Assert.False(world.Pick(new Vector2(-1_000f, -1_000f)).Any);
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
        world.SelectNone();
        Assert.False(world.HandsOn);
        Assert.True(world.HandDriven.IsEmpty);
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
    /// CTL-1b through CTL-2: <b>one right-click is one order to every selected walker</b>, taken on the
    /// same tick and at the same point. Each of them then routes to it as itself.
    /// </summary>
    [Fact]
    public void AnOrderReachesEverySelectedWalker()
    {
        using var world = Open(Towns.Fixture);
        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());
        var walker = AWalkerOutside(world, loop);
        var second = OutOfDoors.AWalker(world, loop, SimConfig.Shipped(), besides: walker);

        world.Select(new Selection(SelectionKind.Person, walker));
        world.SelectAlso(new Selection(SelectionKind.Person, second));

        var toM = world.People.PositionM[walker];
        world.Order(walker, toM);
        world.Order(second, toM);
        loop.Advance(2);

        // CTL-4: both are in manual mode, which is the whole of what an order does to a walker's own
        // goal choice — everything under it is untouched.
        Assert.True(world.People.Manual[walker]);
        Assert.True(world.People.Manual[second]);
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

        var walking = world.People.Walking[0];
        var destination = world.People.DestinationM[0];

        world.HoldAgents = true;
        loop.Advance(600);

        Assert.Equal(walking, world.People.Walking[0]);
        Assert.Equal(destination, world.People.DestinationM[0]);
    }

    /// <summary>
    /// OBJ-2: props and buildings are real collision geometry, and standing them up is what the town
    /// pays at load rather than per tick. <b>OBJ-5a — a building is stood as the rectangles its roof is
    /// built of</b>, so the census is the parts and not the buildings, and the two counts differing is
    /// the whole point of the rule.
    /// </summary>
    [Fact]
    public void TheTownsOwnGeometryIsStoodUpAsBodies()
    {
        var plan = Towns.Of(Towns.Fixture);
        using var world = Open(Towns.Fixture);

        var parts = 0;
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var roof = BuildingRoofs.Of(plan, BuildingCatalog.Shared, world.Uses, building);
            parts += Math.Max(BuildingCatalog.Shared.Variants[roof.Variant].PartsM.Length, 1);
        }

        Assert.True(parts > plan.Buildings.Count, "no shipped roof is built of more than one rectangle");
        Assert.Equal(plan.Props.Count + parts, world.StaticBodyCount);
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
        var wasMass = world.Cars.MassKg[car];
        var wasAt = world.Cars.PositionM[car];
        var bodies = world.StaticBodyCount;

        figures.Trim.Friction = 2f;
        world.FiguresChanged();

        Assert.Equal(wasGrip * 2f, world.Cars.BuildOf(car).GripMps2, 3);

        // And what the car itself is came through untouched, because no dial speaks for a body.
        Assert.Equal(wasMass, world.Cars.MassKg[car], 3);

        // And the town itself did not move: the same bodies, in the same places, mid-whatever they were in.
        Assert.Equal(wasAt, world.Cars.PositionM[car]);
        Assert.Equal(bodies, world.StaticBodyCount);

        loop.Advance(60);
        Assert.Equal(180, loop.Tick);
    }
}
