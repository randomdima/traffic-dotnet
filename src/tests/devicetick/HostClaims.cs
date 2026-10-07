using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>The claims as the town cut them, laid the way a kernel reading them after their cut binds them.</summary>
internal static class HostClaims
{
    /// <summary>Every slot a row, and every way's planned pieces as the town keeps them, in its order.</summary>
    /// <param name="listed">Scratch as long as the claims' capacity.</param>
    public static void Lay(LaneOccupancy claims, Span<DeviceClaims.ClaimRow> rows, Span<UInt2> runs, Span<uint> order, int[] listed)
    {
        for (var slot = 0; slot < claims.SlotCount; slot++)
        {
            rows[slot] = DeviceClaims.ClaimRow.Of(claims.Slots[slot], claims.SlotWays[slot], claims.SlotsGone[slot], claims.Slots[slot].FromM);
        }

        var next = 0;
        for (var way = 0; way < claims.WayCount; way++)
        {
            var count = claims.ListOf(way, bodies: false, listed);
            runs[way] = new UInt2((uint)next, (uint)count);
            for (var at = 0; at < count; at++) order[next + at] = (uint)listed[at];
            next += count;
        }
    }
}
