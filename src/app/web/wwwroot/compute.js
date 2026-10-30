// The world's kernels on the page's device: the browser's half of src/runtime/ComputeKernel.cs, ComputeBatch.cs and
// GpuBuffer.cs, whose .Web.cs halves call in here. town.js hands over the device and exports these with its own.
//
// Four things a page does that the desktop's Vulkan does not, each for want of something WebGPU has not got:
//
// 1. PUSH CONSTANTS ARE A WINDOW OF ONE UNIFORM BUFFER. naga writes a kernel's push block as `var<immediate>`, which
//    no browser takes; it is read as a uniform at group 1, and each dispatch binds its own 256-byte window of the
//    batch's constants by a dynamic offset.
// 2. A LOOP ON A SHARED FIGURE IS SAID TO BE EVERY THREAD'S. WGSL refuses a barrier under a branch it cannot prove
//    uniform, and a figure read out of workgroup memory is one it cannot; the kernels read every such figure through a
//    function named workgroupUniform…, whose loads are made `workgroupUniformLoad` here.
// 3. NOTHING IS MAPPED. What the host writes crosses ahead of each batch from the run's own memory, and what it reads
//    is copied out after the batch and mapped when the device is done, then written into the run's memory where the
//    host keeps it — and the batch's serial with it, which is how the run knows, with no crossing at all.
// 4. A FILL OF ANY WORD BUT NOUGHT IS A KERNEL, since a page clears and does not fill.

const STRIDE = 6;
const DISPATCH = 1;
const INDIRECT = 2;
const COPY = 3;
const FILL = 4;
const SEND = 5;
const READ = 6;
const PUSH_STRIDE = 256;

/// What the world's kernels need of a device beyond the defaults, and the least of each they can run with: the most
/// storage buffers a kernel binds (world_signals.comp, compact.comp) and the solver's one workgroup of 1024 threads.
const NEEDED = {
    maxStorageBuffersPerShaderStage: 16,
    maxComputeWorkgroupSizeX: 1024,
    maxComputeInvocationsPerWorkgroup: 1024,
};

/// And what a big town's tables want, asked for as far as the adapter goes.
const WANTED = ['maxStorageBufferBindingSize', 'maxBufferSize'];

const kernels = {
    device: null,
    heap: null,
    fits: false,
    buffers: new Map(),
    pipelines: new Map(),
    groups: new Map(),
    pushLayout: null,
    pushBuffer: null,
    pushGroup: null,
    scratch: null,
    fill: null,
    landed: null,
    proofs: [],
    clock: null,
};

/// What runs each time a batch has landed: the ticks a frame put off until the device answered (town.js `ticker`).
export function onLanded(then) {
    kernels.landed = then;
}

/// The limits to ask the adapter for: what the kernels need, where it has it, and as much room as it has.
export function kernelLimits(adapter) {
    const limits = {};
    for (const [name, least] of Object.entries(NEEDED)) {
        if (adapter.limits[name] >= least) limits[name] = least;
    }

    for (const name of WANTED) limits[name] = adapter.limits[name];
    return limits;
}

/// The device, and the clock its batches are timed on (gputime.js) or none. **Handed over and not made here**: the build
/// loads this file on its own to rewrite kernels (page_kernel.mjs), where it can import nothing.
export function useDevice(device, clock) {
    kernels.device = device;
    kernels.fits = Object.entries(NEEDED).every(([name, least]) => device.limits[name] >= least);
    kernels.clock = clock;
}

/// The device's time on the batches measured since the last time this was asked, and how many: [ms, count].
export function kernelTimes() {
    return kernels.clock?.drain() ?? [0, 0];
}

/// The run's whole memory, asked for afresh each time it is written into: a view kept is one the runtime's next growth
/// detaches (town.js, rule 3).
export function useHeap(view) {
    kernels.heap = view;
}

/// Whether this device can run the world's kernels at all; a page whose cannot steps the managed world.
function kernelsFit() {
    return kernels.fits;
}

/// How much memory the run has been given, which a page never hands back: the most it ever needed at once.
function heapBytes() {
    return kernels.heap().byteLength;
}

function view(memoryView) {
    return memoryView._unsafe_create_view ? memoryView._unsafe_create_view() : memoryView.slice();
}

function kernelBuffer(id, byteLength, host) {
    const size = Math.max(16, Math.ceil(byteLength / 4) * 4);
    const buffer = kernels.device.createBuffer({
        size,
        // VERTEX too: the engine layers' quads are written by a kernel and drawn where they are written (town.js `engine`).
        usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_SRC | GPUBufferUsage.COPY_DST | GPUBufferUsage.INDIRECT | GPUBufferUsage.VERTEX,
        label: `kernel buffer ${id}`,
    });
    // An address crosses as a 32-bit int, and past two gigabytes of heap that is a negative one.
    kernels.buffers.set(id, { buffer, size, byteLength, host: host >>> 0, staging: null });
}

