using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen.Traced;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>What a deck's edges do</b> (PHY-1b), asked of the street carried east to west over another
/// (<see cref="TracedPlanTests.OverAStreet"/>): a car driven at its edge goes over and falls wherever along it, and one on
/// the ground under its low end is stopped by its side; a walker walks up onto its walk and
/// stands on it and its lanes, falls off them pushed clear, and walks under it on the ground.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class DeckEdgesInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly byte Ground = CityPlan.RoadArrays.GroundChannel;

    static readonly byte Over = CityPlan.RoadArrays.ChannelOf(CityPlan.RoadArrays.Over);

    /// <summary>North, square across the deck.</summary>
    static readonly float NorthRad = -MathF.PI / 2f;

    /// <summary><see cref="TracedPlanTests.OverAStreet"/> with one walker, its first, for a staging to put where it is wanted.</summary>
    internal static readonly CityPlan Plan = WithAWalker(TracedPlanTests.OverAStreet());

    static CityPlan WithAWalker(CityPlan plan)
    {
        var spawns = plan.Spawns;
        return new CityPlan
        {
            Seed = plan.Seed, Name = plan.Name, WorldSizeM = plan.WorldSizeM, PavementWidthM = plan.PavementWidthM,
            Junctions = plan.Junctions, JunctionCorners = plan.JunctionCorners, Roads = plan.Roads, Bridges = plan.Bridges,
            Roundabouts = plan.Roundabouts, PavedAreas = plan.PavedAreas, Crosswalks = plan.Crosswalks,
            ParkingLots = plan.ParkingLots, Buildings = plan.Buildings, Props = plan.Props, Water = plan.Water,
            Spawns = new CityPlan.SpawnArrays
            {
                Kind = [.. spawns.Kind, 0], PositionM = [.. spawns.PositionM, new Vector2(150f, 520f)],
                HeadingRad = [.. spawns.HeadingRad, 0f],
                AtTheKerb = spawns.AtTheKerb.Length > 0 ? [.. spawns.AtTheKerb, false] : [],
            },
        };
    }

    /// <summary>A place on the deck's line this far along it from where the deck begins.</summary>
    static Vector2 OnTheDeckM(float alongM) =>
        Spline.SampleAt(Plan.Roads.SegmentsOf(Plan.Bridges.Road[0]), Plan.Bridges.FromM[0] + alongM).PositionM;

    /// <summary>A quarter of the way along the deck: past its low ends, and clear of the cars the town stands mid-deck.</summary>
    internal static Vector2 DeckQuarterM => OnTheDeckM((Plan.Bridges.ToM[0] - Plan.Bridges.FromM[0]) * 0.25f);

    /// <summary>Halfway into the low end at the deck's start.</summary>
    static Vector2 LowEndM => OnTheDeckM(Config.Road.DeckLowEndM * 0.5f);

    /// <summary><b>A car driven off the middle of a deck falls</b>: a wreck, on the ground's channel.</summary>
    [Fact]
    public void ACarDrivenOffTheMiddleOfADeckFallsAWreckOntoTheGround()
    {
        var (world, car) = DrivenNorthFrom(DeckQuarterM, Over, seconds: 3f);
        using var held = world;

        Assert.Equal((true, Ground), (world.Cars.Broken[car], world.Cars.Channels[car]));
    }

    /// <summary><b>A car driven off a deck's edge near its end falls too</b>: nothing on the deck is held at its edge.</summary>
    [Fact]
    public void ACarDrivenOffADecksEdgeNearItsEndFallsToo()
    {
        var (world, car) = DrivenNorthFrom(LowEndM, Over, seconds: 3f);
        using var held = world;

        Assert.Equal((true, Ground), (world.Cars.Broken[car], world.Cars.Channels[car]));
    }

    /// <summary>
    /// <b>A car with a whole side over a deck's edge and its centre of mass on the deck hangs there</b>: intact, on the
    /// deck's channel.
    /// </summary>
    [Fact]
    public void ACarWithASideOverADecksEdgeAndItsCentreOnTheDeckHangsThere()
    {
        var (world, car) = StoodEastAcrossTheNorthEdge(centreInByHalfTracks: 0.5f, seconds: 1f);
        using var held = world;

        Assert.Equal((false, Over), (world.Cars.Broken[car], world.Cars.Channels[car]));
    }

    /// <summary>
    /// <b>A car with its centre of mass past a deck's edge falls</b>, though the wheels of its other side are still on the
    /// deck: a wreck, on the ground's channel.
    /// </summary>
    [Fact]
    public void ACarWithItsCentrePastADecksEdgeFallsThoughASideIsOnIt()
    {
        var (world, car) = StoodEastAcrossTheNorthEdge(centreInByHalfTracks: -0.5f, seconds: 1f);
        using var held = world;

        Assert.Equal((true, Ground), (world.Cars.Broken[car], world.Cars.Channels[car]));
    }

    /// <summary>
    /// The town with every agent held, one car stood facing east along the deck a quarter of the way along it on its
    /// channel, its centre this many halves of its track in from the north edge — its left wheels a half track further
    /// out — and left standing.
    /// </summary>
    static (TownWorld World, int Car) StoodEastAcrossTheNorthEdge(float centreInByHalfTracks, float seconds)
    {
        var world = new TownWorld(Plan, Config) { HoldAgents = true };
        var loop = new SimLoop<TownWorld>(world, Config);
        const int Car = 0;
        ref readonly var build = ref world.Cars.BuildOf(Car);
        var halfTrackM = 0f;
        for (var wheel = 0; wheel < build.WheelCount; wheel++) halfTrackM = MathF.Max(halfTrackM, MathF.Abs(TyreModel.WheelAtM(build, wheel).Y));

        var inM = (Plan.Bridges.DeckWidthM[0] * 0.5f) - (halfTrackM * centreInByHalfTracks);
        world.StandTheCarAt(Car, DeckQuarterM - new Vector2(0f, inM), 0f);
        world.PutTheCarOnChannels(Car, Over);
        loop.Advance((int)(seconds / Config.TickSeconds));
        return (world, Car);
    }

    /// <summary>
    /// <b>A car on the ground under a deck's low end is stopped by its side</b> — off any way under it, so on grass, and
    /// given the time grass takes to get there.
    /// </summary>
    [Fact]
    public void ACarOnTheGroundUnderADecksLowEndIsStoppedByItsSide()
    {
        var (world, car) = DrivenNorthFrom(LowEndM, Ground, seconds: 6f);
        using var held = world;

        Assert.True(world.Cars.PositionM[car].Y > SideLineY, $"the car is at {world.Cars.PositionM[car]}, past the side at y = {SideLineY}");
    }

    /// <summary><b>A walker on a deck off its lanes falls</b>: down, on the ground's channel.</summary>
    [Fact]
    public void AWalkerOnADeckOffItsLanesFallsDownOntoTheGround()
    {
        var (world, person) = WalkerStoodOnTheDeck(OffTheLanesM, seconds: 1f);
        using var held = world;

        Assert.Equal((true, Ground), (world.People.Wounded[person], world.People.Channels[person]));
    }

    /// <summary><b>A walker on a deck's lanes stands on them</b>: on its feet, on the deck's channel.</summary>
    [Fact]
    public void AWalkerOnADecksLanesStandsOnThem()
    {
        var (world, person) = WalkerStoodOnTheDeck(DeckQuarterM, seconds: 1f);
        using var held = world;

        Assert.Equal((false, Over), (world.People.Wounded[person], world.People.Channels[person]));
    }

    /// <summary><b>A walker falling off a deck is pushed off it</b>: further from the deck's line than where it stood.</summary>
    [Fact]
    public void AWalkerFallingOffADeckIsPushedOffIt()
    {
        var (world, person) = WalkerStoodOnTheDeck(OffTheLanesM, seconds: 1f);
        using var held = world;

        Assert.True(
            world.People.PositionM[person].Y < OffTheLanesM.Y, $"the walker lies at {world.People.PositionM[person]}, stood at {OffTheLanesM}");
    }

    /// <summary>
    /// <b>A walker walked along a street's pavement to a deck's end goes up onto its walk</b> (WLK-1b): on the deck's
    /// channel and on its feet a little way along it.
    /// </summary>
    [Fact]
    public void AWalkerWalkedOnToADeckFromTheStreetGoesUpOntoIt()
    {
        var (world, person) = PlayerWalked(
            fromM: OnTheDeckM(0f) - new Vector2(WalkedUpM, 0f), Vector2.UnitX, seconds: (WalkedUpM * 2f) / Config.PersonWalkSpeedMps);
        using var held = world;

        Assert.Equal((Over, false), (world.People.Channels[person], world.People.Wounded[person]));
    }

    /// <summary>
    /// <b>A walker walking up onto a deck is on its level by the deck's end</b> (WLK-1b), where the deck is drawn from — not
    /// still on the ground's, and drawn under it.
    /// </summary>
    [Fact]
    public void AWalkerWalkingUpOntoADeckIsOnItsLevelByItsEnd()
    {
        using var world = new TownWorld(Plan, Config, served: true) { HoldAgents = true };
        var loop = new SimLoop<TownWorld>(world, Config);
        var endM = OnTheDeckM(0f);
        Assert.True(world.SpawnPlayer(0, endM - new Vector2(WalkedUpM, 0f)), "no pavement free short of the deck");
        var person = world.BodyOfSeat(0);
        var walked = new HandInput(Held: true, Throttle: 0f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.UnitX);
        for (var tick = 0; tick < (WalkedUpM * 2f) / (Config.PersonWalkSpeedMps * Config.TickSeconds) && world.People.PositionM[person].X < endM.X; tick++)
        {
            world.HandOnPlayer(0, walked);
            loop.Advance();
        }

        Assert.True((world.People.Channels[person] & Over) != 0, $"the walker reached the deck's end on channels {world.People.Channels[person]}");
    }

    /// <summary><b>A walker walked under a deck along the street it carries over stays on the ground</b> (PHY-1a, WLK-1b).</summary>
    [Fact]
    public void AWalkerWalkedUnderADeckStaysOnTheGround()
    {
        var crossingM = TracedPlanTests.OverAStreetCrossingM;
        var (world, person) = PlayerWalked(
            fromM: crossingM + new Vector2(Plan.Bridges.DeckWidthM[0], WalkedUpM), -Vector2.UnitY,
            seconds: (WalkedUpM * 2f) / Config.PersonWalkSpeedMps);
        using var held = world;

        Assert.Equal((Ground, false, true), (world.People.Channels[person], world.People.Wounded[person], world.People.PositionM[person].Y < crossingM.Y));
    }

    /// <summary>How far short of a deck's end, or of its line, a walker is stood up before it is walked at it.</summary>
    const float WalkedUpM = 12f;

    /// <summary>A town served to one seat, every agent of its own held, and the seat's body stood up nearest a place and walked one way.</summary>
    static (TownWorld World, int Person) PlayerWalked(Vector2 fromM, Vector2 way, float seconds)
    {
        var world = new TownWorld(Plan, Config, served: true) { HoldAgents = true };
        var loop = new SimLoop<TownWorld>(world, Config);
        Assert.True(world.SpawnPlayer(0, fromM), $"no pavement free to stand on near {fromM}");
        var walked = new HandInput(Held: true, Throttle: 0f, Steer: 0f, Handbrake: false, WalkDirection: way);
        for (var tick = 0; tick < seconds / Config.TickSeconds; tick++)
        {
            world.HandOnPlayer(0, walked);
            loop.Advance();
        }

        return (world, world.BodyOfSeat(0));
    }

    /// <summary>Past the deck's walk to the north of its lanes, a quarter of the way along it.</summary>
    static Vector2 OffTheLanesM => DeckQuarterM - new Vector2(0f, Plan.Bridges.DeckWidthM[0] * 0.49f);

    /// <summary>The town with every agent held, one walker stood at <paramref name="atM"/> on the deck's channel, and run on.</summary>
    static (TownWorld World, int Person) WalkerStoodOnTheDeck(Vector2 atM, float seconds)
    {
        var world = new TownWorld(Plan, Config) { HoldAgents = true };
        var loop = new SimLoop<TownWorld>(world, Config);
        const int Person = 0;
        world.StandTheWalkerAt(Person, atM, NorthRad);
        world.PutTheWalkerOnTheChannels(Person, Over);
        loop.Advance((int)(seconds / Config.TickSeconds));
        return (world, Person);
    }

    /// <summary>Where the side of the deck's low end stands on its north edge, the deck running east to west.</summary>
    static float SideLineY => LowEndM.Y - (Plan.Bridges.DeckWidthM[0] * 0.5f);

    /// <summary>The town with every agent held but one car, stood at <paramref name="atM"/> facing north on <paramref name="channels"/>, and driven on.</summary>
    static (TownWorld World, int Car) DrivenNorthFrom(Vector2 atM, byte channels, float seconds)
    {
        var world = new TownWorld(Plan, Config) { HoldAgents = true };
        var loop = new SimLoop<TownWorld>(world, Config);
        const int Car = 0;
        world.StandTheCarAt(Car, atM, NorthRad);
        world.PutTheCarOnChannels(Car, channels);
        loop.Advance(2);

        for (var tick = 0; tick < seconds / Config.TickSeconds; tick++)
        {
            world.HandOnCar(0, Car, new HandInput(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero));
            loop.Advance(1);
        }

        return (world, Car);
    }
}
