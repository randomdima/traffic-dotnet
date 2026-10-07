using TrafficSimulation.Runtime;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The cars' side of a tick on the device, chained in one submit</b>: the car tick and its tyres
/// (<see cref="DeviceCarTick"/>), then the bodies' phase spending the impulses the tyres left — the device's own, not
/// the host's — stepping the solver and reading every car back into the poses the next tick reads
/// (<see cref="DeviceBodyPhase"/>). Nothing crosses back to the host between the two.
/// </summary>
/// <remarks>
/// What each half is told before the submit is its own (<see cref="DeviceCarTick.Prepare"/>,
/// <see cref="DeviceBodyPhase.Lay"/>, with every wheel left to the tyre pass); what it leaves is read as each half reads
/// it.
/// </remarks>
internal sealed class DeviceTick(Vk vk, DeviceCarTick cars, DeviceBodyPhase bodies) : IDisposable
{
    readonly ComputeBatch _batch = new(vk);

    public DeviceCarTick Cars => cars;

    public DeviceBodyPhase Bodies => bodies;

    /// <summary>The device's own time for the last tick's kernels, both halves', in milliseconds.</summary>
    public double KernelsMs => cars.TickMs + cars.TyresMs + bodies.KernelsMs;

    /// <summary>
    /// One tick: the car tick for the first <paramref name="taken"/> cars of its list and the tyres for the fleet's first
    /// <paramref name="fleet"/>, then their bodies stepped <paramref name="dtS"/> — one submit and one wait.
    /// </summary>
    /// <param name="readBack">Whether what both halves left is handed back to the host.</param>
    public void Step(int taken, int fleet, float dtS, bool readBack)
    {
        var commands = _batch.Begin();
        cars.Record(commands, taken, fleet);
        if (readBack) cars.RecordRead(commands);

        vk.BeforeTransfer(commands);
        bodies.Record(commands, fleet, dtS, readBack);
        _batch.SubmitAndWait();
        cars.ReadTimes();
        bodies.ReadTimes();
    }

    public void Dispose() => _batch.Dispose();
}