function kernelBufferGone(id) {
    const held = kernels.buffers.get(id);
    if (!held) return;

    held.buffer.destroy();
    held.staging?.destroy();
    kernels.buffers.delete(id);
}

function kernelWrite(id, offset, data) {
    kernels.device.queue.writeBuffer(kernels.buffers.get(id).buffer, offset, view(data));
}

/// naga's WGSL made a page's: the push block a uniform, `@coherent` (which WGSL has not got) dropped, and the loads in
/// a workgroupUniform… function made workgroupUniformLoads. The web build validates kernels in this form
/// (src/runtime/web/page_kernel.mjs).
export function kernelSource(code) {
    code = code.replace(/var<immediate>/g, '@group(1) @binding(0) var<uniform>').replace(/@coherent /g, '');
    const workgroup = new Set([...code.matchAll(/var<workgroup>\s+(\w+)\s*:/g)].map(m => m[1]));
    return code.replace(/(fn workgroupUniform\w*\([^)]*\)[^{]*\{)([\s\S]*?\n\})/g, (all, head, body) =>
        head + body.replace(/= (\w+)((?:\[[^\]]*\])?);/g, (load, name, index) =>
            workgroup.has(name) ? `= workgroupUniformLoad(&${name}${index});` : load));
}

/// The storage buffers a kernel declares, read off its WGSL: naga writes one only where the GLSL used it.
function storageBindings(code) {
    return [...code.matchAll(/@group\(0\) @binding\((\d+)\)\s*var<storage(?:,\s*(read_write|read))?>/g)]
        .map(m => ({ binding: Number(m[1]), writable: m[2] === 'read_write' }));
}

/// A kernel's pipeline as both its maker and its proof ask for it, and its bindings.
function kernelDescriptor(name, code, push) {
    const device = kernels.device;
    const bindings = storageBindings(code);
    const storage = device.createBindGroupLayout({
        label: name,
        entries: bindings.map(b => ({
            binding: b.binding,
            visibility: GPUShaderStage.COMPUTE,
            buffer: { type: b.writable ? 'storage' : 'read-only-storage' },
        })),
    });

    const layouts = [storage];
    if (push) layouts.push(pushLayout());
    const descriptor = {
        label: name,
        layout: device.createPipelineLayout({ bindGroupLayouts: layouts }),
        compute: { module: device.createShaderModule({ code, label: name }), entryPoint: 'main' },
    };

    return { descriptor, storage, bindings };
}

function kernelPipeline(id, name, wgsl, pushBytes) {
    const { descriptor, storage, bindings } = kernelDescriptor(name, kernelSource(wgsl), pushBytes > 0);
    descriptor.compute.module.getCompilationInfo().then(info => {
        for (const message of info.messages) {
            if (message.type === 'error') console.error(`${name} ${message.lineNum}:${message.linePos}: ${message.message}`);
        }
    });

    const pipeline = kernels.device.createComputePipeline(descriptor);
    kernels.pipelines.set(id, {
        pipeline, storage, name,
        bindings: bindings.map(b => b.binding),
        writable: new Set(bindings.filter(b => b.writable).map(b => b.binding)),
    });
}

/// One kernel built on this device ahead of any town, the answer kept for `kernelsProven`.
///
/// **A driver may refuse a kernel the limits allow**: a handset's Vulkan answered `tyre_pass.comp` with
/// VK_ERROR_UNKNOWN, and a world stepped through a pipeline that failed is every batch refused — bodies that never move,
/// read back off views nobody wrote. **A kernel binding more storage buffers than the device gives is not proved**: it is
/// one the page never builds (WEB-10), and every kernel is written to fit (WEB-12).
function proveKernel(name, wgsl) {
    const code = kernelSource(wgsl);
    if (storageBindings(code).length > kernels.device.limits.maxStorageBuffersPerShaderStage) return;

    const { descriptor } = kernelDescriptor(name, code, code.includes('@group(1)'));    kernels.proofs.push(kernels.device.createComputePipelineAsync(descriptor).then(() => null, e => `${name}: ${e.message}`));
}

/// The kernels `proveKernel` was given, built or refused: the refusals newline apart, or "". **Any refusal and the device
/// no longer fits**, so every town after steps the managed world (WEB-10).
async function kernelsProven() {
    const refused = (await Promise.all(kernels.proofs)).filter(line => line !== null);
    kernels.proofs = [];
    if (refused.length > 0) kernels.fits = false;
    for (const line of refused) console.warn(`This device cannot build a world kernel — ${line}`);
    return refused.join('\n');
}

