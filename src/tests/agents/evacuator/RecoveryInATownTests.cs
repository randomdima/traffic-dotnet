using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Evacuator;

/// <summary>
/// <b>What an evacuator takes on</b> (EVA-3), staged on the suite's own city, the one town it lays with a depot.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class RecoveryInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// EVA-3, EVA-5: <b>an evacuator takes no wreck lying beside no road</b>, which has no place it could stand to hitch
    /// it from. Taken, the recovery was given up at the next decision and the same wreck taken again at the one after —
    /// a leg laid afresh every decision, shaped out of the truck's bay from the middle of the road.
    /// </summary>
    [Fact]
    public void AnEvacuatorTakesNoWreckItCannotHitch()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);
        Assert.True(world.Evacuators > 0, "the city stood no evacuator");

        var cars = world.Cars;
        var wreck = Enumerable.Range(0, cars.Count).First(each =>
            world.Parking.BayOf(each) >= 0 && !cars.Driven[each] && !cars.Broken[each] && world.Recovery.Depot[each] < 0);
        world.StandTheCarAt(wreck, OffEveryRoadM, 0f);
        world.Apply(new BodyTag(BodyKind.Car, wreck), DamageOutcome.Broken);

        for (var tick = 0; tick < WatchedTicks; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < cars.Count; car++)
            {
                Assert.False(world.Recovery.Wreck[car] == wreck, $"evacuator {car} took wreck {wreck}, which lies beside no road");
            }
        }
    }

    /// <summary>
    /// EVA-5: <b>an arm catches a truck that is already towing</b>, by its nose — the one end of it no fork has — and the
    /// load stays on the bar of the truck it is now behind, so the three are one chain laid under the one in front.
    /// </summary>
    [Fact]
    public void AnArmCatchesATruckThatIsAlreadyTowing()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);
        var cars = world.Cars;
        var trucks = Enumerable.Range(0, cars.Count).Where(each => cars.BuildOf(each).TowReachM > 0f).Take(2).ToArray();
        Assert.True(trucks.Length == 2, "the city stood fewer than two vehicles with an arm");
        var (front, middle) = (trucks[0], trucks[1]);
        var load = Enumerable.Range(0, cars.Count).First(each => cars.BuildOf(each).TowReachM == 0f);

        // In a line facing +x, each set down where the arm of the one ahead of it holds it straight (TowBar.SetDownBehindM).
        var middleM = OffEveryRoadM - new Vector2(TowBar.SetDownBehindM(cars.BuildOf(front), cars.BuildOf(middle)), 0f);
        world.StandTheCarAt(front, OffEveryRoadM, 0f);
        world.StandTheCarAt(middle, middleM, 0f);
        world.StandTheCarAt(load, middleM - new Vector2(TowBar.SetDownBehindM(cars.BuildOf(middle), cars.BuildOf(load)), 0f), 0f);
        Assert.True(world.WorkTheArm(middle));

        Assert.True(world.WorkTheArm(front));

        var hitches = world.Hitches;
        Assert.Equal((middle, false), (hitches.Load(front), hitches.LoadByTheTail(front)));
        Assert.Equal((load, front), (hitches.Load(middle), hitches.FrontOfTheChain(load)));
    }

    /// <summary>Long enough for every car the city stands to be in its bay and at rest.</summary>
    const int WarmUpTicks = 120;

    /// <summary>Ten seconds: an evacuator standing in its bay decides a dozen times over.</summary>
    const int WatchedTicks = 600;

    /// <summary>Somewhere no way of any town reaches.</summary>
    static readonly Vector2 OffEveryRoadM = new(-10_000f, -10_000f);
}
