using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using Vk = TrafficSimulation.Runtime.Vk;

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

    readonly Vk _vk;
    readonly List<GpuBuffer> _owned = [];

    /// <summary>In <c>line_reading.glsl</c>'s order: the lanes' arcs, where each begins, their lengths, the joins' arcs, where each begins, and where each leads.</summary>
    public GpuBuffer[] Tables { get; }

    public DeviceLanes(Vk vk, RoadGraph roads)
    {
        _vk = vk;
        var (laneArcs, laneArcFrom, joinArcs, joinArcFrom, joinTo) = roads.Tables;
        Tables =
        [
            LaidArcs(laneArcs.Span), Laid<int>(laneArcFrom), Laid<float>(roads.LaneLengthM), LaidArcs(joinArcs.Span),
            Laid<int>(joinArcFrom), Laid<int>(joinTo),
        ];
    }

    GpuBuffer Laid<T>(ReadOnlySpan<T> rows) where T : unmanaged
    {
        var buffer = Room<T>(rows.Length);
        buffer.Upload(rows, PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    unsafe GpuBuffer LaidArcs(ReadOnlySpan<ArcSeg> arcs)
    {
        var buffer = Room<ArcRow>(arcs.Length);
        buffer.Upload<ArcSeg, ArcRow>(arcs, &ArcRow.Of, PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    GpuBuffer Room<T>(int count) where T : unmanaged
    {
        var buffer = _vk.CreateBuffer(
            (ulong)(Math.Max(1, count) * Marshal.SizeOf<T>()),
            BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None);
        _owned.Add(buffer);
        return buffer;
    }

    public void Dispose()
    {
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
