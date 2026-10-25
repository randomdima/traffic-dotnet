using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.Render;

/// <summary>The pipelines and the layouts they are built against, created once and outliving every swapchain.</summary>
internal sealed unsafe partial class TownRenderer
{
    RenderTarget NewTarget() => _window is null
        ? OffscreenTarget.Create(_vk, _offscreenSize)
        : Swapchain.Create(_vk, new Extent2D((uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y));

    void CreatePipeline()
    {
        var api = _vk.Api;
        _vertexShader = _vk.LoadShader("ground.vert");
        _fragmentShader = _vk.LoadShader("ground.frag");
        _spriteVertexShader = _vk.LoadShader("sprite.vert");
        _spriteFragmentShader = _vk.LoadShader("sprite.frag");
        _overlayVertexShader = _vk.LoadShader("overlay.vert");
        _overlayFragmentShader = _vk.LoadShader("overlay.frag");
        _markVertexShader = _vk.LoadShader("mark.vert");
        _sceneryVertexShader = _vk.LoadShader("scenery.vert");
        _cloudVertexShader = _vk.LoadShader("cloud.vert");
        _cloudFragmentShader = _vk.LoadShader("cloud.frag");

        // One set for the whole frame: every pipeline reads the same camera and every picture the
        // town is drawn with is bound here once, so nothing is bound twice in a recording. Two
        // uniform blocks the vertex stage reads — the camera's the ground's fragment stage too, for the
        // water's waves — then the atlas, the glyphs and the five
        // surfaces — and not one of them is an array a shader indexes at run time — then what the
        // marks and the scenery are read out of, by the vertex stage again.
        var bindings = stackalloc DescriptorSetLayoutBinding[Bindings];
        for (var binding = 0; binding < Bindings; binding++)
        {
            var type = TypeOf(binding);
            bindings[binding] = new DescriptorSetLayoutBinding
            {
                Binding = (uint)binding,
                DescriptorType = type,
                DescriptorCount = 1,
                StageFlags = binding is FanTableBinding or CameraBinding ? ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
                    : type == DescriptorType.CombinedImageSampler ? ShaderStageFlags.FragmentBit : ShaderStageFlags.VertexBit,
            };
        }

        var setInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = (uint)Bindings,
            PBindings = bindings,
        };

        Vk.Count();
        Vk.Check(api.CreateDescriptorSetLayout(_vk.Device, &setInfo, null, out _setLayout), "vkCreateDescriptorSetLayout");

        var layout = _setLayout;
        var layoutInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &layout,
        };

