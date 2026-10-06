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
/// <b>Every car's claim laid on the device, asked the town's own question</b>: each claims rebuild of the fixture town,
/// the cars handed over as they stand before their claims are laid, and what each came to held to what the town laid.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DevicePlansTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Twenty seconds of the fixture town: cars pulling away, queueing, turning and crossing.</summary>
    const int Ticks = 1_200;

    /// <summary>
    /// <b>Every car's claim reaches as far as the town's, is committed as far, is held short where the town's is, and
    /// lays the same pieces</b> — the same ways, stretches, rungs, arrivals and secondary claims, in the same order — bit
    /// for bit, over every rebuild of the run. A car on a pass is the host's still.
    /// </summary>
    [Fact]
    public void EveryCarsPlanIsLaidAsTheTownLaysIt()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        using var onDevice = new DevicePlans(device.Vk, Config, world);

        var differ = new List<string>();
        var rebuilds = 0;
        var planned = 0;
        world.BeforeThePlansAreLaid = town =>
        {
            onDevice.Lay(town);
            onDevice.Plan(town.Cars.Count);
        };
        world.BeforeThePlansAreAnswered = town =>
        {
            rebuilds++;
            for (var car = 0; car < town.Cars.Count; car++)
            {
                if (town.Cars.Pass[car].Begun) continue;

                planned += Compare(town, onDevice, car, rebuilds, differ) ? 1 : 0;
            }
        };

        for (var tick = 0; tick < Ticks; tick++) loop.Advance();

        Assert.True(differ.Count == 0, $"{differ.Count} differences over {rebuilds} rebuilds, {planned} plans: {string.Join("; ", differ.Take(6))}");
    }

    /// <returns>Whether the car laid a plan.</returns>
    static bool Compare(TownWorld town, DevicePlans onDevice, int car, int rebuild, List<string> differ)
    {
        var cars = town.Cars;
        var result = onDevice.Results[car];
        if (result.ClaimFromM != cars.ClaimFromM[car] || result.ClaimToM != cars.ClaimToM[car]
            || result.CommittedToM != cars.CommittedToM[car] || result.HorizonM != cars.HorizonM[car])
        {
            differ.Add($"rebuild {rebuild} car {car}: town {cars.ClaimFromM[car]}..{cars.ClaimToM[car]} committed {cars.CommittedToM[car]} horizon {cars.HorizonM[car]}; device {result}");
            return false;
        }

        var claims = town.Occupancy;
        var hold = town.PlanHoldOf(car);
        var laid = claims.IsAnswered(hold);
        if (laid != (result.LaidAHold != 0u))
        {
            differ.Add($"rebuild {rebuild} car {car}: town laid a plan {laid}, device {result.LaidAHold}");
            return false;
        }

        if (!laid) return false;

        var (first, end) = claims.PiecesOf(hold);
        var pieces = onDevice.PiecesOf(car);
        if (pieces.Length != end - first)
        {
            differ.Add($"rebuild {rebuild} car {car}: town {end - first} pieces, device {pieces.Length}");
            return true;
        }

        var terms = claims.TermsOf(hold);
        if (terms.PassesBelowMps != result.PassesBelowMps && terms.OnACall)
        {
            differ.Add($"rebuild {rebuild} car {car}: town passes below {terms.PassesBelowMps}, device {result.PassesBelowMps}");
        }

        for (var at = 0; at < pieces.Length; at++)
        {
            var slot = first + at;
            var expected = DeviceClaims.ClaimRow.Of(
                claims.Slots[slot], claims.SlotWays[slot], false,
                claims.Slots[slot].Secondary ? claims.Slots[slot].FromM : claims.SlotStandsToM[slot]);
            if (pieces[at] with { Hold = expected.Hold } != expected)
            {
                differ.Add($"rebuild {rebuild} car {car} piece {at}: town {expected}; device {pieces[at]}");
                break;
            }
        }

        return true;
    }
}