function pushLayout() {
    return kernels.pushLayout ??= kernels.device.createBindGroupLayout({
        label: 'push constants',
        entries: [{ binding: 0, visibility: GPUShaderStage.COMPUTE, buffer: { type: 'uniform', hasDynamicOffset: true } }],
    });
}

/// A dispatch's three words of indirect arguments.
const INDIRECT_BYTES = 12;

/// Where indirect arguments a dispatch also writes are copied to (kernelSubmit), grown to hold a batch's. A buffer grown
/// in the middle of a batch is a fresh one, and the copies already recorded keep the one they named.
function scratchRoom(bytes) {
    if (kernels.scratch && kernels.scratch.size >= bytes) return kernels.scratch;

    kernels.scratch = kernels.device.createBuffer({
        size: Math.max(256, bytes * 2), usage: GPUBufferUsage.INDIRECT | GPUBufferUsage.COPY_DST, label: 'indirect arguments',
    });
    return kernels.scratch;
}

/// The uniform buffer the batch's push constants are written into, grown to hold them.
function pushRoom(bytes) {
    if (kernels.pushBuffer && kernels.pushBuffer.size >= bytes) return;

    const size = Math.max(bytes, (kernels.pushBuffer?.size ?? PUSH_STRIDE * 64) * 2);
    kernels.pushBuffer?.destroy();
    kernels.pushBuffer = kernels.device.createBuffer({
        size, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST, label: 'push constants',
    });
    kernels.pushGroup = kernels.device.createBindGroup({
        layout: pushLayout(),
        entries: [{ binding: 0, resource: { buffer: kernels.pushBuffer, size: PUSH_STRIDE } }],
    });
}

function kernelBind(id, kernel, ids) {
    const held = kernels.pipelines.get(kernel);
    const buffers = view(ids);
    const group = kernels.device.createBindGroup({
        label: `${held.name} ${id}`,
        layout: held.storage,
        entries: held.bindings.map(binding => ({
            binding, resource: { buffer: kernels.buffers.get(buffers[binding]).buffer },
        })),
    });

    const written = new Set(held.bindings.filter(binding => held.writable.has(binding)).map(binding => buffers[binding]));
    kernels.groups.set(id, { group, written });
}

/// A batch as ComputeBatch.Web.cs wrote it: what the host wrote sent, the operations recorded in order, what the host
/// reads copied out after them, and the serial written into the run's memory once all of it has landed there.
function kernelSubmit(operations, push, serial, landedAddress) {
    const device = kernels.device;
    const queue = device.queue;
    const ops = view(operations);
    const constants = view(push);
    if (constants.byteLength > 0) {
        pushRoom(constants.byteLength);
        queue.writeBuffer(kernels.pushBuffer, 0, constants);
    }

    const heap = kernels.heap();
    for (let at = 0; at < ops.length; at += STRIDE) {
        if (ops[at] !== SEND) continue;

        const held = kernels.buffers.get(ops[at + 1]);
        queue.writeBuffer(held.buffer, 0, heap, held.host, held.byteLength & ~3);
    }

    const encoder = device.createCommandEncoder();
    const reads = [];
    // What the batch writes: a buffer the host reads is copied back only where it did, since one it only read holds what
    // the batch that last wrote it landed — and its staging may still be mapped for that one.
    const written = new Set();
    let pass = null;
    let scratchUsed = 0;

    // The batch bracketed by two empty passes, so the copies and fills between its own passes are timed with them.
    const clock = kernels.clock;
    const timed = clock?.take() ?? null;
    if (timed) encoder.beginComputePass({ timestampWrites: clock.writes(timed, true, false) }).end();
    for (let at = 0; at < ops.length; at += STRIDE) {
        const code = ops[at];
        if (code === DISPATCH || code === INDIRECT) {
            const bound = kernels.groups.get(ops[at + 2]);
            for (const id of bound.written) written.add(id);

            // A buffer a dispatch writes cannot be its indirect arguments too (Vulkan reads them before the kernel
            // runs; WebGPU refuses the pair), so the three words are copied out first and read from the copy.
            let indirect = null;
            let indirectAt = 0;
            if (code === INDIRECT) {
                indirect = kernels.buffers.get(ops[at + 3]).buffer;
                indirectAt = ops[at + 4];
                if (bound.written.has(ops[at + 3])) {
                    pass?.end();
                    pass = null;
                    const scratch = scratchRoom(scratchUsed + INDIRECT_BYTES);
                    encoder.copyBufferToBuffer(indirect, indirectAt, scratch, scratchUsed, INDIRECT_BYTES);
                    indirect = scratch;
                    indirectAt = scratchUsed;
                    scratchUsed += INDIRECT_BYTES;
                }
            }

            pass ??= encoder.beginComputePass();
            pass.setPipeline(kernels.pipelines.get(ops[at + 1]).pipeline);
            pass.setBindGroup(0, bound.group);
            const pushAt = code === DISPATCH ? ops[at + 4] : ops[at + 5];
            if (pushAt >= 0) pass.setBindGroup(1, kernels.pushGroup, [pushAt]);
            if (code === DISPATCH) pass.dispatchWorkgroups(ops[at + 3]);
            else pass.dispatchWorkgroupsIndirect(indirect, indirectAt);
            continue;
        }

        if (code === COPY || code === FILL) {
            pass?.end();
            pass = null;
        }

        if (code === COPY) {
            written.add(ops[at + 2]);
            encoder.copyBufferToBuffer(
                kernels.buffers.get(ops[at + 1]).buffer, ops[at + 3], kernels.buffers.get(ops[at + 2]).buffer, ops[at + 4],
                ops[at + 5]);
        } else if (code === FILL) {
            written.add(ops[at + 1]);
            fill(encoder, kernels.buffers.get(ops[at + 1]).buffer, ops[at + 2], ops[at + 3], ops[at + 4] >>> 0);
        } else if (code === READ && written.has(ops[at + 1])) {
            reads.push(kernels.buffers.get(ops[at + 1]));
        }
    }

    pass?.end();
    if (timed) {
        encoder.beginComputePass({ timestampWrites: clock.writes(timed, false, true) }).end();
        clock.resolve(encoder, timed);
    }

    for (const held of reads) {
        held.staging ??= device.createBuffer({
            size: held.size, usage: GPUBufferUsage.MAP_READ | GPUBufferUsage.COPY_DST, label: 'read back',
        });
        encoder.copyBufferToBuffer(held.buffer, 0, held.staging, 0, held.size);
    }

    queue.submit([encoder.finish()]);
    if (timed) clock.land(timed);
    land(reads, serial, landedAddress);
}

