// The machine, on the browser's side of the wall: a WebGPU device, the four draws the town is made of,
// and the keyboard and pointer. It is the counterpart of src/runtime/Vk.cs and src/runtime/AppWindow.cs,
// and it is written in JavaScript for the same reason those are written against Vulkan — it is the last
// place before the driver.
//
// Three rules shape the whole file.
//
// 1. THE WALL IS CROSSED A FIXED NUMBER OF TIMES A FRAME. A standing town crosses it three times
//    whatever it holds: the animation callback going in, the input coming out, and the frame. The
//    pass, the bundle and the submit are all on this side of it.
// 2. NOTHING IS COPIED ON THE MANAGED SIDE. The instance buffers arrive as a Uint8Array over the
//    WebAssembly heap the arrays already live in, so `writeBuffer` reads the simulation's own memory.
// 3. EVERY VIEW IS AN ARGUMENT AND NONE IS KEPT. A view onto that heap is detached the moment the
//    runtime grows its memory, and a kept one would fail on whichever frame that happened to be.
//
// The draws are recorded into a render bundle once, and the counts live in an indirect buffer the CPU
// writes — the same shape as the desktop's one-command-buffer-per-image recording, for the same
// reason: a town that gains five hundred walkers must change a number and not a recording.

import * as opening from './loading.js'
import { compute, kernelBufferOf, kernelLimits, kernelTimes, onLanded, useDevice } from './compute.js'
import { GpuClock } from './gputime.js'
import { addContext } from './log.js'

const SLOT = {
    ground: 0, indices: 1, sprites: 2, overlay: 3, underlay: 4, camera: 5, table: 6, standing: 7, fans: 8, markKnots: 9,
    markRing: 10, markLook: 11,
};
const TEXTURE = { pages: 0, glyphs: 1, surfaces: 2 };

// Draws in painter's order, at their offsets in the indirect buffer. The ground is indexed and so is
// five words; the others are four. The sprites are three runs: what lies under the buildings, the
// buildings and props laid once (a stretch of them a frame), and what moves over them. Then the level
// above, as the desktop draws it (TER-7b, PHY-1a): its ground over the bodies on the ground, its marks,
// and the bodies on it.
const DRAW = {
    ground: 0, underlay: 32, under: 48, standing: 64, over: 80,
    aboveGround: 96, underlayAbove: 128, above: 144, overlay: 160,
};
const INDIRECT_BYTES = 176;

// Where each of a frame's counts stands in the array it hands over (WebGpu.FrameCount).
const COUNT = {
    under: 0, standingFirst: 1, standing: 2, over: 3, above: 4, overlay: 5, underlay: 6, underlayAbove: 7, markKnotsAt: 8,
};

// The keys the town reads, in the order src/runtime/web/AppWindow.Web.cs names them. The two lists are
// one list, and a key added to either without the other is a key that does nothing.
const KEYS = {
    KeyA: 0, KeyD: 1, KeyS: 2, KeyW: 3, KeyE: 4, KeyR: 5,
    ArrowUp: 6, ArrowDown: 7, ArrowLeft: 8, ArrowRight: 9,
    Escape: 10, F11: 11, Backquote: 12, Pause: 13, Space: 14,
    Digit1: 15, Digit2: 16, Digit3: 17,
    ShiftLeft: 18, ShiftRight: 19, ControlLeft: 20, ControlRight: 21,
    Digit4: 22, Digit5: 23, KeyM: 24, Enter: 25, NumpadEnter: 25,
};

const KEY_COUNT = 32;
const DOWN = 0;
const PRESSED = KEY_COUNT;
const BUTTONS = KEY_COUNT * 2;

const AXIS = {
    pointerX: 0, pointerY: 1, scroll: 2, clickX: 3, clickY: 4, clickButton: 5,
    width: 6, height: 7, scale: 8, resized: 9,
    // The fingers on the glass, and where the first two of them are (CTL-9). **Where they are and not
    // what they mean**: a pinch, a twist and a two-finger pan are one movement read a frame apart, and
    // reading it is the run's (src/app/playercontrol/TouchGesture.cs).
    touchCount: 10, touchOneX: 11, touchOneY: 12, touchTwoX: 13, touchTwoY: 14,
    // The device's own time since the last pump (gputime.js): the frames' passes and the kernels' batches whose
    // timestamps landed, summed, and how many of each.
    gpuDrawMs: 15, gpuDraws: 16, gpuKernelsMs: 17, gpuBatches: 18,
    // Whether the browser offers `timestamp-query` at all, so a read-out with no device rows says why.
    gpuTimed: 19,
};

/// How many of them there are, which is the length the run's own array is made at. **Whether the
/// run is over is not one of them**: an axis is something the page saw, a pump copies the page's
/// copy over the run's, and the page has no opinion about that one.
const AXIS_COUNT = 20;

const state = {
    device: null,
    context: null,
    format: null,
    canvas: null,
    layout: null,
    pipelines: {},
    bundle: null,
    buffers: {},
    textures: {},
    samplers: {},
    indirect: null,
    counts: new Uint32Array(INDIRECT_BYTES / 4),
    keys: new Uint8Array(BUTTONS + 8),
    axes: new Float64Array(AXIS_COUNT),
    // The index buffer's two draws: the ground, then the level above straight after it.
    groundIndexCount: 0,
    aboveIndexCount: 0,
    // Where the sprites over the buildings and those on the level above start in their buffer, and the marks on the
    // level above in theirs, in bytes.
    overOffset: 0,
    aboveOffset: 0,
    underlayAboveOffset: 0,
    // Whether an indirect draw may start part way into its buffer; without it the buildings' stretch is drawn
    // from the first of them, which is more than the frame sees and never less.
    firstInstance: false,
    mostPixelRatio: Infinity,
    drawClock: null,
    deviceSaid: { at: 0, drawMs: 0, draws: 0, kernelsMs: 0, batches: 0 },
};

/// The adapter, asked for once and answered to everybody who asks.
///
/// **The page asks before it downloads the engine and the run asks when it starts, and those are the
/// same question.** A second `requestAdapter` is tens of milliseconds spent being told what the first
/// one already said, so the promise is kept rather than the answer — `null` is an answer too, and the
/// two callers say different things about it.
export function adapter() {
    if (!navigator.gpu) return Promise.resolve(null);

    return asked ??= navigator.gpu.requestAdapter().catch(() => null);
}

let asked = null;

/// A fault of the device's, on the console and the first of them in the banner: a handset has no console anybody reads,
/// and a step that fails every tick says the same thing sixty times a second. **The banner and not `say`**, which while
/// the opening is up writes a stage the boot's next line replaces.
function fault(line) {
    console.error(line);
    const banner = document.getElementById('banner');
    if (faulted || !banner) return;

    faulted = true;
    banner.textContent = line;
    banner.style.display = 'block';
}

let faulted = false;

