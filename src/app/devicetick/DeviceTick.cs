using TrafficSimulation.Runtime;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The agents' side of a tick on the device, chained in one submit</b>: the claims rebuild where the claims clock laid
/// one (<see cref="DeviceRebuild"/>), the car tick and its tyres
/// (<see cref="DeviceCarTick"/>) and the walkers' tick (<see cref="DeviceWalkerTick"/>), then the bodies' phase spending
/// the impulses they left — the device's own, not the host's — stepping the solver and reading every car and walker back
/// into the tables the next tick reads (<see cref="DeviceBodyPhase"/>). Nothing crosses back to the host between them.
/// </summary>
/// <remarks>
/// What each part is told before the submit is its own (<see cref="DeviceCarTick.Prepare"/>,
/// <see cref="DeviceWalkerTick.Lay"/>, <see cref="DeviceBodyPhase.Lay"/> with every wheel left to the tyre pass); what it
/// leaves is read as each part reads it. Without walkers the bodies' phase spends what the host lays for them.
/// </remarks>
internal sealed class DeviceTick : IDisposable
{
    readonly Vk _vk;
    readonly DeviceCarTick _cars;
    readonly DeviceBodyPhase _bodies;
    readonly DeviceWalkerTick? _walkers;
    readonly DeviceRebuild? _rebuild;
    readonly ComputeBatch _batch;

    /// <param name="rebuild">
    /// The claims rebuild recorded at the head of a tick whose claims clock laid one (<see cref="DeviceRebuild.Pending"/>),
    /// whose grants the car tick reads — or none, and the car tick reads what it was handed.
    /// </param>
    public DeviceTick(Vk vk, DeviceCarTick cars, DeviceBodyPhase bodies, DeviceWalkerTick? walkers = null, DeviceRebuild? rebuild = null)
    {
        _vk = vk;
        _cars = cars;
        _bodies = bodies;
        _walkers = walkers;
        _rebuild = rebuild;
        _batch = new ComputeBatch(vk);
        if (walkers != null) bodies.TakeTheWalkers(walkers);
        if (rebuild != null) rebuild.Chained = true;
    }

    /// <summary>Whether the last tick's submit carried a claims rebuild.</summary>
    public bool Rebuilt { get; private set; }

    public DeviceCarTick Cars => _cars;

    public DeviceBodyPhase Bodies => _bodies;

    public DeviceWalkerTick? Walkers => _walkers;

    /// <summary>The device's own time for the last tick's kernels, every part's, in milliseconds.</summary>
    public double KernelsMs => RebuildMs + _cars.TickMs + _cars.TyresMs + (_walkers?.TickMs ?? 0d) + _bodies.KernelsMs;

    /// <summary>The device's own time for the last tick's claims rebuild, or nought where it carried none, in milliseconds.</summary>
    public double RebuildMs => Rebuilt ? _rebuild!.BetweenMs(DeviceRebuild.Stage.Sent, DeviceRebuild.Stage.Granted) : 0d;

    /// <summary>
    /// One tick: the car tick for the first <paramref name="taken"/> cars of its list and the tyres for the fleet's first
    /// <paramref name="fleet"/>, the walkers' tick for the first <paramref name="walkers"/>, then their bodies stepped
    /// <paramref name="dtS"/> — one submit and one wait.
    /// </summary>
    /// <param name="readBack">Whether what every part left is handed back to the host.</param>
    public void Step(int taken, int fleet, int walkers, float dtS, bool readBack)
    {
        var commands = _batch.Begin();
        Rebuilt = _rebuild?.Pending == true;
        if (Rebuilt)
        {
            _rebuild!.Record(commands, readBack: false);
            _vk.BetweenKernels(commands);
        }

        _cars.Record(commands, taken, fleet);
        if (readBack) _cars.RecordRead(commands);

        _walkers?.Record(commands, walkers);

        // The bodies' kernels read what the car and walker kernels wrote, and their own sends land on what the tyre pass
        // wrote: one barrier for each.
        _vk.BetweenKernels(commands);
        _vk.BeforeTransfer(commands);
        _bodies.Record(commands, fleet, dtS, readBack);
        if (readBack) _walkers?.RecordRead(commands);

        _batch.SubmitAndWait();
        if (Rebuilt) _rebuild!.ReadTimes();
        _cars.ReadTimes();
        _walkers?.ReadTimes();
        _bodies.ReadTimes();
    }

    public void Dispose() => _batch.Dispose();
}
