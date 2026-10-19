using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>A rail vehicle is picked out as a car is, and a locomotive driven as one</b> (CTL-1, CTL-5, TRN-10): what a click on
/// each picks out, the camera's place, and the keys as its train's controls — in a town with a railway and a train on it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P7)]
public class TrainHandTests
{
    static readonly SimConfig Config = new() { Train = new TrainFigures { FirstLeastS = 0.1f, FirstMostS = 0.5f } };

    static readonly Lazy<TrafficSimulation.CityGen.CityPlan> Plan = new(() => Towns.LayFresh(Towns.Brief(seed: 3, cars: 0, railTracks: 2)));

    /// <summary>A town run until a train of it is in the world, and that train.</summary>
    static (TownWorld World, SimLoop<TownWorld> Loop, int Train) WithATrainOut()
    {
        var world = new TownWorld(Plan.Value, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        for (var tick = 0; tick < 2_000; tick++)
        {
            loop.Advance(1);
            for (var train = 0; train < world.Trains.Count; train++)
            {
                var vehicles = world.Trains.VehiclesOf(train);
                if (vehicles.Length > 0 && world.World.View.Wagons[vehicles[^1]].IsInWorld) return (world, loop, train);
            }
        }

        throw new InvalidOperationException("no train came out in the time");
    }

    /// <summary><b>A click on a train's locomotive picks out the unit its train is driven by</b> (CTL-1).</summary>
    [Fact]
    public void AClickOnALocomotivePicksOutItsTrain()
    {
        var (world, _, train) = WithATrainOut();
        using var owned = world;

        var picked = world.Pick(world.World.View.Wagons[world.Trains.VehiclesOf(train)[0]].PositionM);

        Assert.Equal(new Selection(SelectionKind.Locomotive, train), picked);
    }

    /// <summary>
    /// <b>A click on a wagon picks out that wagon alone</b> (CTL-1): a unit of its own, and the camera stands on it
    /// (OBS-1a).
    /// </summary>
    [Fact]
    public void AClickOnAWagonPicksOutThatWagonAndTheCameraStandsOnIt()
    {
        var (world, _, train) = WithATrainOut();
        using var owned = world;
        var wagon = world.Trains.VehiclesOf(train)[^1];
        var wagonM = world.World.View.Wagons[wagon].PositionM;

        world.Select(world.Pick(wagonM));

        Assert.Equal(new Selection(SelectionKind.Wagon, wagon), world.Lead);
        Assert.True(world.Whereabouts(world.Lead, out var atM, out _));
        Assert.Equal(wagonM, atM);
    }

    /// <summary>
    /// <b>The drive key against the way a train is going brakes it, and from a stop pulls it back</b> (CTL-5, TRN-10) —
    /// a car's brake pedal, said of a train.
    /// </summary>
    [Fact]
    public void TheKeyAgainstTheWayATrainGoesBrakesItAndFromAStopPullsItBack()
    {
        var (world, loop, train) = WithATrainOut();
        using var owned = world;
        world.Select(new Selection(SelectionKind.Locomotive, train));
        var back = new HandInput(true, -1f, 0f, false, Vector2.Zero);

        world.Hands(back);
        loop.Advance(1);
        Assert.True(world.Trains.Given(train).Brake, "a train going on was not braked");

        for (var tick = 0; tick < 20_000 && world.Trains.AlongMps(train) > Config.Driving.StopSpeedMps; tick++)
        {
            world.Hands(back);
            loop.Advance(1);
        }

        world.Hands(back);
        loop.Advance(1);
        var given = world.Trains.Given(train);
        Assert.False(given.Brake);
        Assert.True(given.Reverse);
        Assert.Equal(1f, given.Pull);
    }

    /// <summary>
    /// <b>A train let go of is held braked until the reset</b> (CTL-4, CTL-5b): the hand puts it in manual mode, and only
    /// the reset gives it back to its own driver.
    /// </summary>
    [Fact]
    public void ATrainLetGoOfIsHeldBrakedUntilTheReset()
    {
        var (world, loop, train) = WithATrainOut();
        using var owned = world;
        var unit = new Selection(SelectionKind.Locomotive, train);

        world.Select(unit);
        world.Hands(new HandInput(true, 0f, 0f, false, Vector2.Zero));
        loop.Advance(1);
        world.Select(default);
        loop.Advance(1);

        Assert.True(world.Trains.Handed(train), "a train let go of went back to its own driver");
        Assert.True(world.Trains.Given(train).Brake, "a train let go of was not braked");

        world.Select(unit);
        world.ReleaseHands();
        world.Select(default);
        loop.Advance(1);

        Assert.False(world.Trains.Handed(train), "the reset left the train in the player's hands");
    }
}
