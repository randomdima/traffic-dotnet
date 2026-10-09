using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>One compute shader and the layout it is bound by</b>: a run of storage buffers at bindings 0…n−1 of set 0, and
/// push constants of one fixed size. What a buffer holds is the caller's; this knows a kernel's name and its arity.
/// </summary>
/// <remarks>
/// <para>
/// <b>A set is written once and bound for ever</b> (<see cref="Bind"/>): a kernel's buffers are the town's and are
/// laid with it, so nothing is written into a descriptor after load. Which of a pair of swapped buffers is the current
/// one is a word a kernel reads, never a set rebound — a set rebound per tick is a recording that changes. <b>The one
/// exception is a buffer that grows</b> (<see cref="GpuBuffer.Grow"/>), whose sets are pointed at the new buffer with the
/// device idle: what the set binds is still the same <see cref="GpuBuffer"/>.
/// </para>
/// <para>
/// Every call here is setup or recording. Recording into a buffer the frame submits costs no crossing of its own
/// beyond the calls counted here, and a recording made once is never made again.
/// </para>
/// </remarks>
internal sealed unsafe class ComputeKernel : IDisposable
{
    readonly Vk _vk;
    readonly DescriptorSetLayout _setLayout;
    readonly PipelineLayout _layout;
    readonly Pipeline _pipeline;
    readonly DescriptorPool _pool;
    readonly uint _pushBytes;

    /// <summary>Every buffer a set of this kernel binds, and the set — forgotten by each buffer as the kernel goes.</summary>
    readonly List<(GpuBuffer Buffer, DescriptorSet Set)> _bound = [];

    /// <param name="shader">The embedded SPIR-V's name, as <see cref="Vk.LoadShader"/> takes it: <c>integrate.comp</c>.</param>
    /// <param name="buffers">How many storage buffers the kernel binds.</param>
    /// <param name="pushBytes">The size of its push-constant block, a multiple of four; nought for none.</param>
    /// <param name="sets">How many sets will be bound through it — one per distinct run of buffers.</param>
    public ComputeKernel(Vk vk, string shader, int buffers, uint pushBytes, int sets = 1)
    {
        _vk = vk;
        _pushBytes = pushBytes;
        Buffers = buffers;
        var api = vk.Api;

        var bindings = stackalloc DescriptorSetLayoutBinding[buffers];
        for (var binding = 0; binding < buffers; binding++)
        {
            bindings[binding] = new DescriptorSetLayoutBinding
            {
                Binding = (uint)binding,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit,
            };
        }

        var setInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = (uint)buffers,
            PBindings = bindings,
        };
        Vk.Count();
        Vk.Check(api.CreateDescriptorSetLayout(vk.Device, &setInfo, null, out _setLayout), "vkCreateDescriptorSetLayout");