/// The device as a saved log tells it (log.js): what the browser calls it, what it was given against what the adapter
/// has, and so whether its world steps on it (WEB-10).
function described(found, device) {
    const info = found.info ?? {};
    const fallback = info.isFallbackAdapter ?? found.isFallbackAdapter;
    const lines = [
        `gpu ${info.vendor} / ${info.architecture} / ${info.device} / ${info.description}${fallback ? ' (a fallback adapter)' : ''}`,
        `the world's kernels fit: ${compute.kernelsFit()}; indirect-first-instance: ${state.firstInstance}`,
        `canvas ${state.canvas?.width}x${state.canvas?.height} ${state.format}`,
        `features ${[...device.features].sort().join(' ')}`,
        'limits, given / the adapter\'s:',
    ];

    for (const name in device.limits) lines.push(`  ${name} ${device.limits[name]} / ${found.limits[name]}`);
    return lines;
}

/// What the atlas's pages may be held in, in BakedSheets.cs's SheetFormat order: the feature a device needs to sample
/// it, and the texture format it is created in.
const SHEETS = [
    { name: 'rgba', feature: null, format: 'rgba8unorm' },
    { name: 'bc7', feature: 'texture-compression-bc', format: 'bc7-rgba-unorm' },
    { name: 'astc', feature: 'texture-compression-astc', format: 'astc-4x4-unorm' },
];

/// BC7 where the adapter samples BC — every desktop's — else ASTC — every phone's — else texels; or what `?sheets=` names,
/// for a comparison shot. Whether the build baked any is the run's to find out (Data.cs), which falls back to texels.
function sheetFormatOf(found) {
    const asked = SHEETS.findIndex(each => each.name === new URLSearchParams(location.search).get('sheets'));
    if (asked >= 0) return SHEETS[asked].feature === null || found.features.has(SHEETS[asked].feature) ? asked : 0;

    const offered = SHEETS.findIndex(each => each.feature !== null && found.features.has(each.feature));
    return offered < 0 ? 0 : offered;
}

/// The format the device was opened for, as an index of SHEETS.
function sheetFormat() {
    return state.sheets ?? 0;
}

/// The archives' block files the atlas was written from, kept for the run: a second map packs the atlas again out of
/// the same blocks, as it does out of the same decoded pictures.
const blockFiles = new Map();

/// **The atlas written as blocks, a box at a time, straight out of the archive that brought them** (BakedSheets.cs): the
/// run's heap never holds them. `boxes` is six numbers a box — page, x, y, width, height in texels, and where its
/// blocks start in the file — every corner on a block's.
function sheetBlocks(slot, format, pages, path, boxes) {
    let blocks = blockFiles.get(path);
    if (blocks === undefined) {
        blocks = held.get(path);
        if (!(blocks instanceof Uint8Array)) throw new Error(`the atlas's blocks were not fetched: ${path}`);

        held.delete(path);
        blockFiles.set(path, blocks);
    }

    if (state.textures[slot]) state.textures[slot].destroy();
    state.textures[slot] = state.device.createTexture({
        size: [PAGE_PX, PAGE_PX, pages],
        format: SHEETS[format].format,
        usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST,
        dimension: '2d',
    });

    const table = bytes(boxes);
    const words = new Int32Array(table.buffer, table.byteOffset, table.byteLength / 4);
    for (let at = 0; at + 6 <= words.length; at += 6) {
        const [page, x, y, width, height, from] = words.subarray(at, at + 6);
        state.device.queue.writeTexture(
            { texture: state.textures[slot], origin: [x, y, page] },
            blocks, { offset: from, bytesPerRow: (width / 4) * 16, rowsPerImage: height / 4 }, [width, height, 1]);
    }
}

/// An atlas page's side in texels (SheetAtlas.PagePx).
const PAGE_PX = 4096;

/// The device, the canvas and the shaders. Answers the reason it could not, or "" — a page that cannot
/// have a device says so in words rather than drawing nothing.
async function start(wgsl) {
    if (!navigator.gpu) {
        return 'This browser has no WebGPU. It wants Chrome or Edge 113+, Safari 26+, or a Firefox where it has shipped.';
    }

    const found = await adapter();
    if (!found) return 'No WebGPU adapter: the browser has the API but no device it will hand out.';

    state.firstInstance = found.features.has('indirect-first-instance');
    state.sheets = sheetFormatOf(found);
    const features = ['indirect-first-instance', 'timestamp-query', SHEETS[state.sheets].feature]
        .filter(feature => feature && found.features.has(feature));
    state.device = await found.requestDevice({ requiredFeatures: features, requiredLimits: kernelLimits(found) });
    const timed = state.device.features.has('timestamp-query');
    state.drawClock = timed ? new GpuClock(state.device, 'frame') : null;
    state.axes[AXIS.gpuTimed] = timed ? 1 : 0;
    useDevice(state.device, timed ? new GpuClock(state.device, 'kernels') : null);
    addContext(() => described(found, state.device));
    state.device.addEventListener('uncapturederror', e => fault(`WebGPU: ${e.error.message}`));
    state.device.lost.then(info => fault(`WebGPU device lost: ${info.message}`));

    state.canvas = document.getElementById('town');
    state.context = state.canvas.getContext('webgpu');
    state.format = navigator.gpu.getPreferredCanvasFormat();
    state.axes[AXIS.clickButton] = -1;
    resize();

    const module = state.device.createShaderModule({ code: wgsl, label: 'town' });
    const info = await module.getCompilationInfo();
    for (const message of info.messages) {
        if (message.type === 'error') return `WGSL ${message.lineNum}:${message.linePos}: ${message.message}`;
    }

    state.samplers.clamped = state.device.createSampler({
        magFilter: 'linear', minFilter: 'linear', mipmapFilter: 'linear',
        addressModeU: 'clamp-to-edge', addressModeV: 'clamp-to-edge',
    });
    state.samplers.repeated = state.device.createSampler({
        magFilter: 'linear', minFilter: 'linear', mipmapFilter: 'linear',
        addressModeU: 'repeat', addressModeV: 'repeat',
    });

    buildLayout();
    buildPipelines(module);
    listen();
    return '';
}

