// What the device itself spends, measured on it: WebGPU's timestamps written around a pass or a batch, resolved and
// read back when the device is done, and summed until the run takes them with the frame's input (town.js `pump`).
//
// **Nothing waits for one.** A measurement takes a slot and gives it back once its readback lands; with every slot
// still out, a frame or a batch goes unmeasured rather than held. A browser rounds a timestamp to a tenth of a
// millisecond unless its developer features are on, which a sum over a window of them reads through.

const SLOTS = 4;
const SLOT_BYTES = 16;

/// How far apart two slots are resolved: WebGPU resolves a query set only to an offset that is a multiple of it.
const RESOLVE_STRIDE = 256;

export class GpuClock {
    constructor(device, label) {
        this.querySet = device.createQuerySet({ type: 'timestamp', count: 2 * SLOTS, label });
        this.resolved = device.createBuffer({
            size: RESOLVE_STRIDE * SLOTS, usage: GPUBufferUsage.QUERY_RESOLVE | GPUBufferUsage.COPY_SRC, label,
        });
        this.free = [];
        for (let slot = 0; slot < SLOTS; slot++) {
            this.free.push({
                slot,
                readback: device.createBuffer({
                    size: SLOT_BYTES, usage: GPUBufferUsage.MAP_READ | GPUBufferUsage.COPY_DST, label,
                }),
            });
        }

        this.sumMs = 0;
        this.count = 0;
    }

    /// A slot for one measurement, or null where every one is still out.
    take() {
        return this.free.pop() ?? null;
    }

    /// The pass descriptor's `timestampWrites`, at the start of one pass and the end of another — or of the same one.
    writes(slot, begin, end) {
        const writes = { querySet: this.querySet };
        if (begin) writes.beginningOfPassWriteIndex = 2 * slot.slot;
        if (end) writes.endOfPassWriteIndex = 2 * slot.slot + 1;
        return writes;
    }

    /// The slot's two stamps resolved and copied out, recorded after the last pass they bracket.
    resolve(encoder, slot) {
        encoder.resolveQuerySet(this.querySet, 2 * slot.slot, 2, this.resolved, RESOLVE_STRIDE * slot.slot);
        encoder.copyBufferToBuffer(this.resolved, RESOLVE_STRIDE * slot.slot, slot.readback, 0, SLOT_BYTES);
    }

    /// Read once the submit that resolved it is done, into the sum, and the slot given back.
    land(slot) {
        slot.readback.mapAsync(GPUMapMode.READ).then(() => {
            const [from, to] = new BigUint64Array(slot.readback.getMappedRange());
            if (to > from) {
                this.sumMs += Number(to - from) / 1e6;
                this.count++;
            }

            slot.readback.unmap();
            this.free.push(slot);
        }, () => this.free.push(slot));
    }

    /// What has landed since the last time this was asked, and none of it again.
    drain() {
        const taken = [this.sumMs, this.count];
        this.sumMs = 0;
        this.count = 0;
        return taken;
    }
}
