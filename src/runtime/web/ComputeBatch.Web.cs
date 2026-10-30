using System.Runtime.InteropServices;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>Kernels run with no frame to ride, in a page</b>: the browser's half of <c>ComputeBatch.cs</c>. A batch is written
/// as words into this side's own memory — a dispatch, a copy, a fill — and crosses to the page once, on
/// <see cref="Submit"/>, with the push constants beside it; the page records it, submits it, and copies back what the host
/// reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>A page cannot wait</b>, so nothing here blocks: <see cref="Wait"/> is nothing, and what a batch hands back is the
/// host's to read once <see cref="Landed"/> — which the page says by writing the batch's serial into memory of this
/// side's that it was told of, between two frames and with no crossing. Whoever reads must ask first
/// (<c>WorldLayer.Ready</c>).
/// </para>
/// <para>
/// <b>What a batch touched is what crosses</b>: every buffer a dispatch binds, a copy reads or writes or a fill fills.
/// A touched buffer the host writes is sent whole ahead of the batch; one the host reads is copied back whole after it,
/// where the batch wrote it (<c>compute.js</c>) — one only read holds what the batch that wrote it last landed.
/// </para>
/// </remarks>
internal sealed class ComputeBatch : IDisposable
{
    /// <summary>
    /// How far apart two dispatches' push constants stand in the page's uniform buffer: WebGPU's least offset alignment,
    /// and more than any kernel's block.
    /// </summary>
    const int PushStride = 256;

    /// <summary>The batch's operations, as <c>town.js</c> reads them: a code, then its words.</summary>
    const int DispatchCode = 1, DispatchIndirectCode = 2, CopyCode = 3, FillCode = 4, SendCode = 5, ReadCode = 6;

    int[] _operations = new int[1024];
    int _words;
    byte[] _push = new byte[PushStride * 64];
    int _pushBytes;
    GpuBuffer[] _touched = new GpuBuffer[64];
    int _touchedCount;

    /// <summary>The serial the page writes once a batch's copies back have landed: pinned, so it writes with no crossing.</summary>
    readonly uint[] _landed = GC.AllocateArray<uint>(1, pinned: true);

    readonly int _landedAddress;
    uint _serial;
    bool _submitted;

    /// <summary>Every batch's serial, across batches, so a buffer touched in one is never taken for touched in another.</summary>
    static uint _serials;

    public ComputeBatch(Vk vk)
    {
        _landedAddress = (int)Marshal.UnsafeAddrOfPinnedArrayElement(_landed, 0);
    }

    /// <summary>
    /// How many batches have been begun — which recording a command belongs to, for what the host writes once a recording
    /// and the device reads when it runs (<see cref="ResidentRows{T}.Send"/>). A page has one thread.
    /// </summary>
    public static long Begun => _begun;

    static long _begun;

    /// <summary>The batch emptied and begun, for its kernels to be recorded into.</summary>
    public Recording Begin()
    {
        if (InFlight) throw new InvalidOperationException("A batch is begun again before what it last read back has landed.");

        _begun++;
        _serial = ++_serials;
        _words = 0;
        _pushBytes = 0;
        _touchedCount = 0;
        _submitted = false;
        return new Recording(this);
    }

    /// <summary>Whether everything the last submit copies back is in the host's memory — or nothing was submitted.</summary>
    public bool Landed => !_submitted || Volatile.Read(ref _landed[0]) == _serial;

    /// <summary>Whether a submit is out and has not landed: the batch may be neither begun again nor read.</summary>
    public bool InFlight => !Landed;

    /// <summary>Whether <see cref="Wait"/> holds the host until the submit is done: a page's cannot.</summary>
    public bool Waits => false;

    /// <summary>Whether what the last submit writes is the host's to read: once it has <see cref="Landed"/>.</summary>
    public bool Readable => Landed;

    public void SubmitAndWait() => Submit();

    /// <summary>What was recorded, sent, and its read-backs asked for: they land between frames (<see cref="Landed"/>).</summary>
    public void Submit()
    {
        for (var at = 0; at < _touchedCount; at++)
        {
            var buffer = _touched[at];
            switch (buffer.Host)
            {
                case HostAccess.Writes or HostAccess.Streams: Operation(SendCode, buffer.Id); break;
                case HostAccess.Reads: Operation(ReadCode, buffer.Id); break;
            }
        }

        WebGpu.KernelSubmit(_operations.AsSpan(0, _words), _push.AsSpan(0, _pushBytes), (int)_serial, _landedAddress);
        _submitted = true;
    }

    /// <summary>Nothing: a page has nothing to wait on. Read only once <see cref="Landed"/>.</summary>
    public void Wait()
    {
    }

    internal void Dispatch(int kernel, BoundSet set, uint groups, ReadOnlySpan<byte> push)
    {
        TouchAll(set);
        Operation(DispatchCode, kernel, set.Id, (int)groups, Constants(push));
    }

    internal void DispatchIndirect(int kernel, BoundSet set, GpuBuffer counts, ulong offsetBytes, ReadOnlySpan<byte> push)
    {
        TouchAll(set);
        Touch(counts);
        Operation(DispatchIndirectCode, kernel, set.Id, counts.Id, checked((int)offsetBytes), Constants(push));
    }

    internal void Copy(GpuBuffer from, GpuBuffer to, CopyRun run)
    {
        if (run.Bytes == 0) return;

        Touch(from);
        Touch(to);
        Operation(CopyCode, from.Id, to.Id, checked((int)run.FromBytes), checked((int)run.ToBytes), checked((int)run.Bytes));
    }

    internal void Fill(GpuBuffer buffer, ulong offsetBytes, ulong bytes, uint word)
    {
        if (bytes == 0) return;

        Touch(buffer);
        Operation(FillCode, buffer.Id, checked((int)offsetBytes), checked((int)bytes), (int)word);
    }

    void TouchAll(BoundSet set)
    {
        foreach (var buffer in set.Buffers) Touch(buffer);
    }

    void Touch(GpuBuffer buffer)
    {
        if (buffer.TouchedIn == _serial) return;

        buffer.TouchedIn = _serial;
        if (_touchedCount == _touched.Length) Array.Resize(ref _touched, _touched.Length * 2);
        _touched[_touchedCount++] = buffer;
    }

    /// <summary>Where the dispatch's constants stand in the batch's uniform buffer, or −1 for none.</summary>
    int Constants(ReadOnlySpan<byte> push)
    {
        if (push.IsEmpty) return -1;

        if (_pushBytes + PushStride > _push.Length) Array.Resize(ref _push, _push.Length * 2);
        var at = _pushBytes;
        push.CopyTo(_push.AsSpan(at));
        _pushBytes += PushStride;
        return at;
    }

    void Operation(int code, int a, int b = 0, int c = 0, int d = 0, int e = 0)
    {
        if (_words + 6 > _operations.Length) Array.Resize(ref _operations, _operations.Length * 2);
        var ops = _operations.AsSpan(_words, 6);
        ops[0] = code;
        ops[1] = a;
        ops[2] = b;
        ops[3] = c;
        ops[4] = d;
        ops[5] = e;
        _words += 6;
    }

    public void Dispose()
    {
    }
}
