using Silk.NET.Vulkan;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>Kernels run with no frame to ride</b>: one command buffer recorded, submitted and waited on — how a headless run,
/// a probe and the suite step a town on the device. Whatever was recorded, it is one submit and one wait — the wait
/// put off where the host has work of its own to do meanwhile (<see cref="Submit"/>, <see cref="Wait"/>).
/// </summary>
/// <remarks>
/// A windowed run never uses this: its ticks are recorded into the frame's own command buffer and go with its one
/// submit (rule 1). Here there is no frame, so a batch of ticks is the unit a crossing is paid per.
/// </remarks>
internal sealed unsafe class ComputeBatch : IDisposable
{
    readonly Vk _vk;
    readonly CommandBuffer _commands;
    readonly Fence _fence;

    public ComputeBatch(Vk vk)
    {
        _vk = vk;
        _commands = vk.AllocateCommandBuffers(1)[0];

        var info = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        Vk.Count();
        Vk.Check(vk.Api.CreateFence(vk.Device, &info, null, out _fence), "vkCreateFence");
    }

    /// <summary>The command buffer, reset and begun, for the batch's kernels to be recorded into.</summary>
    public CommandBuffer Begin()
    {
        if (InFlight) throw new InvalidOperationException("A batch is begun again while its last submit is in flight.");

        var api = _vk.Api;
        Vk.Count();
        Vk.Check(api.ResetCommandBuffer(_commands, 0), "vkResetCommandBuffer");

        var begin = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        Vk.Count();
        Vk.Check(api.BeginCommandBuffer(_commands, &begin), "vkBeginCommandBuffer");
        return _commands;
    }

    /// <summary>What was recorded, submitted and waited for: on return, everything it wrote is the host's to read.</summary>
    public void SubmitAndWait()
    {
        Submit();
        Wait();
    }

    /// <summary>
    /// <b>Whether a submit is in flight</b> (<see cref="Submit"/>) and not yet waited for — when the batch may be neither
    /// begun again nor read.
    /// </summary>
    public bool InFlight { get; private set; }

    /// <summary>What was recorded, submitted and left running: the host goes on and waits for it later (<see cref="Wait"/>).</summary>
    public void Submit()
    {
        if (InFlight) throw new InvalidOperationException("A batch is submitted again before its last submit was waited for.");

        var api = _vk.Api;
        _vk.Barrier(_commands, PipelineStageFlags2.AllCommandsBit, AccessFlags2.MemoryWriteBit, PipelineStageFlags2.HostBit, AccessFlags2.HostReadBit);
        Vk.Count();
        Vk.Check(api.EndCommandBuffer(_commands), "vkEndCommandBuffer");

        var commands = _commands;
        var submit = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commands,
        };
        var fence = _fence;
        Vk.Count();
        Vk.Check(api.ResetFences(_vk.Device, 1, &fence), "vkResetFences");
        Vk.Count();
        Vk.Check(api.QueueSubmit(_vk.Queue, 1, &submit, _fence), "vkQueueSubmit");
        InFlight = true;
    }

    /// <summary>The last submit waited for, if one is in flight: on return, everything it wrote is the host's to read.</summary>
    public void Wait()
    {
        if (!InFlight) return;

        var fence = _fence;
        Vk.Count();
        Vk.Check(_vk.Api.WaitForFences(_vk.Device, 1, &fence, true, ulong.MaxValue), "vkWaitForFences");
        InFlight = false;
    }

    public void Dispose()
    {
        Wait();
        var api = _vk.Api;
        Vk.Count();
        api.DestroyFence(_vk.Device, _fence, null);
        var commands = _commands;
        Vk.Count();
        api.FreeCommandBuffers(_vk.Device, _vk.Pool(), 1, &commands);
    }
}