function buildLayout() {
    const entries = [
        // The camera's: the ground's fragment stage reads the water's waves from it.
        { binding: 0, visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT, buffer: { type: 'uniform' } },
        { binding: 1, visibility: GPUShaderStage.VERTEX, buffer: { type: 'uniform' } },
        { binding: 2, visibility: GPUShaderStage.FRAGMENT, texture: { viewDimension: '2d-array' } },
    ];

    entries.push({ binding: 3, visibility: GPUShaderStage.FRAGMENT, texture: {} });
    // The five surfaces, each its looks a layer a look (town.wgsl, GroundPatches).
    for (let binding = 4; binding <= 8; binding++) {
        entries.push({ binding, visibility: GPUShaderStage.FRAGMENT, texture: { viewDimension: '2d-array' } });
    }

    entries.push({ binding: 9, visibility: GPUShaderStage.FRAGMENT, sampler: {} });
    entries.push({ binding: 10, visibility: GPUShaderStage.FRAGMENT, sampler: {} });
    // The track's strip, the sixth surface, after the samplers (town.wgsl).
    entries.push({ binding: 11, visibility: GPUShaderStage.FRAGMENT, texture: {} });
    // The roofs' fans, read by both sprite stages (town.wgsl, FanTable).
    entries.push({ binding: 12, visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT, buffer: { type: 'uniform' } });
    // The shore, the seventh surface, after the fans (town.wgsl).
    entries.push({ binding: 13, visibility: GPUShaderStage.FRAGMENT, texture: { viewDimension: '2d-array' } });
    // The two kerbs' strips, the eighth and ninth (town.wgsl).
    entries.push({ binding: 14, visibility: GPUShaderStage.FRAGMENT, texture: {} });
    entries.push({ binding: 15, visibility: GPUShaderStage.FRAGMENT, texture: {} });
    // A bridge's art, its strips and its fittings a layer each, after them (town.wgsl, BridgeSheet).
    entries.push({ binding: 16, visibility: GPUShaderStage.FRAGMENT, texture: { viewDimension: '2d-array' } });
    state.layout =state.device.createBindGroupLayout({ entries, label: 'town' });

    // The marks' own group (town.wgsl's markVertex): their look, the ring's knots and its words.
    state.markLayout = state.device.createBindGroupLayout({
        label: 'marks',
        entries: [
            { binding: 0, visibility: GPUShaderStage.VERTEX, buffer: { type: 'uniform' } },
            { binding: 1, visibility: GPUShaderStage.VERTEX, buffer: { type: 'read-only-storage' } },
            { binding: 2, visibility: GPUShaderStage.VERTEX, buffer: { type: 'read-only-storage' } },
        ],
    });
}

function buildPipelines(module) {
    const layout = state.device.createPipelineLayout({ bindGroupLayouts: [state.layout] });
    const blend = {
        color: { srcFactor: 'src-alpha', dstFactor: 'one-minus-src-alpha', operation: 'add' },
        alpha: { srcFactor: 'src-alpha', dstFactor: 'one-minus-src-alpha', operation: 'add' },
    };
    // The sprites' colour comes premultiplied (town.wgsl's spriteFragment), so a sheet of light, written with no
    // coverage, adds onto what is under it.
    const premultiplied = {
        color: { srcFactor: 'one', dstFactor: 'one-minus-src-alpha', operation: 'add' },
        alpha: { srcFactor: 'one', dstFactor: 'one-minus-src-alpha', operation: 'add' },
    };

    // The ground keeps the frame's alpha whole, as the desktop's blend does (TownRenderer.BuildPipeline).
    const groundBlend = {
        color: { srcFactor: 'src-alpha', dstFactor: 'one-minus-src-alpha', operation: 'add' },
        alpha: { srcFactor: 'one', dstFactor: 'one-minus-src-alpha', operation: 'add' },
    };

    const float2 = 'float32x2';
    state.pipelines.ground = state.device.createRenderPipeline({
        layout,
        vertex: {
            module, entryPoint: 'groundVertex',
            buffers: [{
                arrayStride: 12,
                attributes: [
                    { shaderLocation: 0, offset: 0, format: float2 },
                    { shaderLocation: 1, offset: 8, format: 'uint32' },
                ],
            }],
        },
        // Blended for the shore's fades alone (GroundMesh.Fade): everywhere else the ground's cover is one.
        fragment: { module, entryPoint: 'groundFragment', targets: [{ format: state.format, blend: groundBlend }] },
        primitive: { topology: 'triangle-list' },
    });

    state.pipelines.sprite = state.device.createRenderPipeline({
        layout,
        vertex: {
            module, entryPoint: 'spriteVertex',
            buffers: [{
                // SpriteInstance's packing: centre, half-size as halves, heading, cell bytes, tint bytes, sheet.
                arrayStride: 28, stepMode: 'instance',
                attributes: [
                    { shaderLocation: 0, offset: 0, format: float2 },
                    { shaderLocation: 1, offset: 8, format: 'float16x2' },
                    { shaderLocation: 2, offset: 12, format: 'float32' },
                    { shaderLocation: 3, offset: 16, format: 'uint8x4' },
                    { shaderLocation: 4, offset: 20, format: 'unorm8x4' },
                    { shaderLocation: 5, offset: 24, format: 'uint16x2' },
                ],
            }],
        },
        fragment: { module, entryPoint: 'spriteFragment', targets: [{ format: state.format, blend: premultiplied }] },
        primitive: { topology: 'triangle-strip' },
    });

    // The marks (town.wgsl's markVertex): a strip a knot, out of the ring, and no vertex buffer — a draw a level, each of
    // its own knots alone (drawsRaised).
    const marksLayout = state.device.createPipelineLayout({ bindGroupLayouts: [state.layout, state.markLayout] });
    const marksOf = drawsRaised => state.device.createRenderPipeline({
        layout: marksLayout,
        vertex: { module, entryPoint: 'markVertex', constants: { drawsRaised: drawsRaised ? 1 : 0 } },
        fragment: { module, entryPoint: 'spriteFragment', targets: [{ format: state.format, blend: premultiplied }] },
        primitive: { topology: 'triangle-strip' },
    });
    state.pipelines.marks = marksOf(false);
    state.pipelines.marksAbove = marksOf(true);

    // The clouds' shadows (OBJ-8): one triangle out of the vertex index, and no buffer.
    state.pipelines.cloud = state.device.createRenderPipeline({
        layout,
        vertex: { module, entryPoint: 'cloudVertex' },
        fragment: { module, entryPoint: 'cloudFragment', targets: [{ format: state.format, blend }] },
        primitive: { topology: 'triangle-list' },
    });

    state.pipelines.overlay = state.device.createRenderPipeline({
        layout,
        vertex: {
            module, entryPoint: 'overlayVertex',
            buffers: [{
                arrayStride: 60, stepMode: 'instance',
                attributes: [
                    { shaderLocation: 0, offset: 0, format: float2 },
                    { shaderLocation: 1, offset: 8, format: float2 },
                    { shaderLocation: 2, offset: 16, format: float2 },
                    { shaderLocation: 3, offset: 24, format: float2 },
                    { shaderLocation: 4, offset: 32, format: 'float32x4' },
                    { shaderLocation: 5, offset: 48, format: 'float32' },
                    { shaderLocation: 6, offset: 52, format: 'uint32' },
                    { shaderLocation: 7, offset: 56, format: 'float32' },
                ],
            }],
        },
        fragment: { module, entryPoint: 'overlayFragment', targets: [{ format: state.format, blend }] },
        primitive: { topology: 'triangle-strip' },
    });
}

/// A window onto the run's own memory, as something the browser's APIs will take.
///
/// What arrives from the runtime is a MemoryView — a pointer and a length, not a typed array — and
/// WebGPU takes only the latter. `_unsafe_create_view` is the view over the WebAssembly heap itself,
/// which is the whole point: no copy, and `writeBuffer` reads the simulation's own memory. It is
/// underscored because it is unsafe to *keep*, which nothing here does. `slice` is the same thing with
/// a copy, kept as the way out should a runtime ever stop offering the first.
function bytes(view) {
    return view._unsafe_create_view ? view._unsafe_create_view() : view.slice();
}

/// A buffer laid for what a frame may write into it, and left empty.
function reserve(slot, byteLength, usage) {
    if (state.buffers[slot]) state.buffers[slot].destroy();

    state.buffers[slot] = state.device.createBuffer({
        size: Math.max(16, Math.ceil(byteLength / 4) * 4),
        usage: usage | GPUBufferUsage.COPY_DST,
    });
}

