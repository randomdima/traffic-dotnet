using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.Runtime;
using Xunit;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A kernel reaches a table by its id alone</b> (<see cref="TownRoot"/>): one table read and another written through
/// the root, and a table that grew read where it went.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class TownRootTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Push(uint Count, uint FromId, uint IntoId);

    const int From = 5;
    const int Into = 9;

    [Fact]
    public void ATableIsReadAndWrittenThroughTheRoot()
    {
        var into = Doubled(grows: false);
        Assert.Equal([2f, 4f, 6f], into);
    }

    [Fact]
    public void ATableThatGrewIsReadWhereItWent()
    {
        var into = Doubled(grows: true);
        Assert.Equal([2f, 4f, 6f], into);
    }

    float[] Doubled(bool grows)
    {
        var vk = device.Vk;
        using var root = new TownRoot(vk);
        using var from = vk.CreateBuffer(GpuBuffer.Bytes<float>(3), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        using var into = vk.CreateBuffer(GpuBuffer.Bytes<float>(3), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        root.Set(From, from);
        root.Set(Into, into);
        if (grows) from.Grow(GpuBuffer.Bytes<float>(64));
        from.Write<float>([1f, 2f, 3f]);

        using var kernel = new ComputeKernel(vk, "town_root_check.comp", 1, (uint)Marshal.SizeOf<Push>());
        var set = kernel.Bind([root.Buffer]);
        vk.OneShot(commands => kernel.Dispatch(commands, set, 1, new Push(3, From, Into)));
        return into.Span<float>()[..3].ToArray();
    }
}
