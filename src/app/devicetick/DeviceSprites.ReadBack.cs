using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The emit read back for an instrument</b> (<c>--bench deviceworld</c>, the device tier): the desktop's alone, since it
/// waits on the device for the runs and the passes' times, which a page cannot (WEB-12).
/// </summary>
internal sealed partial class DeviceSprites
{
    /// <summary>A stamp before the emit's first pass and one after each of its five.</summary>
    const int PassStamps = 6;

    /// <summary>The device's own time each pass of an emit took, in milliseconds.</summary>
    internal readonly record struct PassesMs(double Lay, double Count, double Scan, double Write, double Order);

    /// <summary>What each pass of the last <see cref="Emit"/> took on the device.</summary>
    public PassesMs EmitMs { get; private set; }

    /// <summary>The slot an emit read back is steered by, apart from every frame's, laid by the first.</summary>
    Slot? _readSlot;

    ComputeBatch? _readBatch;

    GpuTimer? _timer;

    /// <summary>Both runs and their draws as an emit read back left them, laid by the first.</summary>
    GpuBuffer? _instancesOut, _drawsOut;

    /// <summary>Whether the emit being recorded is the one <see cref="Emit"/> times.</summary>
    bool _stamping;

    /// <summary>How many instances the last emit read back wrote into the over run, and into the run above.</summary>
    public int OverCount => (int)DrawnBack(0).InstanceCount;

    public int AboveCount => (int)DrawnBack(1).InstanceCount;

    /// <summary>The over run as the last emit read back wrote it.</summary>
    public ReadOnlySpan<SpriteInstance> Over => ReadBack.Span<SpriteInstance>()[..OverCount];

    /// <summary>And the run above, laid past the over run's room.</summary>
    public ReadOnlySpan<SpriteInstance> Above => ReadBack.Span<SpriteInstance>().Slice(_room.Over, AboveCount);

    GpuBuffer ReadBack => _instancesOut ?? throw new InvalidOperationException("No emit has read its runs back.");

    DrawRow DrawnBack(int run) =>
        (_drawsOut ?? throw new InvalidOperationException("No emit has read its runs back.")).Span<DrawRow>()[run];

    /// <summary>
    /// <b>The two runs emitted for one view</b> — the one the fill is handed (<see cref="TownSprites.FillMoving"/>) — and
    /// read back with their counts, and the passes timed (<see cref="EmitMs"/>).
    /// </summary>
    public void Emit(TownWorld town, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        var batch = _readBatch ??= new ComputeBatch(_vk);
        batch.Wait();
        var at = _readSlot ??= Lay();
        Write(at, town, viewCentreM, viewSpanM, pixelsPerMetre, lay: true);
        var commands = batch.Begin();
        _timer ??= new GpuTimer(_vk, PassStamps);
        _stamping = true;
        RecordTheEmit(commands, at);
        _stamping = false;
        _vk.BeforeTransfer(commands);
        _instancesOut ??= _held.Readback<SpriteInstance>((int)(_instances.SizeBytes / GpuBuffer.Bytes<SpriteInstance>(1)));
        _drawsOut ??= _held.Readback<DrawRow>(2);
        _vk.Copy(commands, _instances, _instancesOut, _instances.SizeBytes);
        _vk.Copy(commands, _draws, _drawsOut, _draws.SizeBytes);
        batch.SubmitAndWait();
        _timer.Read();
        EmitMs = new PassesMs(_timer.BetweenMs(0, 1), _timer.BetweenMs(1, 2), _timer.BetweenMs(2, 3), _timer.BetweenMs(3, 4), _timer.BetweenMs(4, 5));
    }

    partial void Stamp(Recording commands, int pass)
    {
        if (!_stamping) return;

        if (pass == 0) _timer!.Reset(commands);
        _timer!.Stamp(commands, pass);
    }

    partial void DisposeTheReadBack()
    {
        _readBatch?.Dispose();
        _timer?.Dispose();
    }
}
