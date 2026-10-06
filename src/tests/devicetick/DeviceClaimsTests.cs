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
/// <b>The plans answered on the device, asked the town's own question</b>: every claims rebuild of the fixture town
/// handed to both as the town laid it, and the device's answers and cuts held to the town's.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceClaimsTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Twenty seconds of the fixture town: queues, junctions, crossings and lights all busy at once.</summary>
    const int Ticks = 1_200;

    /// <summary>
    /// <b>Every hold ends where the town's does, kept off what cut it by as much, cut on the same way by the same
    /// claim</b>, and every reservation is left as long as the town left it — bit for bit, since the answers are
    /// comparisons and sums, and every rebuild of the run is asked.
    /// </summary>
    [Fact]
    public void ThePlansAreAnsweredAsTheTownAnswersThem()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var claims = world.Occupancy;
        using var onDevice = new DeviceClaims(device.Vk, claims.WayCount, claims.Capacity, claims.HoldCapacity, Config.Road.MostSettlingPasses);

        var differ = new List<string>();
        var rebuilds = 0;
        world.BeforeThePlansAreAnswered = town =>
        {
            onDevice.Lay(town.Occupancy);
            onDevice.Answer();
        };
        world.AfterThePlansAreAnswered = town =>
        {
            rebuilds++;
            Compare(town.Occupancy, onDevice, rebuilds, differ);
        };

        for (var tick = 0; tick < Ticks; tick++) loop.Advance();

        Assert.True(differ.Count == 0, $"{differ.Count} differences over {rebuilds} rebuilds: {string.Join("; ", differ.Take(6))}");
    }

    static void Compare(LaneOccupancy claims, DeviceClaims onDevice, int rebuild, List<string> differ)
    {
        var answers = onDevice.Answers;
        var rows = onDevice.Rows;
        for (var hold = 0; hold < claims.HoldCount; hold++)
        {
            if (!claims.IsAnswered(hold)) continue;

            var endsAtM = claims.HoldEndsAtM(hold, out var marginM, out var cutBy);
            ref readonly var answer = ref answers[hold];
            var sameCutter = answer.CutBy == DeviceClaims.ClaimAnswer.NoRow || SameClaim(cutBy, rows[(int)answer.CutBy]);
            if (endsAtM != answer.CutLineM || marginM != answer.MarginM || claims.HoldCutOn(hold) != answer.CutOn || !sameCutter)
            {
                differ.Add($"rebuild {rebuild} hold {hold}: town {endsAtM} kept {marginM} on {claims.HoldCutOn(hold)} by {cutBy.Occupant}; device {answer}");
            }
        }

        var slots = claims.Slots;
        var gone = claims.SlotsGone;
        for (var slot = 0; slot < slots.Length; slot++)
        {
            if (gone[slot] != rows[slot].IsGone || (!gone[slot] && slots[slot].ToM != rows[slot].ToM))
            {
                differ.Add($"rebuild {rebuild} slot {slot}: town {slots[slot].ToM} gone {gone[slot]}; device {rows[slot].ToM} gone {rows[slot].IsGone}");
            }
        }
    }

    /// <summary>Whether a row is the claim the town says cut a hold: one holder's, of one rung, from one metre.</summary>
    static bool SameClaim(in LaneClaim claim, in DeviceClaims.ClaimRow row) =>
        claim.Occupant == row.Occupant && claim.Hold == row.Hold && claim.FromM == row.FromM && (uint)claim.Priority == (row.Flags & 0xF);
}
