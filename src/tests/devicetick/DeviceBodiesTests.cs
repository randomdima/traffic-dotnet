using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>Every car's body laid on the device, asked the town's own question</b>: each claims rebuild of the fixture town,
/// the cars handed over as the rebuild begins, and the bodies laid held to the town's.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceBodiesTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Twenty seconds of the fixture town: cars parked, pulling out, queueing and turning.</summary>
    const int Ticks = 1_200;

    /// <summary>
    /// <b>Every car's body lies over the ways the town lays it over, over the same stretches, travelling them as fast
    /// and on to the same next way, at rest where the town's is</b>, and every car is left on the channels the town put
    /// it on with the same wheels on a way (TER-7b) — bit for bit, over every rebuild of the run.
    /// </summary>
    /// <remarks>Way by way, since nothing in the fixture town is towed or on duty, so no car's body is grown into another's.</remarks>
    [Fact]
    public void EveryCarsBodyIsLaidAsTheTownLaysIt()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        using var onDevice = new DeviceBodies(device.Vk, Config, world);

        var differ = new List<string>();
        var rebuilds = 0;
        var bodies = 0;
        world.WatchingTheClaims = (town, stage) =>
        {
            switch (stage)
            {
                case ClaimsStage.Begun:
                    onDevice.Lay(town);
                    onDevice.LayTheBodies(town.Cars.Count);
                    break;
                case ClaimsStage.PlansDue:
                    rebuilds++;
                    bodies += Compare(town, onDevice, rebuilds, differ);
                    break;
            }
        };

        for (var tick = 0; tick < Ticks; tick++) loop.Advance();

        Assert.True(differ.Count == 0, $"{differ.Count} differences over {rebuilds} rebuilds, {bodies} bodies: {string.Join("; ", differ.Take(6))}");
    }

    /// <returns>How many car bodies the town laid.</returns>
    static int Compare(TownWorld town, DeviceBodies onDevice, int rebuild, List<string> differ)
    {
        var claims = town.Occupancy;
        var onTheWays = new Dictionary<int, List<DeviceClaims.ClaimRow>>();
        for (var car = 0; car < town.Cars.Count; car++)
        {
            foreach (var row in onDevice.RowsOf(car)) Listed(onTheWays, (int)row.Way).Add(row);
            if (onDevice.ChannelsOf(car) != town.Cars.Channels[car])
            {
                differ.Add($"rebuild {rebuild} car {car}: town on channels {town.Cars.Channels[car]}, device {onDevice.ChannelsOf(car)}");
            }

            if (onDevice.WheelsOnAWayOf(car) != town.Cars.WheelsOnAWay[car])
            {
                differ.Add($"rebuild {rebuild} car {car}: town's wheels on a way {town.Cars.WheelsOnAWay[car]}, device's {onDevice.WheelsOnAWayOf(car)}");
            }
        }

        var laid = new Dictionary<int, List<DeviceClaims.ClaimRow>>();
        for (var slot = 0; slot < claims.SlotCount; slot++)
        {
            ref readonly var claim = ref claims.Slots[slot];
            if (!claim.HasBody || claim.Of != LaneRoster.Driving || claim.Passing || claims.SlotsGone[slot]) continue;

            Listed(laid, claims.SlotWays[slot]).Add(DeviceClaims.ClaimRow.Of(claim, claims.SlotWays[slot], false, claim.FromM));
        }

        var bodies = 0;
        foreach (var (way, byTheTown) in laid)
        {
            bodies += byTheTown.Count;
            var byTheDevice = onTheWays.GetValueOrDefault(way) ?? [];
            if (!byTheTown.OrderBy(Key).SequenceEqual(byTheDevice.OrderBy(Key)))
            {
                differ.Add($"rebuild {rebuild} way {way}: town [{string.Join(", ", byTheTown)}] device [{string.Join(", ", byTheDevice)}]");
            }
        }

        foreach (var (way, byTheDevice) in onTheWays)
        {
            if (!laid.ContainsKey(way)) differ.Add($"rebuild {rebuild} way {way}: town none, device [{string.Join(", ", byTheDevice)}]");
        }

        return bodies;
    }

    static List<DeviceClaims.ClaimRow> Listed(Dictionary<int, List<DeviceClaims.ClaimRow>> onTheWays, int way) =>
        onTheWays.TryGetValue(way, out var rows) ? rows : onTheWays[way] = [];

    static (int, float) Key(DeviceClaims.ClaimRow row) => (row.Occupant, row.FromM);
}
