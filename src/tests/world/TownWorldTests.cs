using System.Numerics;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The town as a running simulation, over the maps this engine ships. What is being asked is the
/// walking skeleton's own exit condition: a person walks, the ground it is on changes how fast, and
/// an order is obeyed and then let go of.
/// </summary>
[Collection(TrafficSimulation.Tests.Simulation.SolverCollection.Name)]
[Trait(Tier.Key, Tier.Town)]
public class TownWorldTests
{
    static TownWorld Open(string map, bool standStatics = true) =>
        new(Towns.Fresh(map), SimConfig.Shipped(), standStatics);

    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
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

        Assert.Equal(config.Person.WalkSpeedMps * config.Terrain.PavedCoefficient, paved.PaceMps, 1);
        Assert.Equal(config.Person.WalkSpeedMps * config.Terrain.GrassCoefficient, grass.PaceMps, 1);
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

    /// <summary>Off its feet a walker is sent down the road, and the two grips are what make that a different thing.</summary>
    [Fact]
    public void OffItsFeetAWalkerSlidesFurtherThanItsOwnBody()
    {
        var config = SimConfig.Shipped();

        var onFeet = WalkProbe.Measure(config, config.Terrain.PavedCoefficient, onFeet: true);
        var offFeet = WalkProbe.Measure(config, config.Terrain.PavedCoefficient, onFeet: false);

        Assert.True(offFeet.StopM > config.PersonDiameterM, $"a slide of {offFeet.StopM:F2} m is not being sent anywhere");
        Assert.True(offFeet.StopM > onFeet.StopM * 10f);
    }

    /// <summary>CTL-2 and CTL-4: an order pins the goal, and a finished order ends in idle awaiting the next.</summary>
    [Fact]
    public void AnOrderIsObeyedAndThenLetGoOf()
    {
        using var world = Open(Towns.Fixture, standStatics: false);
        var loop = new SimLoop<TownWorld>(world, SimConfig.Shipped());

        var walker = 0;
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

        world.Selected = new Selection(SelectionKind.Person, 0);
        Assert.Equal(new Selection(SelectionKind.Person, 0), world.Selected);
        Assert.Equal(0, world.SelectedPerson);
        Assert.Equal(-1, world.SelectedCar);

        // Out of the roster is nothing at all, which is what makes a stale index harmless rather
        // than a walker somebody else is looking at.
        world.Selected = new Selection(SelectionKind.Person, world.People.Count + 10);
        Assert.False(world.Selected.Any);

        Assert.Equal(0, world.PersonAt(world.People.PositionM[0]));
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

        world.Selected = new Selection(SelectionKind.Car, 0);
        world.Hands(new HandInput(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero));
        loop.Advance(60);

        Assert.True(world.HandsOn);
        Assert.True(world.Cars.Command[0].ThrottleMps2 > 0f, "the throttle key should reach the tyres as a pedal");
        Assert.True(world.Cars.VelocityMps[0].Length() > 0f, "a car under power should be moving");

        // A change of selection gives up the wheel, so nothing drives on out of sight.
        world.Selected = default;
        Assert.False(world.HandsOn);
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
    /// pays at load rather than per tick.
    /// </summary>
    [Fact]
    public void TheTownsOwnGeometryIsStoodUpAsBodies()
    {
        var plan = Towns.Of(Towns.Fixture);
        using var world = Open(Towns.Fixture);

        Assert.Equal(plan.Props.Count + plan.Buildings.Count, world.StaticBodyCount);
    }
}
