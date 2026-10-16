using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The lanes and joins a car is read along, on the device</b> (<c>world/road/kernels/line_reading.glsl</c>): the road's
/// own tables (<see cref="RoadGraph.Tables"/>), laid once with the world and never sent again, since nothing in them
/// changes while the town runs.
/// </summary>
internal sealed class DeviceLanes : IDisposable
{
    /// <summary>One arc as the device reads it (<c>LaneArc</c>): <see cref="ArcSeg"/>'s figures and its direction, padded to 32 B.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ArcRow(Vector2 StartM, Vector2 StartUnit, float HeadingRad, float LengthM, float Curvature, float Pad)
    {
        public static ArcRow Of(in ArcSeg arc) => new(arc.StartM, arc.StartUnit, arc.HeadingRad, arc.LengthM, arc.Curvature, 0f);
    }

    readonly DeviceHoldings _held;

    /// <summary>
    /// In <c>line_reading.glsl</c>'s order: the lanes' arcs then the joins', and the words — where each lane's arcs begin,
    /// the lanes' lengths, where each join's begin and where each join leads, under a head naming where each table and the
    /// joins' first arc stand (<c>LineWords</c>). Two bindings where six would take more than WebGPU gives a kernel.
    /// </summary>
    public GpuBuffer[] Tables { get; }

    /// <summary>How many words the head of the words takes: a table's start each, and the joins' first arc.</summary>
    const int HeadWords = 5;

    public unsafe DeviceLanes(Vk vk, RoadGraph roads)
    {
        _held = new DeviceHoldings(vk);
        var (laneArcs, laneArcFrom, joinArcs, joinArcFrom, joinTo) = roads.Tables;
        var arcs = _held.Room<ArcRow>(laneArcs.Length + joinArcs.Length);
        arcs.Upload<ArcSeg, ArcRow>(laneArcs.Span, &ArcRow.Of);
        arcs.Upload<ArcSeg, ArcRow>(joinArcs.Span, &ArcRow.Of, (ulong)(laneArcs.Length * Marshal.SizeOf<ArcRow>()));

        var lengths = MemoryMarshal.Cast<float, int>(roads.LaneLengthM.AsSpan());
        Span<int> head =
        [
            HeadWords, HeadWords + laneArcFrom.Length, HeadWords + laneArcFrom.Length + lengths.Length,
            HeadWords + laneArcFrom.Length + lengths.Length + joinArcFrom.Length, laneArcs.Length,
        ];
        var words = _held.Room<int>(head[3] + joinTo.Length);
        Upload(words, (ReadOnlySpan<int>)head, 0);
        Upload(words, laneArcFrom, head[0]);
        Upload(words, lengths, head[1]);
        Upload(words, joinArcFrom, head[2]);
        Upload(words, joinTo, head[3]);
        Tables = [arcs, words];
    }

    static void Upload(GpuBuffer words, ReadOnlySpan<int> rows, int atWord) =>
        words.Upload(rows, (ulong)atWord * sizeof(int));

    public void Dispose() => _held.Dispose();
}
