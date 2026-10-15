using System.Runtime.InteropServices;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>A buffer a kernel binds, in a page</b>: the browser's half of <c>GpuBuffer.cs</c>. One the host writes or reads is
/// pinned memory on this side as well as the device's: what the host writes crosses ahead of each batch that touches it,
/// and what a batch writes lands back here between frames (<see cref="ComputeBatch"/>).
/// </summary>
/// <remarks>
/// <b>A page maps nothing</b>, so the desktop's "a span over memory the driver owns" is a span over this side's own copy:
/// read it only once the batch that wrote it has landed (<see cref="ComputeBatch.Landed"/>).
/// </remarks>
internal sealed class GpuBuffer : IDisposable
{
    readonly Vk _vk;
    readonly byte[]? _memory;

    public GpuBuffer(Vk vk, int id, ulong sizeBytes, HostAccess host, byte[]? memory)
    {
        _vk = vk;
        Id = id;
        SizeBytes = sizeBytes;
        Host = host;
        _memory = memory;
    }

    /// <summary>The page's own name for it.</summary>
    public int Id { get; }

    public ulong SizeBytes { get; }

    public HostAccess Host { get; }

    /// <summary>The serial of the last batch that touched it (<see cref="ComputeBatch"/>), nought for none.</summary>
    internal uint TouchedIn;

    /// <summary>The host's copy as the type written into it. Host-reached buffers only.</summary>
    public Span<T> Span<T>() where T : unmanaged
    {
        if (_memory is null) throw new InvalidOperationException("This buffer is device-only: the host holds nothing of it.");

        return MemoryMarshal.Cast<byte, T>(_memory.AsSpan());
    }

    public void Write<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        data.CopyTo(Span<T>());
    }

    /// <summary>The size of a buffer of <paramref name="count"/> elements, never empty.</summary>
    public static unsafe ulong Bytes<T>(int count) where T : unmanaged => (ulong)Math.Max(1, count) * (ulong)sizeof(T);

    /// <summary>Elements laid into the buffer, ahead of every batch submitted after.</summary>
    public void Upload<T>(int count, StagedFill<T> fill) where T : unmanaged
    {
        if (count == 0) return;

        var rows = new T[count];
        fill(rows);
        Upload<T>(rows);
    }

    /// <summary>
    /// The most bytes one write crosses with. <b>A browser stages a write whole</b>, in its own memory, before the device
    /// has any of it — so a traced city's atlas sent in one would be a second gigabyte for a moment, and on a 32-bit page
    /// a converted copy of it a third.
    /// </summary>
    const int WriteBytes = 16 << 20;

    /// <inheritdoc cref="Upload{T}(int, StagedFill{T})"/>
    public void Upload<T>(ReadOnlySpan<T> rows, ulong atBytes = 0) where T : unmanaged
    {
        if (rows.IsEmpty) return;

        var bytes = MemoryMarshal.AsBytes(rows);
        var words = bytes.Length & ~(sizeof(uint) - 1);
        for (var at = 0; at < words; at += WriteBytes)
        {
            Send(bytes.Slice(at, Math.Min(WriteBytes, words - at)), atBytes + (ulong)at);
        }

        // A page writes whole words: the last is padded with what the buffer holds past the rows, which nothing reads.
        if (words < bytes.Length)
        {
            var padded = new byte[sizeof(uint)];
            bytes[words..].CopyTo(padded);
            Send(padded, atBytes + (ulong)words);
        }
    }

    /// <summary>And rows of another kind, each turned into the device's a write's worth at a time.</summary>
    public unsafe void Upload<TFrom, T>(ReadOnlySpan<TFrom> rows, delegate*<in TFrom, T> convert, ulong atBytes = 0)
        where TFrom : unmanaged where T : unmanaged
    {
        if (rows.IsEmpty) return;

        // A whole number of words a write, whatever a row weighs, so no write but the last is padded.
        var rowsAWrite = Math.Max(sizeof(uint), WriteBytes / sizeof(T) / sizeof(uint) * sizeof(uint));
        var into = new T[Math.Min(rowsAWrite, rows.Length)];
        for (var first = 0; first < rows.Length; first += into.Length)
        {
            var count = Math.Min(into.Length, rows.Length - first);
            for (var at = 0; at < count; at++) into[at] = convert(rows[first + at]);
            Upload<T>(into.AsSpan(0, count), atBytes + ((ulong)first * (ulong)sizeof(T)));
        }
    }

    /// <summary>The page only reads it; a view crosses as a writable span.</summary>
    void Send(ReadOnlySpan<byte> bytes, ulong atBytes) =>
        WebGpu.KernelWrite(Id, checked((int)atBytes), MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(bytes), bytes.Length));

    public void Dispose()
    {
        WebGpu.KernelBufferGone(Id);
        _vk.Forget(this);
    }
}

/// <summary>The elements a staged upload writes, into memory the upload already owns.</summary>
internal delegate void StagedFill<T>(Span<T> into) where T : unmanaged;

/// <summary>What the CPU does with a buffer's memory, which decides where a page keeps a copy of it (<see cref="GpuBuffer"/>).</summary>
internal enum HostAccess
{
    /// <summary>Nothing: on the device only, reached by a copy or a kernel.</summary>
    None,

    /// <summary>Writes it: sent ahead of each batch that touches it.</summary>
    Writes,

    /// <summary>Reads it back: landed after each batch that touches it.</summary>
    Reads,

    /// <summary>Writes it every frame — on a page, as <see cref="Writes"/>.</summary>
    Streams,
}
