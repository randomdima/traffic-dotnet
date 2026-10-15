namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>Where kernels are recorded</b>: on a page, the batch being written, which keeps every operation in this side's own
/// memory until <see cref="ComputeBatch.Submit"/> hands the lot across in one call. The desktop's is a command buffer.
/// </summary>
internal readonly record struct Recording(ComputeBatch Batch);

/// <summary>
/// <b>A kernel's buffers, bound once</b> (<see cref="ComputeKernel.Bind(ReadOnlySpan{GpuBuffer})"/>): the page's bind
/// group by its id, and the buffers themselves, so a batch knows what a kernel it runs may have written for the host to
/// read, and what the host wrote for it.
/// </summary>
internal readonly record struct BoundSet(int Id, GpuBuffer[] Buffers);