/// A buffer laid for what it is being given and written once: the ground, and the table of places.
function buffer(slot, view, usage) {
    reserve(slot, view.byteLength, usage);
    state.device.queue.writeBuffer(state.buffers[slot], 0, bytes(view));
}

/// One picture, one layer of the atlas, or one level of a chain. The first of them lays the texture,
/// which is why the size handed over is the top level's and the levels are counted rather than derived.
function texture(slot, view, width, height, layers, layer, level, levels) {
    if (layer === 0 && level === 0) {
        if (state.textures[slot]) state.textures[slot].destroy();
        state.textures[slot] = state.device.createTexture({
            size: [width, height, layers],
            mipLevelCount: levels,
            format: 'rgba8unorm',
            usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST,
            dimension: '2d',
        });
    }

    state.device.queue.writeTexture(
        { texture: state.textures[slot], mipLevel: level, origin: [0, 0, layer] },
        bytes(view), { bytesPerRow: width * 4, rowsPerImage: height }, [width, height, 1]);
}

/// The bind group and the recording, made again whenever what they are made of changes — a map opened,
/// a canvas resized — and never while a frame is being drawn.
function rebuild(groundIndexCount, aboveIndexCount, overOffset, aboveOffset, underlayAboveOffset, marksAbove) {
    state.groundIndexCount = groundIndexCount;
    state.aboveIndexCount = aboveIndexCount;
    state.overOffset = overOffset;
    state.aboveOffset = aboveOffset;
    state.underlayAboveOffset = underlayAboveOffset;
    state.marksAbove = marksAbove;
    if (!state.indirect) {
        state.indirect = state.device.createBuffer({
            size: INDIRECT_BYTES,
            usage: GPUBufferUsage.INDIRECT | GPUBufferUsage.COPY_DST,
        });
    }

    const entries = [
        { binding: 0, resource: { buffer: state.buffers[SLOT.camera] } },
        { binding: 1, resource: { buffer: state.buffers[SLOT.table] } },
        { binding: 2, resource: state.textures[TEXTURE.pages].createView({ dimension: '2d-array' }) },
        { binding: 3, resource: state.textures[TEXTURE.glyphs].createView() },
    ];

    for (let surface = 0; surface < 5; surface++) {
        entries.push({ binding: 4 + surface, resource: state.textures[TEXTURE.surfaces + surface].createView({ dimension: '2d-array' }) });
    }

    entries.push({ binding: 9, resource: state.samplers.clamped });
    entries.push({ binding: 10, resource: state.samplers.repeated });
    entries.push({ binding: 11, resource: state.textures[TEXTURE.surfaces + 5].createView() });
    entries.push({ binding: 12, resource: { buffer: state.buffers[SLOT.fans] } });
    entries.push({ binding: 13, resource: state.textures[TEXTURE.surfaces + 6].createView({ dimension: '2d-array' }) });
    entries.push({ binding: 14, resource: state.textures[TEXTURE.surfaces + 7].createView() });
    entries.push({ binding: 15, resource: state.textures[TEXTURE.surfaces + 8].createView() });
    entries.push({ binding: 16, resource: state.textures[TEXTURE.surfaces + 9].createView({ dimension: '2d-array' }) });
    const group = state.device.createBindGroup({ layout: state.layout, entries });

    const pass = state.device.createRenderBundleEncoder({ colorFormats: [state.format] });
    pass.setBindGroup(0, group);

    pass.setPipeline(state.pipelines.ground);
    pass.setVertexBuffer(0, state.buffers[SLOT.ground]);
    pass.setIndexBuffer(state.buffers[SLOT.indices], 'uint32');
    pass.drawIndexedIndirect(state.indirect, DRAW.ground);

    // The town's own ground marks, over the ground and under everything that stands on it.
    pass.setPipeline(state.pipelines.overlay);
    pass.setVertexBuffer(0, state.buffers[SLOT.underlay]);
    pass.drawIndirect(state.indirect, DRAW.underlay);
    engineRuns(pass, [ENGINE.ground]);

    // What the traffic wrote on the ground, under everything that stands on it.
    const marks = markRing();
    if (marks) {
        pass.setPipeline(state.pipelines.marks);
        pass.setBindGroup(1, marks.group);
        pass.drawIndirect(marks.ring, 0);
    }

    pass.setPipeline(state.pipelines.sprite);
    pass.setVertexBuffer(0, state.buffers[SLOT.sprites]);
    pass.drawIndirect(state.indirect, DRAW.under);
    pass.setVertexBuffer(0, state.buffers[SLOT.standing]);
    pass.drawIndirect(state.indirect, DRAW.standing);
    movingRun(pass, MOVING.over, overOffset, DRAW.over);

    // The level above, over the bodies on the ground, its marks over it, and the bodies on it over those. A town with
    // nothing above its ground draws nothing in any of them, and no road above leaves out the marks', which read the whole ring.
    pass.setPipeline(state.pipelines.ground);
    pass.setVertexBuffer(0, state.buffers[SLOT.ground]);
    pass.setIndexBuffer(state.buffers[SLOT.indices], 'uint32');
    pass.drawIndexedIndirect(state.indirect, DRAW.aboveGround);

    pass.setPipeline(state.pipelines.overlay);
    pass.setVertexBuffer(0, state.buffers[SLOT.underlay], underlayAboveOffset);
    pass.drawIndirect(state.indirect, DRAW.underlayAbove);
    engineRuns(pass, [ENGINE.above]);

    if (marks && state.marksAbove) {
        pass.setPipeline(state.pipelines.marksAbove);
        pass.setBindGroup(1, marks.group);
        pass.drawIndirect(marks.ring, 0);
    }

    pass.setPipeline(state.pipelines.sprite);
    movingRun(pass, MOVING.above, aboveOffset, DRAW.above);

    // The clouds' shadows, over everything the town stands and under everything that reads it, where the desktop draws
    // them (TownRenderer.Recording.cs); a clear sky's frame puts the triangle out of the picture (town.wgsl).
    pass.setPipeline(state.pipelines.cloud);
    pass.draw(3);

    // The interface and everything that annotates a body, over all of it.
    pass.setPipeline(state.pipelines.overlay);
    engineRuns(pass, [ENGINE.over]);
    pass.setVertexBuffer(0, state.buffers[SLOT.overlay]);
    pass.drawIndirect(state.indirect, DRAW.overlay);

    state.bundle = pass.finish();
}

// The engine layers' runs (src/app/debug/EngineLayers.cs), each drawn from where a kernel wrote it and as many as the draw
// the kernel wrote says: the ground's, the level above's, and the run over the bodies. None where no layer of them has
// been turned on.
const ENGINE = { ground: 0, above: 1, over: 2 };
const ENGINE_DRAW_BYTES = 16;

function engineRuns(pass, runs) {
    const engine = state.engine;
    if (!engine) return;

    const quads = kernelBufferOf(engine.quads);
    const draws = kernelBufferOf(engine.draws);
    if (!quads || !draws) return;

    for (const run of runs) {
        pass.setVertexBuffer(0, quads, run * engine.runBytes);
        pass.drawIndirect(draws, run * ENGINE_DRAW_BYTES);
    }
}

