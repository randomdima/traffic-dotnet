using System.Numerics;
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
/// <b>The plans answered on the device, asked the town's own question</b>: every claims rebuild of the suite's city
/// handed to both as the town laid it, and the device's lists, answers, cuts and grants held to the town's.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceClaimsTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// Twenty seconds of the suite's city: queues, junctions, crossings and lights all busy at once — the fixture town's
    /// dozen cars never queue, so no grant there is ever cut by a body.
    /// </summary>
    const int Ticks = 1_200;

    /// <summary>
    /// <b>Every way's lists are laid as the town laid them, every hold ends where the town's does, kept off what cut it
    /// by as much, cut on the same way by the same claim, every reservation is left as long as the town left it, and
    /// every car's grant is read as the town read it</b> — bit for bit, since the lists and the answers are comparisons
    /// and sums, and every rebuild of the run is asked.
    /// </summary>
    [Fact]
    public void ThePlansAreAnsweredAsTheTownAnswersThem()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var claims = world.Occupancy;
        using var onDevice = new DeviceClaims(
            device.Vk, claims.WayCount, claims.Capacity, claims.HoldCapacity, Config.Road.MostSettlingPasses, world.Cars.Capacity);

        var differ = new List<string>();
        var rebuilds = 0;
        var listed = new int[claims.Capacity];
        world.WatchingTheClaims = (town, stage) =>
        {
            switch (stage)
            {
                case ClaimsStage.PlansLaid:
                    onDevice.Lay(town.Occupancy);
                    var holders = onDevice.Holders(town.Cars.Count);
                    for (var car = 0; car < town.Cars.Count; car++)
                    {
                        holders[car] = new Vector2(BitConverter.Int32BitsToSingle(town.PlanHoldOf(car)), town.Cars.ClaimFromM[car]);
                    }

                    onDevice.Answer();
                    CompareTheLists(town.Occupancy, onDevice, rebuilds + 1, listed, differ);
                    break;
                case ClaimsStage.PlansAnswered:
                    rebuilds++;
                    Compare(town.Occupancy, onDevice, rebuilds, differ);
                    break;
                case ClaimsStage.GrantsRead:
                    CompareTheGrants(town, onDevice, rebuilds, differ);
                    break;
            }
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

    /// <summary>
    /// Every car's grant as the device read it off its plan's answer, against the town's — but a plan the backing laid
    /// after the answers cut, which the device does not lay yet.
    /// </summary>
    static void CompareTheGrants(TownWorld town, DeviceClaims onDevice, int rebuild, List<string> differ)
    {
        var claims = town.Occupancy;
        var grants = onDevice.Grants;
        for (var car = 0; car < town.Cars.Count; car++)
        {
            var hold = town.PlanHoldOf(car);
            if (!claims.IsAnswered(hold)) continue;

            claims.HoldEndsAtM(hold, out _, out var cutBy);
            if (cutBy.Found && cutBy.Priority == ClaimPriority.Backing) continue;

            var grant = grants[car];
            var cars = town.Cars;
            if (grant.X != cars.AuthorityM[car] || grant.Y != cars.GrantMarginM[car] || (uint)grant.Z != (uint)cars.GrantCutBy[car])
            {
                differ.Add($"rebuild {rebuild} car {car}: town {cars.AuthorityM[car]} kept {cars.GrantMarginM[car]} by {cars.GrantCutBy[car]}; device {grant}");
            }
        }
    }

    /// <summary>Every way's two lists as the device laid them from the rows, against the town's as it laid them.</summary>
    static void CompareTheLists(LaneOccupancy claims, DeviceClaims onDevice, int rebuild, int[] listed, List<string> differ)
    {
        for (var way = 0; way < claims.WayCount; way++)
        {
            foreach (var bodies in (ReadOnlySpan<bool>)[true, false])
            {
                var count = claims.ListOf(way, bodies, listed);
                var laid = onDevice.ListOf(way, bodies);
                var same = laid.Length == count;
                for (var at = 0; same && at < count; at++) same = laid[at] == (uint)listed[at];
                if (!same)
                {
                    differ.Add($"rebuild {rebuild} way {way} {(bodies ? "bodies" : "planned")}: town [{string.Join(",", listed[..count])}] device [{string.Join(",", laid.ToArray())}]");
                }
            }
        }
    }

    /// <summary>Whether a row is the claim the town says cut a hold: one holder's, of one rung, from one metre.</summary>
    static bool SameClaim(in LaneClaim claim, in DeviceClaims.ClaimRow row) =>
        claim.Occupant == row.Occupant && claim.Hold == row.Hold && claim.FromM == row.FromM && (uint)claim.Priority == (row.Flags & 0xF);
}
