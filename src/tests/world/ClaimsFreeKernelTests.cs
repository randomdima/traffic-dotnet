using System.Runtime.InteropServices;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>Whether a pass's ground is free, asked on the device</b> (<c>world/road/kernels/claims_free.glsl</c>, a switching
/// car's look for room): a way's lists as the device files them hold a piece a later cut took out (<c>ClaimGone</c>),
/// which the host's <see cref="LaneOccupancy.IsFreeForAPass(int, float, float, float, float, int, LaneRoster, ReadOnlySpan{LaneClaim}, in PassTerms)"/>
/// no longer has in its list at all.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P2)]
public class ClaimsFreeKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct PassAsk(
        uint Way, float SweptFromM, float SweptToM, float HeldFromM, float HeldToM, int Occupant, uint Roster, uint Pad);

    const uint NoRow = uint.MaxValue;

    /// <summary>Who asks: a car stepping across over [5, 10) of way 0.</summary>
    static readonly PassAsk Asking = new(0u, 5f, 10f, 5f, 10f, Occupant: 3, (uint)LaneRoster.Driving, 0u);

    /// <summary>Another car's body over the step, and a third car's plan over it, as the way's two lists hold them.</summary>
    static DeviceClaims.ClaimRow[] InTheWay(bool gone) =>
    [
        DeviceClaims.ClaimRow.Of(new LaneClaim(6f, 9f, 0f, Occupant: 9, ClaimPriority.Hard), way: 0, gone, standsToM: 6f),
        DeviceClaims.ClaimRow.Of(new LaneClaim(0f, 20f, 8f, Occupant: 7, ClaimPriority.Committed), way: 0, gone, standsToM: 0f),
    ];

    uint Refusing(DeviceClaims.ClaimRow[] rows)
    {
        using var claimRows = device.Holding<DeviceClaims.ClaimRow>(rows);
        using var bodyRuns = device.Holding<UInt2>([new UInt2(0u, 1u)]);
        using var plannedRuns = device.Holding<UInt2>([new UInt2(1u, 1u)]);
        using var order = device.Holding<uint>([0u, 1u]);
        using var asks = device.Holding<PassAsk>([Asking]);
        using var refusing = device.Sized<uint>(1);
        GpuBuffer[] buffers = [claimRows, bodyRuns, plannedRuns, order, asks, refusing];
        using var kernel = new ComputeKernel(device.Vk, "claims_free_asks.comp", buffers.Length, sizeof(uint));
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        kernel.Dispatch(batch.Begin(), set, 1u, 1u);
        batch.SubmitAndWait();
        return refusing.Span<uint>()[0];
    }

    [Fact]
    public void APieceACutTookOutRefusesNoPass() => Assert.Equal(NoRow, Refusing(InTheWay(gone: true)));

    [Fact]
    public void TheSamePiecesLeftInRefuseIt() => Assert.Equal(0u, Refusing(InTheWay(gone: false)));
}