/// The recording made again over what the last rebuild was handed.
function rebuildAsBefore() {
    rebuild(
        state.groundIndexCount, state.aboveIndexCount, state.overOffset, state.aboveOffset, state.underlayAboveOffset,
        state.marksAbove);
}

/// The engine layers' quads and draws, kernel buffers both, drawn from the next recording on — or none, for nought.
function engine(quads, draws, runBytes) {
    state.engine = quads > 0 ? { quads, draws, runBytes } : null;
    if (state.bundle) {
        rebuildAsBefore();
    }
}

// What moves, drawn where the device's emit wrote it (src/app/devicetick/DeviceSprites.cs) — its over run from the first
// instance and its run above from where the emit says, each as many as the draw the emit wrote — or, with no emit, from
// the frame's own runs as many as its counts say.
const MOVING = { over: 0, above: 1 };
const MOVING_DRAW_BYTES = 16;

function movingRun(pass, run, ownOffset, ownDraw) {
    const moving = state.moving;
    const instances = moving && kernelBufferOf(moving.instances);
    const draws = moving && kernelBufferOf(moving.draws);
    if (!instances || !draws) {
        pass.setVertexBuffer(0, state.buffers[SLOT.sprites], ownOffset);
        pass.drawIndirect(state.indirect, ownDraw);
        return;
    }

    pass.setVertexBuffer(0, instances, run === MOVING.above ? moving.aboveBytes : 0);
    pass.drawIndirect(draws, run * MOVING_DRAW_BYTES);
}

/// The ring of knots the marks are drawn out of — the device's own where it lays them (deviceMarks), the frame's copy of
/// the host's where it does not — its words the indirect buffer of the draw, and its group; none before the look is laid.
function markRing() {
    const device = state.deviceMarks;
    const knots = device ? kernelBufferOf(device.knots) : state.buffers[SLOT.markKnots];
    const ring = device ? kernelBufferOf(device.ring) : state.buffers[SLOT.markRing];
    const look = state.buffers[SLOT.markLook];
    if (!knots || !ring || !look) return null;

    const group = state.device.createBindGroup({
        label: 'marks',
        layout: state.markLayout,
        entries: [
            { binding: 0, resource: { buffer: look } },
            { binding: 1, resource: { buffer: knots } },
            { binding: 2, resource: { buffer: ring } },
        ],
    });
    return { group, ring };
}

/// The device's ring of knots and its words, kernel buffers both, drawn from the next recording on in place of the
/// frame's copy of the host's.
function deviceMarks(knots, ring) {
    state.deviceMarks = { knots, ring };
    if (state.bundle) {
        rebuildAsBefore();
    }
}

/// The emit's instances and draws, kernel buffers both, drawn from the next recording on.
function moving(instances, draws, aboveBytes) {
    state.moving = { instances, draws, aboveBytes };
    if (state.bundle) {
        rebuildAsBefore();
    }
}

/// Everything the device is holding for a town that is being taken down. The recording goes with it,
/// so a frame between this and the next rebuild draws nothing rather than reading a destroyed buffer.
function release() {
    state.bundle = null;
    state.engine = null;
    state.moving = null;
    state.deviceMarks = null;
    for (const slot in state.buffers) state.buffers[slot].destroy();
    for (const slot in state.textures) state.textures[slot].destroy();
    state.buffers = {};
    state.textures = {};
}

/// The whole frame: the memory the simulation just wrote, then the recording that reads it.
function frame(camera, under, over, above, overlay, underlay, underlayAbove, markKnots, markRingWords, frameCounts) {
    if (!state.bundle) return;

    const queue = state.device.queue;
    const given = bytes(frameCounts);
    const counts = state.counts;
    counts[DRAW.ground / 4] = state.groundIndexCount;
    counts[DRAW.ground / 4 + 1] = 1;
    counts[DRAW.underlay / 4] = 4;
    counts[DRAW.underlay / 4 + 1] = given[COUNT.underlay];
    counts[DRAW.under / 4] = 4;
    counts[DRAW.under / 4 + 1] = given[COUNT.under];
    counts[DRAW.standing / 4] = 4;
    counts[DRAW.standing / 4 + 1] = state.firstInstance
        ? given[COUNT.standing] : given[COUNT.standingFirst] + given[COUNT.standing];
    counts[DRAW.standing / 4 + 3] = state.firstInstance ? given[COUNT.standingFirst] : 0;
    counts[DRAW.over / 4] = 4;
    counts[DRAW.over / 4 + 1] = given[COUNT.over];
    counts[DRAW.aboveGround / 4] = state.aboveIndexCount;
    counts[DRAW.aboveGround / 4 + 1] = 1;
    counts[DRAW.aboveGround / 4 + 2] = state.groundIndexCount;
    counts[DRAW.underlayAbove / 4] = 4;
    counts[DRAW.underlayAbove / 4 + 1] = given[COUNT.underlayAbove];
    counts[DRAW.above / 4] = 4;
    counts[DRAW.above / 4 + 1] = given[COUNT.above];
    counts[DRAW.overlay / 4] = 4;
    counts[DRAW.overlay / 4 + 1] = given[COUNT.overlay];
    queue.writeBuffer(state.indirect, 0, counts);

    queue.writeBuffer(state.buffers[SLOT.camera], 0, bytes(camera));
    if (given[COUNT.under] > 0) queue.writeBuffer(state.buffers[SLOT.sprites], 0, bytes(under));
    if (given[COUNT.over] > 0) queue.writeBuffer(state.buffers[SLOT.sprites], state.overOffset, bytes(over));
    if (given[COUNT.above] > 0) queue.writeBuffer(state.buffers[SLOT.sprites], state.aboveOffset, bytes(above));
    if (given[COUNT.overlay] > 0) queue.writeBuffer(state.buffers[SLOT.overlay], 0, bytes(overlay));
    if (given[COUNT.underlay] > 0) queue.writeBuffer(state.buffers[SLOT.underlay], 0, bytes(underlay));
    if (given[COUNT.underlayAbove] > 0) {
        queue.writeBuffer(state.buffers[SLOT.underlay], state.underlayAboveOffset, bytes(underlayAbove));
    }

    // The host's knots written since the last frame, and the ring's words: none where the device keeps the ring.
    if (markKnots.byteLength > 0) queue.writeBuffer(state.buffers[SLOT.markKnots], given[COUNT.markKnotsAt], bytes(markKnots));
    if (markRingWords.byteLength > 0) queue.writeBuffer(state.buffers[SLOT.markRing], 0, bytes(markRingWords));

    const encoder = state.device.createCommandEncoder();
    const clock = state.drawClock;
    const timed = clock?.take() ?? null;
    const pass = encoder.beginRenderPass({
        colorAttachments: [{
            view: state.context.getCurrentTexture().createView(),
            clearValue: { r: 0, g: 0, b: 0, a: 1 },
            loadOp: 'clear',
            storeOp: 'store',
        }],
        timestampWrites: timed ? clock.writes(timed, true, true) : undefined,
    });

    pass.executeBundles([state.bundle]);
    pass.end();
    if (timed) clock.resolve(encoder, timed);
    queue.submit([encoder.finish()]);
    if (timed) clock.land(timed);
}

