using Silk.NET.Vulkan;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>Kernels run with no frame to ride</b>: one command buffer recorded, submitted and waited on — how a headless run,
/// a probe and the suite step a town on the device. Whatever was recorded, it is one submit and one wait.
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
        Vk.Count();
        Vk.Check(api.WaitForFences(_vk.Device, 1, &fence, true, ulong.MaxValue), "vkWaitForFences");
    }

    public void Dispose()
    {
        var api = _vk.Api;
        Vk.Count();
        api.DestroyFence(_vk.Device, _fence, null);
        var commands = _commands;
        Vk.Count();
        api.FreeCommandBuffers(_vk.Device, _vk.Pool(), 1, &commands);
    }
}
