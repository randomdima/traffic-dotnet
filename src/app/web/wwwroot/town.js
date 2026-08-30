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

const SLOT = { ground: 0, indices: 1, sprites: 2, overlay: 3, underlay: 4, camera: 5, table: 6 };
const TEXTURE = { pages: 0, glyphs: 1, tread: 2, surfaces: 3 };

// Draws in painter's order, at their offsets in the indirect buffer. The ground is indexed and so is
// five words; the other three are four.
const DRAW = { ground: 0, underlay: 32, sprites: 48, overlay: 64 };
const INDIRECT_BYTES = 80;

// The keys the town reads, in the order src/runtime/web/AppWindow.Web.cs names them. The two lists are
// one list, and a key added to either without the other is a key that does nothing.
const KEYS = {
    KeyA: 0, KeyD: 1, KeyS: 2, KeyW: 3, KeyE: 4, KeyR: 5,
    ArrowUp: 6, ArrowDown: 7, ArrowLeft: 8, ArrowRight: 9,
    Escape: 10, F11: 11, Backquote: 12, Pause: 13, Space: 14,
    Digit1: 15, Digit2: 16, Digit3: 17,
    ShiftLeft: 18, ShiftRight: 19,
};

const KEY_COUNT = 32;
const DOWN = 0;
const PRESSED = KEY_COUNT;
const BUTTONS = KEY_COUNT * 2;

const AXIS = {
    pointerX: 0, pointerY: 1, scroll: 2, clickX: 3, clickY: 4, clickButton: 5,
    width: 6, height: 7, scale: 8, resized: 9,
};

/// How many of them there are, which is the length the run's own array is made at. **Whether the
/// run is over is not one of them**: an axis is something the page saw, a pump copies the page's
/// copy over the run's, and the page has no opinion about that one.
const AXIS_COUNT = 10;

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
    indexCount: 0,
};

/// The device, the canvas and the shaders. Answers the reason it could not, or "" — a page that cannot
/// have a device says so in words rather than drawing nothing.
async function start(wgsl) {
    if (!navigator.gpu) {
        return 'This browser has no WebGPU. It wants Chrome or Edge 113+, Safari 26+, or a Firefox where it has shipped.';
    }

    const adapter = await navigator.gpu.requestAdapter();
    if (!adapter) return 'No WebGPU adapter: the browser has the API but no device it will hand out.';

    state.device = await adapter.requestDevice();
    state.device.addEventListener('uncapturederror', e => console.error('WebGPU:', e.error.message));
    state.device.lost.then(info => console.error('WebGPU device lost:', info.message));

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
        { binding: 0, visibility: GPUShaderStage.VERTEX, buffer: { type: 'uniform' } },
        { binding: 1, visibility: GPUShaderStage.VERTEX, buffer: { type: 'uniform' } },
        { binding: 2, visibility: GPUShaderStage.FRAGMENT, texture: { viewDimension: '2d-array' } },
    ];

    for (let binding = 3; binding <= 9; binding++) {
        entries.push({ binding, visibility: GPUShaderStage.FRAGMENT, texture: {} });
    }

    entries.push({ binding: 10, visibility: GPUShaderStage.FRAGMENT, sampler: {} });
    entries.push({ binding: 11, visibility: GPUShaderStage.FRAGMENT, sampler: {} });
    state.layout = state.device.createBindGroupLayout({ entries, label: 'town' });
}