        var setLayout = _setLayout;
        var push = new PushConstantRange(ShaderStageFlags.ComputeBit, 0, pushBytes);
        var layoutInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &setLayout,
            PushConstantRangeCount = pushBytes > 0 ? 1u : 0u,
            PPushConstantRanges = &push,
        };
        Vk.Count();
        Vk.Check(api.CreatePipelineLayout(vk.Device, &layoutInfo, null, out _layout), "vkCreatePipelineLayout");

        var module = vk.LoadShader(shader);
        var entry = (byte*)SilkMarshal.StringToPtr("main");
        try
        {
            var info = new ComputePipelineCreateInfo
            {
                SType = StructureType.ComputePipelineCreateInfo,
                Stage = new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.ComputeBit,
                    Module = module,
                    PName = entry,
                },
                Layout = _layout,
            };
            Vk.Count();
            Vk.Check(api.CreateComputePipelines(vk.Device, default, 1, &info, null, out _pipeline), "vkCreateComputePipelines");
        }
        finally
        {
            SilkMarshal.Free((nint)entry);
            Vk.Count();
            api.DestroyShaderModule(vk.Device, module, null);
        }

        var size = new DescriptorPoolSize(DescriptorType.StorageBuffer, (uint)(buffers * sets));
        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = (uint)sets,
            PoolSizeCount = 1,
            PPoolSizes = &size,
        };
        Vk.Count();
        Vk.Check(api.CreateDescriptorPool(vk.Device, &poolInfo, null, out _pool), "vkCreateDescriptorPool");
    }

    /// <summary>How many storage buffers a set of this kernel binds.</summary>
    public int Buffers { get; }

    /// <summary>A set over these buffers, in binding order, written once.</summary>
    public DescriptorSet Bind(ReadOnlySpan<GpuBuffer> buffers)
    {
        if (buffers.Length != Buffers) throw new ArgumentException($"The kernel binds {Buffers} buffers, not {buffers.Length}.");

        var api = _vk.Api;
        var setLayout = _setLayout;
        var allocate = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _pool,
            DescriptorSetCount = 1,
            PSetLayouts = &setLayout,
        };
        Vk.Count();
        Vk.Check(api.AllocateDescriptorSets(_vk.Device, &allocate, out var set), "vkAllocateDescriptorSets");

        var infos = stackalloc DescriptorBufferInfo[Buffers];
        var writes = stackalloc WriteDescriptorSet[Buffers];
        for (var binding = 0; binding < Buffers; binding++)
        {
            infos[binding] = new DescriptorBufferInfo(buffers[binding].Handle, 0, Silk.NET.Vulkan.Vk.WholeSize);
            writes[binding] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = set,
                DstBinding = (uint)binding,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = &infos[binding],
            };
        }

        Vk.Count();
        api.UpdateDescriptorSets(_vk.Device, (uint)Buffers, writes, 0, null);
        for (var binding = 0; binding < Buffers; binding++)
        {
            buffers[binding].BoundIn(set, (uint)binding);
            _bound.Add((buffers[binding], set));
        }

        return set;
    }

    /// <summary>The kernel run over <paramref name="groups"/> workgroups, with <paramref name="push"/> as its constants.</summary>
    public void Dispatch<TPush>(CommandBuffer commands, DescriptorSet set, uint groups, in TPush push)
        where TPush : unmanaged
    {
        BindFor(commands, set, push);
        Vk.Count();
        _vk.Api.CmdDispatch(commands, groups, 1, 1);
    }

    /// <summary>
    /// The kernel run over as many workgroups as a buffer says when the queue reaches it — a count a kernel before it
    /// wrote, or nought for a run that has nothing to do this time, with the recording unchanged.
    /// </summary>
    public void DispatchIndirect<TPush>(CommandBuffer commands, DescriptorSet set, GpuBuffer counts, ulong offsetBytes, in TPush push)
        where TPush : unmanaged
    {
        BindFor(commands, set, push);
        Vk.Count();
        _vk.Api.CmdDispatchIndirect(commands, counts.Handle, offsetBytes);
    }

    void BindFor<TPush>(CommandBuffer commands, DescriptorSet set, in TPush push)
        where TPush : unmanaged
    {
        if (_pushBytes > 0 && (uint)sizeof(TPush) != _pushBytes)
        {
            throw new ArgumentException($"The kernel's constants are {_pushBytes} bytes, not {sizeof(TPush)}.");
        }

        var api = _vk.Api;
        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Compute, _pipeline);
        Vk.Count();
        api.CmdBindDescriptorSets(commands, PipelineBindPoint.Compute, _layout, 0, 1, &set, 0, null);
        if (_pushBytes == 0) return;

        fixed (TPush* p = &push)
        {
            Vk.Count();
            api.CmdPushConstants(commands, _layout, ShaderStageFlags.ComputeBit, 0, _pushBytes, p);
        }
    }

    public void Dispose()
    {
        foreach (var (buffer, set) in _bound) buffer.Unbound(set);

        var api = _vk.Api;
        Vk.Count();
        api.DestroyDescriptorPool(_vk.Device, _pool, null);
        Vk.Count();
        api.DestroyPipeline(_vk.Device, _pipeline, null);
        Vk.Count();
        api.DestroyPipelineLayout(_vk.Device, _layout, null);
        Vk.Count();
        api.DestroyDescriptorSetLayout(_vk.Device, _setLayout, null);
    }
}

/// <summary>What a kernel made with no push constants is dispatched with; its size is never read.</summary>
internal readonly struct NoPush;
