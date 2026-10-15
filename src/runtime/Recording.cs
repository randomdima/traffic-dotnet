using Silk.NET.Vulkan;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>Where kernels are recorded</b>: on the desktop a command buffer, which is all this is. The device world is one
/// source for both heads, so it names this and never Vulkan's type; a page's is <c>Recording.Web.cs</c>.
/// </summary>
internal readonly record struct Recording(CommandBuffer Buffer)
{
    public static implicit operator Recording(CommandBuffer buffer) => new(buffer);

    public static implicit operator CommandBuffer(Recording recording) => recording.Buffer;
}

/// <summary>
/// <b>A kernel's buffers, bound once</b> (<see cref="ComputeKernel.Bind(ReadOnlySpan{GpuBuffer})"/>): on the desktop a
/// descriptor set, named here for the reason <see cref="Recording"/> is.
/// </summary>
internal readonly record struct BoundSet(DescriptorSet Set)
{
    public static implicit operator BoundSet(DescriptorSet set) => new(set);

    public static implicit operator DescriptorSet(BoundSet bound) => bound.Set;
}