function buildPipelines(module) {
    const layout = state.device.createPipelineLayout({ bindGroupLayouts: [state.layout] });
    const blend = {
        color: { srcFactor: 'src-alpha', dstFactor: 'one-minus-src-alpha', operation: 'add' },
        alpha: { srcFactor: 'src-alpha', dstFactor: 'one-minus-src-alpha', operation: 'add' },
    };

    const float2 = 'float32x2';
    state.pipelines.ground = state.device.createRenderPipeline({
        layout,
        vertex: {
            module, entryPoint: 'groundVertex',
            buffers: [{
                arrayStride: 32,
                attributes: [
                    { shaderLocation: 0, offset: 0, format: float2 },
                    { shaderLocation: 1, offset: 8, format: float2 },
                    { shaderLocation: 2, offset: 16, format: 'float32x3' },
                    { shaderLocation: 3, offset: 28, format: 'uint32' },
                ],
            }],
        },
        fragment: { module, entryPoint: 'groundFragment', targets: [{ format: state.format }] },
        primitive: { topology: 'triangle-list' },
    });

    state.pipelines.sprite = state.device.createRenderPipeline({
        layout,
        vertex: {
            module, entryPoint: 'spriteVertex',
            buffers: [{
                arrayStride: 56, stepMode: 'instance',
                attributes: [
                    { shaderLocation: 0, offset: 0, format: float2 },
                    { shaderLocation: 1, offset: 8, format: float2 },
                    { shaderLocation: 2, offset: 16, format: float2 },
                    { shaderLocation: 3, offset: 24, format: float2 },
                    { shaderLocation: 4, offset: 32, format: 'float32x4' },
                    { shaderLocation: 5, offset: 48, format: 'uint32' },
                    { shaderLocation: 6, offset: 52, format: 'float32' },
                ],
            }],
        },
        fragment: { module, entryPoint: 'spriteFragment', targets: [{ format: state.format, blend }] },
        primitive: { topology: 'triangle-strip' },
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
function rebuild(indexCount) {
    state.indexCount = indexCount;
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
        { binding: 4, resource: state.textures[TEXTURE.tread].createView() },
    ];

    for (let surface = 0; surface < 5; surface++) {
        entries.push({ binding: 5 + surface, resource: state.textures[TEXTURE.surfaces + surface].createView() });
    }

    entries.push({ binding: 10, resource: state.samplers.clamped });
    entries.push({ binding: 11, resource: state.samplers.repeated });
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

    pass.setPipeline(state.pipelines.sprite);
    pass.setVertexBuffer(0, state.buffers[SLOT.sprites]);
    pass.drawIndirect(state.indirect, DRAW.sprites);

    // The interface and everything that annotates a body, over all of it.
    pass.setPipeline(state.pipelines.overlay);
    pass.setVertexBuffer(0, state.buffers[SLOT.overlay]);
    pass.drawIndirect(state.indirect, DRAW.overlay);

    state.bundle = pass.finish();
}

/// Everything the device is holding for a town that is being taken down. The recording goes with it,
/// so a frame between this and the next rebuild draws nothing rather than reading a destroyed buffer.
function release() {
    state.bundle = null;
    for (const slot in state.buffers) state.buffers[slot].destroy();
    for (const slot in state.textures) state.textures[slot].destroy();
    state.buffers = {};
    state.textures = {};
}

/// The whole frame: the memory the simulation just wrote, then the recording that reads it.
function frame(camera, sprites, overlay, underlay, spriteCount, overlayCount, underlayCount) {
    if (!state.bundle) return;

    const queue = state.device.queue;
    const counts = state.counts;
    counts[0] = state.indexCount;
    counts[1] = 1;
    counts[DRAW.underlay / 4] = 4;
    counts[DRAW.underlay / 4 + 1] = underlayCount;
    counts[DRAW.sprites / 4] = 4;
    counts[DRAW.sprites / 4 + 1] = spriteCount;
    counts[DRAW.overlay / 4] = 4;
    counts[DRAW.overlay / 4 + 1] = overlayCount;
    queue.writeBuffer(state.indirect, 0, counts);

    queue.writeBuffer(state.buffers[SLOT.camera], 0, bytes(camera));
    if (spriteCount > 0) queue.writeBuffer(state.buffers[SLOT.sprites], 0, bytes(sprites));
    if (overlayCount > 0) queue.writeBuffer(state.buffers[SLOT.overlay], 0, bytes(overlay));
    if (underlayCount > 0) queue.writeBuffer(state.buffers[SLOT.underlay], 0, bytes(underlay));

    const encoder = state.device.createCommandEncoder();
    const pass = encoder.beginRenderPass({
        colorAttachments: [{
            view: state.context.getCurrentTexture().createView(),
            clearValue: { r: 0, g: 0, b: 0, a: 1 },
            loadOp: 'clear',
            storeOp: 'store',
        }],
    });

    pass.executeBundles([state.bundle]);
    pass.end();
    queue.submit([encoder.finish()]);
}

function resize() {
    const scale = window.devicePixelRatio || 1;
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

/// What the page has seen since the last frame asked, copied into the run's own memory. What is
/// edge-triggered — a press, a click, a notch, a resize — is handed over and forgotten here, so the
/// run reads each of them exactly once.
function pump(keys, axes) {
    keys.set(state.keys);
    axes.set(state.axes);
    state.keys.fill(0, PRESSED, PRESSED + KEY_COUNT);
    state.axes[AXIS.scroll] = 0;
    state.axes[AXIS.clickButton] = -1;
    state.axes[AXIS.resized] = 0;
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

    // A page that loses focus loses every key with it, or the town drives off on a key nobody is
    // holding down any more.
    addEventListener('blur', () => state.keys.fill(0, DOWN, DOWN + KEY_COUNT));

    state.canvas.addEventListener('pointermove', e => {
        const scale = state.axes[AXIS.scale];
        state.axes[AXIS.pointerX] = e.offsetX * scale;
        state.axes[AXIS.pointerY] = e.offsetY * scale;
    });

    state.canvas.addEventListener('pointerdown', e => {
        const scale = state.axes[AXIS.scale];
        state.keys[BUTTONS + e.button] = 1;
        state.axes[AXIS.clickButton] = e.button;
        state.axes[AXIS.clickX] = e.offsetX * scale;
        state.axes[AXIS.clickY] = e.offsetY * scale;
    });

    addEventListener('pointerup', e => { state.keys[BUTTONS + e.button] = 0; });
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

/// A line under the canvas: what the desktop puts on stdout, where a page can be read. An empty line
/// takes the banner away, which is what the first frame does.
function say(line) {
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

/// One file the page was served. **A path is relative to the page**: `fetch` resolves it against
/// `document.baseURI`, which is what a path in the manifest is written against, so a page served from
/// anywhere under a host reads its own files.
async function grab(path) {
    const response = await fetch(path);
    if (!response.ok) throw new Error(`${response.status} ${response.statusText} — ${path}`);

    parked = new Uint8Array(await response.arrayBuffer());
    return parked.byteLength;
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
/// `getImageData` are synchronous. So `picture` is called at boot, where waiting is allowed, and
/// `texels` is called where the packer stands.
///
/// **What is kept is a bitmap, not a page of texels.** The browser stores an ImageBitmap as it pleases
/// — often on the GPU — and the run's own heap holds nothing until one sheet is asked for.
///
/// **They are kept for the run and not dropped after the atlas is built**, because the art is every
/// town's: picking a second map on the menu packs the atlas again out of the same sheets, and a bitmap
/// closed after the first one would be a decode that had to happen inside a frame.
const pictures = new Map();

/// The parked file, decoded and kept under `path`.
///
/// **The bytes are taken before the first await**, because `parked` is one slot and the next `grab` is
/// entitled to overwrite it the moment this yields.
///
/// **Both options are load-bearing.** Left to itself the browser premultiplies alpha and may put the
/// display's colour profile through the texels, and this town's art is alpha-cut sprites drawn at their
/// own texel grid: either one is a sheet that no longer matches what the desktop draws.
async function picture(path) {
    const file = parked;
    pictures.set(path, await createImageBitmap(new Blob([file]), {
        premultiplyAlpha: 'none',
        colorSpaceConversion: 'none',
    }));
}

/// The texels of a picture already decoded, parked for `take` exactly as a fetched file is.
///
/// The canvas is made at the picture's size and thrown away with it: it is one sheet at a time, and the
/// atlas above is careful never to hold more than a page's worth at once.
function texels(path) {
    const bitmap = pictures.get(path);
    if (!bitmap) throw new Error(`no picture decoded for ${path} — the boot did not fetch it`);

    const canvas = new OffscreenCanvas(bitmap.width, bitmap.height);
    const context = canvas.getContext('2d', { willReadFrequently: true, colorSpace: 'srgb' });
    context.drawImage(bitmap, 0, 0);
    parked = new Uint8Array(context.getImageData(0, 0, bitmap.width, bitmap.height).data.buffer);
    return parked.byteLength;
}

export const town = {
    start, reserve, buffer, texture, rebuild, release, frame, pump, fullscreen, say, grab, park, take,
    picture, texels,
    ticker: step => {
        const next = () => {
            if (step()) requestAnimationFrame(next);
        };

        requestAnimationFrame(next);
    },
};