/// The most device pixels the canvas is drawn at to the point (src/core/config `PageMostPixelRatio`), handed over once
/// the figures are read and the canvas laid again at it. **The interface is laid in points either way**: the scale the
/// run is told is the one drawn at, so a pointer and a panel stand where they did.
function mostPixelRatio(ratio) {
    state.mostPixelRatio = ratio;
    resize();
}

function resize() {
    const scale = Math.min(window.devicePixelRatio || 1, state.mostPixelRatio);
    const width = Math.max(1, Math.round(state.canvas.clientWidth * scale));
    const height = Math.max(1, Math.round(state.canvas.clientHeight * scale));
    if (state.canvas.width === width && state.canvas.height === height) return;

    state.canvas.width = width;
    state.canvas.height = height;
    state.context.configure({ device: state.device, format: state.format, alphaMode: 'opaque' });
    state.axes[AXIS.width] = width;
    state.axes[AXIS.height] = height;
    state.axes[AXIS.scale] = scale;
    state.axes[AXIS.resized] = 1;
}

/// The device's own time, said on the console every so often, so a log saved off a handset carries it (log.js).
function sayTheDevice() {
    const said = state.deviceSaid;
    said.drawMs += state.axes[AXIS.gpuDrawMs];
    said.draws += state.axes[AXIS.gpuDraws];
    said.kernelsMs += state.axes[AXIS.gpuKernelsMs];
    said.batches += state.axes[AXIS.gpuBatches];
    const now = performance.now();
    if (now - said.at < DEVICE_SAID_EVERY_MS || said.draws + said.batches === 0) return;

    const mean = (ms, count) => count > 0 ? `${(ms / count).toFixed(2)} ms over ${count}` : 'none';
    console.log(`the device: a frame's pass ${mean(said.drawMs, said.draws)}, a kernels' batch ${mean(said.kernelsMs, said.batches)}`);
    state.deviceSaid = { at: now, drawMs: 0, draws: 0, kernelsMs: 0, batches: 0 };
}

const DEVICE_SAID_EVERY_MS = 10000;

/// What the page has seen since the last frame asked, copied into the run's own memory. What is
/// edge-triggered — a press, a click, a notch, a resize — is handed over and forgotten here, so the
/// run reads each of them exactly once.
function pump(keys, axes, inbox, outbox) {
    [state.axes[AXIS.gpuDrawMs], state.axes[AXIS.gpuDraws]] = state.drawClock?.drain() ?? [0, 0];
    [state.axes[AXIS.gpuKernelsMs], state.axes[AXIS.gpuBatches]] = kernelTimes();
    sayTheDevice();
    keys.set(state.keys);
    axes.set(state.axes);
    state.keys.fill(0, PRESSED, PRESSED + KEY_COUNT);
    state.axes[AXIS.scroll] = 0;
    state.axes[AXIS.clickButton] = -1;
    state.axes[AXIS.resized] = 0;
    pumpLink(inbox, outbox);
}

/// The socket to a served town (MP-3), opened once by the run (ServerLink.Web.cs) and read and written only
/// inside the pump, so a frame's crossing count is the same joined or not (WEB-2).
const link = { socket: null, state: 0, queue: [] };

/// ServerLink's states: none asked for, opening, open, closed.
const LINK = { none: 0, opening: 1, open: 2, closed: 3 };

/// Where a link buffer's messages start: its used length, then (the inbox only) the link's state.
const LINK_HEAD = 8;

function join(url) {
    link.socket = new WebSocket(url);
    link.socket.binaryType = 'arraybuffer';
    link.state = LINK.opening;
    link.socket.onopen = () => link.state = LINK.open;
    link.socket.onmessage = e => link.queue.push(new Uint8Array(e.data));
    link.socket.onclose = () => link.state = LINK.closed;
    link.socket.onerror = () => link.state = LINK.closed;
}

/// Four bytes of scratch a length is said in, kept so a pump makes nothing for the collector.
const linkWord = new Uint8Array(4);
const linkWordView = new DataView(linkWord.buffer);

/// A length read off four bytes little-endian.
function wordAt(array, at) {
    return (array[at] | (array[at + 1] << 8) | (array[at + 2] << 16) | (array[at + 3] << 24)) >>> 0;
}

/// What the run wrote since the last pump sent, each message whole; then what arrived, as many whole messages as the
/// inbox holds, the rest kept for the next pump. Each message is its length, four bytes little-endian, then itself.
/// **Written straight into the run's memory**, a message at a time: no buffer of the whole is laid and copied again.
function pumpLink(inbox, outbox) {
    if (link.socket === null) return;

    // A socket copies what it is handed as it is handed it, so a window onto the run's memory is all a send needs.
    const out = bytes(outbox);
    const sent = wordAt(out, 0);
    if (sent > 0) {
        for (let at = 4; at + 4 <= 4 + sent;) {
            const length = wordAt(out, at);
            if (link.state === LINK.open) link.socket.send(out.subarray(at + 4, at + 4 + length));
            at += 4 + length;
        }

        linkWordView.setUint32(0, 0, true);
        outbox.set(linkWord, 0);
    }

    let used = 0;
    const room = inbox.length - LINK_HEAD;

    // A message no inbox could hold is never one the server sends; the link is given up rather than wedged.
    if (link.queue.length > 0 && 4 + link.queue[0].length > room) {
        link.queue.length = 0;
        link.socket.close();
        link.state = LINK.closed;
    }

    let taken = 0;
    while (taken < link.queue.length && used + 4 + link.queue[taken].length <= room) {
        const message = link.queue[taken++];
        linkWordView.setUint32(0, message.length, true);
        inbox.set(linkWord, LINK_HEAD + used);
        inbox.set(message, LINK_HEAD + used + 4);
        used += 4 + message.length;
    }

    if (taken > 0) link.queue.splice(0, taken);
    linkWordView.setUint32(0, used, true);
    inbox.set(linkWord, 0);
    linkWord[0] = link.state;
    inbox.set(linkWord.subarray(0, 1), 4);
}

/// The fingers on the glass, by the id the browser gave each, in the order they came down (CTL-9).
/// **A Map and not an array**, because that order is what makes "the first two" mean the same pair from
/// one frame to the next: a finger lifted out of the middle of three must not renumber the others.
const fingers = new Map();

/// One contact, in the canvas's own pixels — the same space the pointer is written in.
function at(e) {
    const scale = state.axes[AXIS.scale];
    return { x: e.offsetX * scale, y: e.offsetY * scale };
}

/// How many are down and where the first two are. Written on every change rather than once a frame,
/// because a pump is a copy and this is what it copies.
function writeFingers() {
    const down = [...fingers.values()];
    state.axes[AXIS.touchCount] = down.length;
    if (down.length > 0) {
        state.axes[AXIS.touchOneX] = down[0].x;
        state.axes[AXIS.touchOneY] = down[0].y;
    }

    if (down.length > 1) {
        state.axes[AXIS.touchTwoX] = down[1].x;
        state.axes[AXIS.touchTwoY] = down[1].y;
    }
}

