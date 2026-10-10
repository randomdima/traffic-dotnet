using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A town handed back only the poses it reads goes as one handed back every pose</b> (<see cref="DeviceTownBodies.HandsBackEveryPose"/>):
/// two device towns of one plan, one handed every pose back after every step and one only what it reads, held to each
/// other's device every tick — so a pose the town read and was not handed shows as the two parting.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceTownPosesTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// The suite's built town over half a minute, its parked cars shoved into each other once they have stood their rest
    /// time and then as long again: crashes, wrecks, the people thrown clear, and what comes for them.
    /// </summary>
    [Fact]
    public void AShovedCarParkGoesAsIfEveryPoseWereRead()
    {
        var apart = Parted(Towns.Built, ticks: 1_800, shoveAt: ShovedCarPark.ShoveAtTicks, walkersLeft: true);
        Assert.True(apart == null, apart);
    }

    /// <summary>A whole city touring for half a minute: the cars it drives and parks.</summary>
    [Fact]
    public void ATouringCityGoesAsIfEveryPoseWereRead()
    {
        var apart = Parted(Towns.Of(Towns.City), ticks: 1_800, shoveAt: -1, walkersLeft: false);
        Assert.True(apart == null, apart);
    }

    /// <summary>
    /// The first tick and agent the two towns' devices left apart, or null where they never parted — but for a run that
    /// left no car's pose unread, or no walker's where <paramref name="walkersLeft"/>, which asked nothing.
    /// </summary>
    string? Parted(CityPlan plan, int ticks, int shoveAt, bool walkersLeft)
    {
        using var every = new TownWorld(plan, Config);
        using var some = new TownWorld(plan, Config);
        var everyLoop = new SimLoop<TownWorld>(every, Config);
        var someLoop = new SimLoop<TownWorld>(some, Config);
        using var everyDevice = new DeviceTown(device.Vk, Config, every);
        using var someDevice = new DeviceTown(device.Vk, Config, some);
        everyDevice.Bodies.HandsBackEveryPose = true;
        someDevice.Bodies.PoisonsWhatIsUnread = true;
        every.Device = everyDevice;
        some.Device = someDevice;
        var (carsUnread, walkersUnread) = (0L, 0L);
        try
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                if (tick == shoveAt) TwoTowns.ShoveTheParkedCars(every, every, some);

                everyLoop.Advance();
                someLoop.Advance();
                if (Apart(tick, everyDevice.Bodies.Phase, someDevice.Bodies.Phase, some) is { } apart) return apart;

                carsUnread += some.Cars.Count - someDevice.Bodies.CarsRead;
                walkersUnread += someDevice.Bodies.Phase.WalkersAfter.Length - someDevice.Bodies.WalkersRead;
            }

            return carsUnread > 0 && (!walkersLeft || walkersUnread > 0)
                ? null
                : $"too little was left unread: {carsUnread} car-ticks, {walkersUnread} walker-ticks";
        }
        finally
        {
            every.Device = null;
            some.Device = null;
        }
    }

    static string? Apart(int tick, DeviceBodyPhase every, DeviceBodyPhase some, TownWorld town)
    {
        var everyCars = every.PosesAfter;
        var someCars = some.PosesAfter;
        for (var car = 0; car < everyCars.Length; car++)
        {
            if (everyCars[car] != someCars[car]) return $"tick {tick} car {car}: {everyCars[car]} read whole, {someCars[car]} read in part";
        }

        var everyWalkers = every.WalkersAfter;
        var someWalkers = some.WalkersAfter;
        for (var person = 0; person < everyWalkers.Length; person++)
        {
            if (town.People.Inside[person].Any || everyWalkers[person] == someWalkers[person]) continue;

            return $"tick {tick} walker {person}: {everyWalkers[person]} read whole, {someWalkers[person]} read in part";
        }

        return null;
    }
}
