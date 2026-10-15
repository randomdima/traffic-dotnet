// No stackalloc in the engine is zeroed: every buffer is written before it is read, and one that counts or is read
// whole clears itself first. Zeroing was a store the width of every buffer on every call, the tick's per-agent ones
// among them. A new buffer read before it is written is garbage, not nought — write it, or Clear it.
[module: System.Runtime.CompilerServices.SkipLocalsInit]