function listen() {
    addEventListener('keydown', e => {
        const key = KEYS[e.code];
        if (key === undefined) return;

        // The browser's own bindings for the keys the town uses: F11 is the page's fullscreen and
        // space scrolls it, and both belong to the town while it is being looked at.
        e.preventDefault();
        if (!state.keys[DOWN + key]) state.keys[PRESSED + key] = 1;
        state.keys[DOWN + key] = 1;
    });

    addEventListener('keyup', e => {
        const key = KEYS[e.code];
        if (key !== undefined) state.keys[DOWN + key] = 0;
    });

    // A page that loses focus loses every key with it, and every finger too, or the town drives off on
    // a key nobody is holding down any more and stays pinched between two that are not there.
    addEventListener('blur', () => {
        state.keys.fill(0, DOWN, DOWN + KEY_COUNT);
        fingers.clear();
        writeFingers();
    });

    state.canvas.addEventListener('pointermove', e => {
        if (e.pointerType === 'touch') {
            if (!fingers.has(e.pointerId)) return;

            fingers.set(e.pointerId, at(e));
            writeFingers();
            if (!e.isPrimary) return;
        }

        const { x, y } = at(e);
        state.axes[AXIS.pointerX] = x;
        state.axes[AXIS.pointerY] = y;
    });

    state.canvas.addEventListener('pointerdown', e => {
        // **The canvas keeps the pointer for the whole gesture.** Without it a drag that leaves the
        // element stops reporting, and the town is left being dragged by a finger nobody can see. It is
        // refused for a pointer the browser is not tracking — a synthetic event, chiefly — and a throw
        // here would take the rest of the press with it.
        try {
            state.canvas.setPointerCapture(e.pointerId);
        } catch {
            // Nothing to do: the gesture still reports while it is over the canvas, which is the page.
        }

        if (e.pointerType === 'touch') {
            fingers.set(e.pointerId, at(e));
            writeFingers();

            // **Only the first finger is the button** (CTL-9): a second one landing is the camera's, and
            // reported as a press it would be a second click on whatever it came down on.
            if (!e.isPrimary) return;
        }

        // A tap has no move before it, so the pointer is put where the press was or the release resolves
        // against wherever the mouse was last seen.
        const { x, y } = at(e);
        state.axes[AXIS.pointerX] = x;
        state.axes[AXIS.pointerY] = y;
        state.keys[BUTTONS + e.button] = 1;
        state.axes[AXIS.clickButton] = e.button;
        state.axes[AXIS.clickX] = x;
        state.axes[AXIS.clickY] = y;
    });

    const lifted = e => {
        if (e.pointerType === 'touch') {
            fingers.delete(e.pointerId);
            writeFingers();
            if (!e.isPrimary) return;
        }

        state.keys[BUTTONS + e.button] = 0;
    };

    addEventListener('pointerup', lifted);
    // A gesture the browser took over — a system edge swipe, a call arriving — is fingers that never
    // come up, and a town left mid-pinch by one.
    addEventListener('pointercancel', lifted);
    state.canvas.addEventListener('contextmenu', e => e.preventDefault());
    state.canvas.addEventListener('wheel', e => {
        e.preventDefault();
        state.axes[AXIS.scroll] += e.deltaY > 0 ? -1 : 1;
    }, { passive: false });

    addEventListener('resize', resize);
}

function fullscreen() {
    if (document.fullscreenElement) document.exitFullscreen();
    else state.canvas.requestFullscreen();
}

/// The tab, closed — the way out of the game, all the way out (OBS-2g).
///
/// **A browser grants this only to a page it opened itself**, so a tab somebody typed a link into is
/// one `close` is ignored on, with a line in the console and nothing else. There is no way around that
/// and nothing here pretends otherwise: the run has stopped either way, and where the tab stays the
/// banner under the canvas is what says so.
function shut() {
    window.close();
}

/// What the desktop puts on stdout, where a page can be read. **Two places, and which one is decided
/// by whether the town is up yet**: it is the opening's own line while the opening is in front of the
/// canvas (loading.js), and a banner under the canvas once the town is behind it. An empty line is the
/// boot saying it has finished — it takes the opening away, and takes the banner away after that.
function say(line) {
    if (opening.isOpen()) {
        if (line === '') {
            opening.opened();
        } else {
            // A new stage, and nothing counted yet: the bar sweeps until something says how much of
            // this one there is (`decode`), and a bar left full from the last stage would be a lie.
            opening.stage(line);
            opening.progress(0, 0);
        }

        return;
    }

    const banner = document.getElementById('banner');
    if (!banner) return;

    banner.textContent = line;
    banner.style.display = line === '' ? 'none' : 'block';
}

/// One file, parked here until something on this side is asked to do with it.
///
/// **Two crossings and not one, and the reason is rule 3.** A MemoryView is a window onto the
/// WebAssembly heap handed *out*, and there is no shape that hands one back — so the fetch answers a
/// length, the run makes an array at it, and `take` copies into that.
///
/// **It is filled by `grab` or by `park` and read by `take` or by `picture`**, and it stands until the
/// next one fills it. That is what lets a sheet be written into the run's file system and decoded by
/// the browser without the bytes moving twice: one fetch, one park, two readers.
let parked = null;

/// A file already in flight or already here, by the path it was asked for: either the bytes or the
/// promise of them. Emptied as `grab` reads them, so nothing here is held twice or held after it has
/// been taken.
const held = new Map();

/// Files asked for before anything wants them.
///
/// **This is what puts the town beside the engine on the wire.** The art and the runtime are about
/// three megabytes each and neither needs the other, so asked for in turn a page waits for the sum of
/// them, and without this the art is not asked for until the runtime has started and a map has been
/// picked. Started here, the two come down together.
///
/// **Nothing above it changes**: `grab` reads a prefetched file where it would have fetched one, and
/// **a prefetch that fails costs an ordinary fetch and nothing else** — a name that is wrong here is
/// slow rather than broken.
///
/// The paths are one string, newline apart, because that is how a list crosses the wall (`unpack`).
export function prefetch(list) {
    for (const path of list.split('\n')) {
        if (path !== '' && !held.has(path)) held.set(path, file(path));
    }
}

