namespace TrafficSimulation.Runtime;

/// <summary>
/// What the device itself spent, off timestamps written on it: the frames' render passes and the kernels' batches whose
/// timings landed, each summed, with how many there were — and whether the device is timed at all.
/// </summary>
/// <remarks>
/// <b>A count beside each sum, because not every frame is timed</b>: a measurement is read back when the device is done
/// and is never waited for, so a window's mean is its sum over its own count and not over its frames.
/// </remarks>
internal readonly record struct GpuTimes(double DrawMs, int Draws, double KernelsMs, int Batches, GpuClock Clock)
{
    public static GpuTimes operator +(GpuTimes a, GpuTimes b) => new(
        a.DrawMs + b.DrawMs, a.Draws + b.Draws, a.KernelsMs + b.KernelsMs, a.Batches + b.Batches,
        (GpuClock)Math.Max((int)a.Clock, (int)b.Clock));
}

/// <summary>Whether a head times its device: never asked, asked and refused, or timed.</summary>
internal enum GpuClock
{
    /// <summary>The head takes no timestamps (the desktop's).</summary>
    None,

    /// <summary>A page whose browser offers no <c>timestamp-query</c>: a row that says so, rather than no row.</summary>
    Refused,

    Timed,
}