        Vk.Count();
        Vk.Check(api.CreatePipelineLayout(_vk.Device, &layoutInfo, null, out _pipelineLayout), "vkCreatePipelineLayout");
    }

    /// <summary>
    /// The town's standing ground: one vertex per corner and a triangle list. Opaque and drawn first, because
    /// there is no depth buffer and painter's order is the whole of the sorting story.
    /// </summary>
    /// <remarks>
    /// <b>Blended all the same</b>, for the one place the ground is not opaque: a surface faded out over the
    /// ground laid before it along the shore's edges (<see cref="GroundMesh.Fade"/>). Everywhere else the
    /// fragment stage writes a cover of one, and the blend hands back what it was given.
    /// </remarks>
    void CreateGroundPipeline()
    {
        var binding = new VertexInputBindingDescription
        {
            Binding = 0,
            Stride = (uint)sizeof(GroundVertex),
            InputRate = VertexInputRate.Vertex,
        };

        var attributes = stackalloc VertexInputAttributeDescription[2];
        attributes[0] = new VertexInputAttributeDescription(0, 0, Format.R32G32Sfloat, 0);
        attributes[1] = new VertexInputAttributeDescription(1, 0, Format.R32Uint, 8);

        _pipeline = BuildPipeline(_vertexShader, _fragmentShader, binding, attributes, 2, PrimitiveTopology.TriangleList, blended: true);
    }

    /// <summary>
    /// The bodies standing on it: one <em>instance</em> per body and no vertex data at all — the quad
    /// comes out of <c>gl_VertexIndex</c> — blended over the ground, and drawn second for the same
    /// painter's-order reason the ground is drawn first.
    /// </summary>
    void CreateSpritePipeline()
    {
        var binding = new VertexInputBindingDescription
        {
            Binding = 0,
            Stride = (uint)sizeof(SpriteInstance),
            InputRate = VertexInputRate.Instance,
        };

        // SpriteInstance's packing, field for field: the centre, the half-size's two halves, the heading, the cell's four
        // bytes, the tint's four normalised, the sheet and the ID.
        var attributes = stackalloc VertexInputAttributeDescription[6];
        attributes[0] = new VertexInputAttributeDescription(0, 0, Format.R32G32Sfloat, 0);
        attributes[1] = new VertexInputAttributeDescription(1, 0, Format.R16G16Sfloat, 8);
        attributes[2] = new VertexInputAttributeDescription(2, 0, Format.R32Sfloat, 12);
        attributes[3] = new VertexInputAttributeDescription(3, 0, Format.R8G8B8A8Uint, 16);
        attributes[4] = new VertexInputAttributeDescription(4, 0, Format.R8G8B8A8Unorm, 20);
        attributes[5] = new VertexInputAttributeDescription(5, 0, Format.R16G16Uint, 24);

        _spritePipeline = BuildPipeline(
            _spriteVertexShader, _spriteFragmentShader, binding, attributes, 6, PrimitiveTopology.TriangleStrip, blended: true,
            premultiplied: true);
    }

    /// <summary>
    /// The interface and the debug layers: the same instanced quad again, blended, drawn last, and
    /// carrying its own transform flag so one buffer holds both the panels laid out in pixels and the
    /// marks drawn in metres where they happen.
    /// </summary>
    void CreateOverlayPipeline()
    {
        var binding = new VertexInputBindingDescription
        {
            Binding = 0,
            Stride = (uint)sizeof(OverlayQuad),
            InputRate = VertexInputRate.Instance,
        };

        var attributes = stackalloc VertexInputAttributeDescription[8];
        attributes[0] = new VertexInputAttributeDescription(0, 0, Format.R32G32Sfloat, 0);
        attributes[1] = new VertexInputAttributeDescription(1, 0, Format.R32G32Sfloat, 8);
        attributes[2] = new VertexInputAttributeDescription(2, 0, Format.R32G32Sfloat, 16);
        attributes[3] = new VertexInputAttributeDescription(3, 0, Format.R32G32Sfloat, 24);
        attributes[4] = new VertexInputAttributeDescription(4, 0, Format.R32G32B32A32Sfloat, 32);
        attributes[5] = new VertexInputAttributeDescription(5, 0, Format.R32Sfloat, 48);
        attributes[6] = new VertexInputAttributeDescription(6, 0, Format.R32Uint, 52);
        attributes[7] = new VertexInputAttributeDescription(7, 0, Format.R32Sfloat, 56);

        _overlayPipeline = BuildPipeline(
            _overlayVertexShader, _overlayFragmentShader, binding, attributes, 8, PrimitiveTopology.TriangleStrip,
            blended: true);
    }

    /// <summary>
    /// The marks, out of the ground's ring (<c>mark.vert</c>): the sprites' quad and fragment stage, blended, with no vertex
    /// buffer at all — each instance reads its own row.
    /// </summary>
    void CreateMarkPipeline() =>
        _markPipeline = BuildPipeline(
            _markVertexShader, _spriteFragmentShader, null, null, 0, PrimitiveTopology.TriangleStrip, blended: true,
            premultiplied: true);

    /// <summary>The scenery, out of its pieces by the frame's runs (<c>scenery.vert</c>), on the same terms.</summary>
    void CreateSceneryPipeline() =>
        _sceneryPipeline = BuildPipeline(
            _sceneryVertexShader, _spriteFragmentShader, null, null, 0, PrimitiveTopology.TriangleStrip, blended: true,
            premultiplied: true);

    /// <summary>
    /// The clouds' shadows (<see cref="CloudShadows"/>): one triangle over the whole target out of <c>gl_VertexIndex</c>, black
    /// laid over at the shadow's alpha, so what is under it keeps its colour and loses that share of its light.
    /// </summary>
    void CreateCloudPipeline() =>
        _cloudPipeline = BuildPipeline(
            _cloudVertexShader, _cloudFragmentShader, null, null, 0, PrimitiveTopology.TriangleList, blended: true);

    /// <param name="premultiplied">
    /// Whether the fragment stage writes its colour already times its alpha (<c>sprite.frag</c>): blended One over
    /// OneMinusSrcAlpha, which lays a texel over at its alpha and, with that alpha nought, adds it as light.
    /// </param>
    Pipeline BuildPipeline(
        ShaderModule vertex, ShaderModule fragment, VertexInputBindingDescription? binding,
        VertexInputAttributeDescription* attributes, uint attributeCount, PrimitiveTopology topology, bool blended,
        bool premultiplied = false)
    {
        var bound = binding.GetValueOrDefault();
        var api = _vk.Api;
        var entry = (byte*)SilkMarshal.StringToPtr("main");
        try
        {
            var stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertex,
                PName = entry,
            };
            stages[1] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragment,
                PName = entry,
            };

            var vertexInput = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = binding is null ? 0u : 1u,
                PVertexBindingDescriptions = binding is null ? null : &bound,
                VertexAttributeDescriptionCount = attributeCount,
                PVertexAttributeDescriptions = attributes,
            };

            var assembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = topology,
            };

            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
            };

            var raster = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                PolygonMode = PolygonMode.Fill,
                // Nothing here has a back: a ribbon bends both ways and a fillet's winding follows
                // the corner it rounds, so culling would drop half the town.
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                LineWidth = 1f,
            };

            var multisample = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = SampleCountFlags.Count1Bit,
            };

            var blendAttachment = new PipelineColorBlendAttachmentState
            {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                BlendEnable = blended,
                SrcColorBlendFactor = premultiplied ? BlendFactor.One : BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp = BlendOp.Add,
            };

            var blend = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                AttachmentCount = 1,
                PAttachments = &blendAttachment,
            };

            var dynamicStates = stackalloc DynamicState[2] { DynamicState.Viewport, DynamicState.Scissor };
            var dynamic = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates,
            };

            // Dynamic rendering: no render pass, no subpass, no framebuffer — the subsystem most
            // often rebuilt on resize simply is not here.
            var format = _target.Format;
            var rendering = new PipelineRenderingCreateInfo
            {
                SType = StructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = 1,
                PColorAttachmentFormats = &format,
            };

            var info = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                PNext = &rendering,
                StageCount = 2,
                PStages = stages,
                PVertexInputState = &vertexInput,
                PInputAssemblyState = &assembly,
                PViewportState = &viewportState,
                PRasterizationState = &raster,
                PMultisampleState = &multisample,
                PColorBlendState = &blend,
                PDynamicState = &dynamic,
                Layout = _pipelineLayout,
            };

            Vk.Count();
            Vk.Check(api.CreateGraphicsPipelines(_vk.Device, default, 1, &info, null, out Pipeline pipeline), "vkCreateGraphicsPipelines");
            return pipeline;
        }
        finally
        {
            SilkMarshal.Free((nint)entry);
        }
    }
}
