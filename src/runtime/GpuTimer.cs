using Silk.NET.Vulkan;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>Where the device's own time went</b>: timestamps written by the queue as it reaches them, read back once the
/// work that wrote them is waited for — so a figure is the kernels' and not the submit's or the wait's.
/// </summary>
internal sealed unsafe class GpuTimer : IDisposable
{
    readonly Vk _vk;
    readonly QueryPool _pool;
    readonly uint _stamps;
    readonly ulong[] _read;
    readonly double _periodNs;

    public GpuTimer(Vk vk, int stamps)
    {
        _vk = vk;
        _stamps = (uint)stamps;
        _read = new ulong[stamps];
        _periodNs = vk.TimestampPeriodNs;

        var info = new QueryPoolCreateInfo
        {
            SType = StructureType.QueryPoolCreateInfo,
            QueryType = QueryType.Timestamp,
            QueryCount = _stamps,
        };
        Vk.Count();
        Vk.Check(vk.Api.CreateQueryPool(vk.Device, &info, null, out _pool), "vkCreateQueryPool");
    }

    /// <summary>Every stamp cleared, recorded at the head of the work they time.</summary>
    public void Reset(CommandBuffer commands)
    {
        Vk.Count();
        _vk.Api.CmdResetQueryPool(commands, _pool, 0, _stamps);
    }

    /// <summary>Stamp <paramref name="stamp"/> written once everything recorded before it has finished.</summary>
    public void Stamp(CommandBuffer commands, int stamp)
    {
        Vk.Count();
        _vk.Api.CmdWriteTimestamp2(commands, PipelineStageFlags2.AllCommandsBit, _pool, (uint)stamp);
    }

    /// <summary>The stamps as they were written, read after the work that wrote them has been waited for.</summary>
    public void Read()
    {
        fixed (ulong* into = _read)
        {
            Vk.Count();
            Vk.Check(
                _vk.Api.GetQueryPoolResults(
                    _vk.Device, _pool, 0, _stamps, (nuint)(_stamps * sizeof(ulong)), into, sizeof(ulong),
                    QueryResultFlags.Result64Bit | QueryResultFlags.ResultWaitBit),
                "vkGetQueryPoolResults");
        }
    }

    /// <summary>The device's time between two stamps of the last <see cref="Read"/>, in milliseconds.</summary>
    public double BetweenMs(int from, int to) => (_read[to] - _read[from]) * _periodNs / 1e6;

    public void Dispose()
    {
        Vk.Count();
        _vk.Api.DestroyQueryPool(_vk.Device, _pool, null);
    }
}
