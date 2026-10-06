using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.TrafficLight;

/// <summary>
/// <b>The lights' holds laid on the device</b> (<c>agents/trafficlight/control/kernels/signal_holds.glsl</c>), asked the
/// town's own question: at every claims rebuild of the suite's city, every stretch over the bodies as the town laid them.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class SignalHoldsKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Twenty seconds of the suite's city: every light round more than once, and traffic across its bars.</summary>
    const int Ticks = 1_200;

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Figures(uint StretchCount, uint LaneCount, uint CrossingCount, float CycleS, float AmberTailS, float TimeS);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct StretchRow(uint Way, float FromM, float ToM, int Subject, uint ForCars, float Pad0, float Pad1, float Pad2);

    /// <summary>
    /// <b>Every stretch is held where the town holds it, from its bar to the same body travelling its way, and its hold
    /// laid to end where the town's was</b> — bit for bit, since a light's colour and its stretch are sums and comparisons.
    /// </summary>
    [Fact]
    public void EveryLightHoldsWhatTheTownsHolds()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var lights = world.SignalHolds.Table;
        var table = world.Signals.Table;
        var count = lights.Way.Length;
        var stretchRows = new StretchRow[count];
        for (var at = 0; at < count; at++)
        {
            stretchRows[at] = new StretchRow(
                (uint)lights.Way[at], lights.FromM[at], lights.ToM[at], lights.Subject[at], lights.ForCars[at] ? 1u : 0u, 0f, 0f, 0f);
        }

        var lanes = (uint)table.LaneAxis.Length;
        var crossings = (uint)table.CrossingAxis.Length;
        using var laneAxis = device.Holding<int>(table.LaneAxis);
        using var laneJunction = device.Holding<int>(table.LaneJunction);
        using var offsets = device.Holding<float>(table.OffsetS);
        using var crossingAxis = device.Holding<int>(table.CrossingAxis);
        using var crossingJunction = device.Holding<int>(table.CrossingJunction);
        using var stretches = device.Holding<StretchRow>(stretchRows);
        using var rows = device.Sized<DeviceClaims.ClaimRow>(world.Occupancy.Capacity);
        using var bodyRuns = device.Sized<UInt2>(world.Occupancy.WayCount);
        using var bodyOrder = device.Sized<uint>(world.Occupancy.Capacity);
        using var holdOf = device.Sized<int>(Math.Max(1, count));
        using var held = device.Sized<Vector4>(Math.Max(1, count));
        using var pieces = device.Sized<DeviceClaims.ClaimRow>(Math.Max(1, count));
        GpuBuffer[] buffers = [laneAxis, laneJunction, offsets, crossingAxis, crossingJunction, rows, bodyRuns, bodyOrder, stretches, holdOf, held, pieces];
        using var kernel = new ComputeKernel(device.Vk, "signal_holds.comp", buffers.Length, (uint)Marshal.SizeOf<Figures>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var listed = new int[world.Occupancy.Capacity];

        var differ = new List<string>();
        var rebuilds = 0;
        var holds = 0;
        world.WatchingTheClaims = (town, stage) =>
        {
            if (stage != ClaimsStage.PlansDue) return;

            rebuilds++;
            var claims = town.Occupancy;
            LayTheBodies(claims, rows.Span<DeviceClaims.ClaimRow>(), bodyRuns.Span<UInt2>(), bodyOrder.Span<uint>(), listed);
            var numbered = 0;
            for (var at = 0; at < count; at++)
            {
                var stretch = stretchRows[at];
                var colour = SignalHolds.ColourOf(town.Signals, stretch.Subject, stretch.ForCars != 0u, town.ElapsedS);
                holdOf.Span<int>()[at] = colour == SignalColour.Green ? LaneOccupancy.NoHold : numbered++;
            }

            kernel.Dispatch(batch.Begin(), set, (uint)Math.Max(1, (count + 63) / 64), new Figures(
                (uint)count, lanes, crossings, Config.Signals.CycleS, Config.Signals.AmberTailS, town.ElapsedS));
            batch.SubmitAndWait();
            holds += numbered;
            Compare(claims, holdOf.Span<int>(), held.Span<Vector4>(), pieces.Span<DeviceClaims.ClaimRow>(), count, rebuilds, differ);
        };

        for (var tick = 0; tick < Ticks; tick++) loop.Advance();

        Assert.True(differ.Count == 0, $"{differ.Count} differences over {rebuilds} rebuilds, {holds} holds: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>The town's bodies as rows, and every way's list of them as the town keeps it.</summary>
    static void LayTheBodies(LaneOccupancy claims, Span<DeviceClaims.ClaimRow> rows, Span<UInt2> runs, Span<uint> order, int[] listed)
    {
        for (var slot = 0; slot < claims.SlotCount; slot++)
        {
            rows[slot] = DeviceClaims.ClaimRow.Of(claims.Slots[slot], claims.SlotWays[slot], claims.SlotsGone[slot], claims.Slots[slot].FromM);
        }

        var next = 0;
        for (var way = 0; way < claims.WayCount; way++)
        {
            var count = claims.ListOf(way, bodies: true, listed);
            runs[way] = new UInt2((uint)next, (uint)count);
            for (var at = 0; at < count; at++) order[next + at] = (uint)listed[at];
            next += count;
        }
    }

    static void Compare(
        LaneOccupancy claims, ReadOnlySpan<int> holdOf, ReadOnlySpan<Vector4> held, ReadOnlySpan<DeviceClaims.ClaimRow> pieces, int count,
        int rebuild, List<string> differ)
    {
        for (var stretch = 0; stretch < count; stretch++)
        {
            var hold = holdOf[stretch];
            var byTheDevice = held[stretch];
            if ((hold >= 0) != (byTheDevice.X != 0f))
            {
                differ.Add($"rebuild {rebuild} stretch {stretch}: town holds it {hold >= 0}, device {byTheDevice.X}");
                continue;
            }

            if (hold < 0) continue;

            var endsAtM = claims.HoldEndsAtM(hold, out _, out var cutBy);
            var row = BitConverter.SingleToUInt32Bits(byTheDevice.W);
            var sameCutter = row == DeviceClaims.ClaimAnswer.NoRow ? !cutBy.Found : claims.Slots[(int)row] == cutBy;
            var (first, end) = claims.PiecesOf(hold);
            var placed = byTheDevice.Y != 0f;
            var samePiece = placed == (end > first)
                            && (!placed || pieces[stretch] == DeviceClaims.ClaimRow.Of(claims.Slots[first], claims.SlotWays[first], false, claims.Slots[first].FromM));
            if (endsAtM != byTheDevice.Z || !sameCutter || !samePiece)
            {
                differ.Add($"rebuild {rebuild} stretch {stretch} hold {hold}: town ends {endsAtM} by {cutBy.Occupant} pieces {end - first}; device {byTheDevice} {pieces[stretch]}");
            }
        }
    }
}
