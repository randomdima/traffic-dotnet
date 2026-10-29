namespace TrafficSimulation.Runtime;

/// <summary>
/// What the device itself spent, off timestamps written on it: the frames' render passes and the kernels' batches whose
/// timings landed, each summed, with how many there were. A head that takes none answers nought of each.
/// </summary>
/// <remarks>
/// <b>A count beside each sum, because not every frame is timed</b>: a measurement is read back when the device is done
/// and is never waited for, so a window's mean is its sum over its own count and not over its frames.
/// </remarks>
internal readonly record struct GpuTimes(double DrawMs, int Draws, double KernelsMs, int Batches)
{
    public static GpuTimes operator +(GpuTimes a, GpuTimes b) =>
        new(a.DrawMs + b.DrawMs, a.Draws + b.Draws, a.KernelsMs + b.KernelsMs, a.Batches + b.Batches);
}
