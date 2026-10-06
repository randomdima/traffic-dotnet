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
    /// it on — bit for bit, over every rebuild of the run.
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
        world.BeforeTheClaimsAreLaid = town =>
        {
            onDevice.Lay(town);
            onDevice.LayTheBodies(town.Cars.Count);
        };
        world.BeforeThePlansAreLaid = town =>
        {
            rebuilds++;
            var claims = town.Occupancy;
            var onTheWays = new Dictionary<int, List<DeviceClaims.ClaimRow>>();
            for (var car = 0; car < town.Cars.Count; car++)
            {
                foreach (var row in onDevice.RowsOf(car)) Listed(onTheWays, (int)row.Way).Add(row);
                if (onDevice.ChannelsOf(car) != town.Cars.Channels[car])
                {
                    differ.Add($"rebuild {rebuilds} car {car}: town on channels {town.Cars.Channels[car]}, device {onDevice.ChannelsOf(car)}");
                }
            }

            var laid = new Dictionary<int, List<DeviceClaims.ClaimRow>>();
            for (var slot = 0; slot < claims.SlotCount; slot++)
            {
                ref readonly var claim = ref claims.Slots[slot];
                if (!claim.HasBody || claim.Of != LaneRoster.Driving || claim.Passing || claims.SlotsGone[slot]) continue;

                Listed(laid, claims.SlotWays[slot]).Add(DeviceClaims.ClaimRow.Of(claim, claims.SlotWays[slot], false, claim.FromM));
            }

            foreach (var (way, town_) in laid)
            {
                bodies += town_.Count;
                var device_ = onTheWays.GetValueOrDefault(way) ?? [];
                if (!town_.OrderBy(Key).SequenceEqual(device_.OrderBy(Key)))
                {
                    differ.Add($"rebuild {rebuilds} way {way}: town [{string.Join(", ", town_)}] device [{string.Join(", ", device_)}]");
                }
            }

            foreach (var (way, device_) in onTheWays)
            {
                if (!laid.ContainsKey(way)) differ.Add($"rebuild {rebuilds} way {way}: town none, device [{string.Join(", ", device_)}]");
            }
        };

        for (var tick = 0; tick < Ticks; tick++) loop.Advance();

        Assert.True(differ.Count == 0, $"{differ.Count} differences over {rebuilds} rebuilds, {bodies} bodies: {string.Join("; ", differ.Take(6))}");
    }

    static List<DeviceClaims.ClaimRow> Listed(Dictionary<int, List<DeviceClaims.ClaimRow>> onTheWays, int way) =>
        onTheWays.TryGetValue(way, out var rows) ? rows : onTheWays[way] = [];

    static (int, float) Key(DeviceClaims.ClaimRow row) => (row.Occupant, row.FromM);
}