/// The whole of the town's art in one response, held exactly as a prefetched file is, answering the
/// paths it turned out to hold — newline apart, which is how a list crosses the wall.
///
/// **One round trip and not three hundred.** That is the whole reason it exists: the files are small,
/// so what was being waited on was latency and never bytes.
///
/// **A plain tar, because the format is somebody else's.** Thirty lines read it, nothing here had to be
/// invented, and `tar tvf` says what a published archive holds. The gzip around it is undone by
/// `DecompressionStream` — the one decompressor a page has that its .NET runtime does not, which is the
/// same fact that keeps the towns on gzip rather than brotli.
///
/// **Every file is a view onto the one buffer and not a copy of itself.** The archive stands until the
/// last of it has been read out, which costs its own size once instead of twice.
///
/// **It is grabbed and not fetched**, which is what lets the page start it before the runtime that asks
/// for it: an archive already prefetched is read out of `held`, and one that is not is fetched where it
/// stands. A `fetch` of its own here would be a second copy of three megabytes.
async function unpack(path) {
    await grab(path);
    const bytes = new Uint8Array(
        await new Response(new Blob([parked]).stream().pipeThrough(new DecompressionStream('gzip')))
            .arrayBuffer());

    const names = [];
    for (let at = 0; at + TAR_BLOCK <= bytes.length;) {
        const header = bytes.subarray(at, at + TAR_BLOCK);
        if (header[0] === 0) break; // the run of zero blocks an archive ends with

        const name = field(header, 0, 100);
        const prefix = field(header, 345, 155);
        const size = parseInt(field(header, 124, 12), 8) || 0;
        const regular = header[156] === 0x30 || header[156] === 0;
        at += TAR_BLOCK;

        if (regular) {
            const whole = prefix === '' ? name : `${prefix}/${name}`;
            held.set(whole, bytes.subarray(at, at + size));
            names.push(whole);
        }

        at += Math.ceil(size / TAR_BLOCK) * TAR_BLOCK;
    }

    return names.join('\n');
}

const TAR_BLOCK = 512;

/// One header field: fixed width, and everything from the first NUL on is padding.
function field(header, at, width) {
    const raw = header.subarray(at, at + width);
    const end = raw.indexOf(0);
    return new TextDecoder().decode(end < 0 ? raw : raw.subarray(0, end)).trim();
}

/// One file the page was served, out of the batch if it is in one. **A path is relative to the page**:
/// `fetch` resolves it against `document.baseURI`, which is what a path in the manifest is written
/// against, so a page served from anywhere under a host reads its own files.
async function grab(path) {
    const batched = held.get(path);
    held.delete(path);
    if (batched !== undefined) {
        try {
            // Bytes or the promise of them, and awaiting the first of those costs a microtask.
            parked = await batched;
            return parked.byteLength;
        } catch {
            // A prefetch that did not arrive is a file that has not been fetched yet, and no more.
        }
    }

    parked = await file(path);
    return parked.byteLength;
}

/// One file already here, parked as `grab` parks it — or −1 where it is still on the wire or was never asked for, which
/// `grab` waits for. **Synchronous**, so a batch already unpacked is laid without a turn of the event loop a file.
function grabHeld(path) {
    const batched = held.get(path);
    if (!(batched instanceof Uint8Array)) return -1;

    held.delete(path);
    parked = batched;
    return parked.byteLength;
}

async function file(path) {
    const response = await fetch(path);
    if (!response.ok) throw new Error(`${response.status} ${response.statusText} — ${path}`);

    return new Uint8Array(await response.arrayBuffer());
}

/// Bytes out of the run's own memory, for the one picture that was never fetched: the glyph sheet ships
/// inside the assembly. Copied, because the view it arrives as does not outlive this call.
function park(view) {
    parked = bytes(view).slice();
}

/// The parked bytes into the run's own memory.
function take(into) {
    bytes(into).set(parked);
}

/// Every sheet the run has had decoded, by the path it was fetched from.
///
/// **This is the browser's image codec standing in for a library the page would otherwise ship**, and
/// it is split in two because only half of it can wait. `createImageBitmap` is a promise and the atlas
/// is packed from inside `Game.Start`, which a frame reaches and which cannot await; `drawImage` and
/// `getImageData` are synchronous. So `decode` is called where the art is fetched, where waiting is
/// allowed, and `texels` is called where the packer stands.
///
/// **What is kept is a bitmap, not a page of texels.** The browser stores an ImageBitmap as it pleases
/// — often on the GPU — and the run's own heap holds nothing until one sheet is asked for.
///
/// **They are kept for the run and not dropped after the atlas is built**, because the art is every
/// town's: picking a second map on the menu packs the atlas again out of the same sheets, and a bitmap
/// closed after the first one would be a decode that had to happen inside a frame.
const pictures = new Map();

/// The parked file, decoded and kept under `path`: the one sheet that arrives on its own, which is the
/// typeface out of the assembly. **The art is a batch and goes through `decode`.**
///
/// **The bytes are taken before the first await**, because `parked` is one slot and the next `grab` is
/// entitled to overwrite it the moment this yields.
async function picture(path) {
    const file = parked;
    pictures.set(path, await made(file));
}

/// Every sheet of a batch, decoded at once out of the files the fetch is holding.
///
/// **Serially they are four times slower.** The town's hundred and seventy-four sheets are 216 ms
/// decoded one after the next and 57 ms asked for together, because a browser decodes on threads this
/// page has not got — and one after the next is what a loop awaiting each one in turn comes to.
/// They are all kept for the run in any case (`pictures`), so asking for them all at once costs no
/// more memory than asking in tens would.
///
/// **The names are the run's own and not the fetch's**, because it is the run that asks for a sheet's
/// texels back: what a page fetched as `assets/…` the file system holds at `/assets/…`, and the one
/// leading slash is the whole of the difference (src/app/main/web/Data.cs).
async function decode(list, saying) {
    const names = list.split('\n').filter(name => name !== '');
    let done = 0;

    say(saying);
    opening.progress(0, names.length);

    await Promise.all(names.map(async name => {
        const file = held.get(name.replace(/^\//, ''));
        if (file === undefined) throw new Error(`no bytes fetched for ${name}`);

        pictures.set(name, await made(await file));
        opening.progress(++done, names.length);
    }));
}

/// One bitmap out of one file's bytes.
///
/// **Both options are load-bearing.** Left to itself the browser premultiplies alpha and may put the
/// display's colour profile through the texels, and this town's art is alpha-cut sprites drawn at
/// their own texel grid: either one is a sheet that does not match what the desktop draws.
function made(file) {
    return createImageBitmap(new Blob([file]), {
        premultiplyAlpha: 'none',
        colorSpaceConversion: 'none',
    });
}

/// The texels of a picture already decoded, parked for `take` exactly as a fetched file is.
///
/// The canvas is made at the picture's size and thrown away with it: it is one sheet at a time, and the
/// atlas above is careful never to hold more than a page's worth at once.
function texels(path) {
    const bitmap = pictures.get(path);
    if (!bitmap) throw new Error(`no picture decoded for ${path} — nothing fetched it`);

    const canvas = new OffscreenCanvas(bitmap.width, bitmap.height);
    const context = canvas.getContext('2d', { willReadFrequently: true, colorSpace: 'srgb' });
    context.drawImage(bitmap, 0, 0);
    parked = new Uint8Array(context.getImageData(0, 0, bitmap.width, bitmap.height).data.buffer);
    return parked.byteLength;
}

export { useHeap } from './compute.js'

export const town = {
    ...compute,
    start, mostPixelRatio, reserve, buffer, texture, sheetFormat, sheetBlocks, rebuild, release, frame, pump, join, fullscreen, shut, say, engine, moving, deviceMarks,
    progress: opening.progress, standUp: opening.standUp, standing: opening.standing, painted: opening.painted, prefetch, unpack, grab, grabHeld, park, take, picture, decode, texels,
    ticker: (step, between) => {
        onLanded(between);
        const next = () => {
            if (step()) requestAnimationFrame(next);
        };

        requestAnimationFrame(next);
    },
};