/// What the batch copied out, written where the host reads it once the device is done, and then the serial that says so.
/// A buffer let go of while its copy was out is skipped rather than holding the batch up for ever.
function land(reads, serial, landedAddress) {
    const landing = reads.map(held => held.staging.mapAsync(GPUMapMode.READ).then(() => {
        kernels.heap().set(new Uint8Array(held.staging.getMappedRange(), 0, held.byteLength), held.host);
        held.staging.unmap();
    }).catch(() => {}));

    Promise.all(landing).then(() => {
        new DataView(kernels.heap().buffer).setUint32(landedAddress >>> 0, serial >>> 0, true);
        kernels.landed?.();
    });
}

const FILL_WGSL = `
struct Fill { first: u32, count: u32, word: u32, pad: u32 }
@group(0) @binding(0) var<storage, read_write> words: array<u32>;
@group(0) @binding(1) var<uniform> fill: Fill;
@compute @workgroup_size(64) fn main(@builtin(global_invocation_id) at: vec3<u32>) {
    let word = at.y * 4194304u + at.x;
    if (word < fill.count) { words[fill.first + word] = fill.word; }
}`;

/// A run of a buffer filled with one word: cleared where the word is nought, and written by a kernel where it is not.
function fill(encoder, buffer, offset, bytes, word) {
    if (word === 0) {
        encoder.clearBuffer(buffer, offset, bytes);
        return;
    }

    const device = kernels.device;
    kernels.fill ??= device.createComputePipeline({
        label: 'fill', layout: 'auto', compute: { module: device.createShaderModule({ code: FILL_WGSL }), entryPoint: 'main' },
    });

    const count = bytes / 4;
    const figures = device.createBuffer({ size: 16, usage: GPUBufferUsage.UNIFORM, mappedAtCreation: true });
    new Uint32Array(figures.getMappedRange()).set([offset / 4, count, word, 0]);
    figures.unmap();

    const pass = encoder.beginComputePass();
    pass.setPipeline(kernels.fill);
    pass.setBindGroup(0, device.createBindGroup({
        layout: kernels.fill.getBindGroupLayout(0),
        entries: [{ binding: 0, resource: { buffer } }, { binding: 1, resource: { buffer: figures } }],
    }));
    const groups = Math.ceil(count / 64);
    pass.dispatchWorkgroups(Math.min(groups, 65536), Math.ceil(groups / 65536));
    pass.end();
}

/// A kernel buffer as the device holds it, for a draw that reads what a kernel wrote (town.js `engine`).
export function kernelBufferOf(id) {
    return kernels.buffers.get(id)?.buffer ?? null;
}

export const compute = {
    kernelsFit, heapBytes, kernelBuffer, kernelBufferGone, kernelWrite, kernelPipeline, proveKernel, kernelsProven, kernelBind,
    kernelSubmit,
};
