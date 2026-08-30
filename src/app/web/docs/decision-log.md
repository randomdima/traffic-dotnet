# The town in a browser — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md).

## 2026-08-30 — WebGPU, and not WebGL2

WebGL2 was the compatibility floor and it was refused, because it cannot hold the shape this engine is
built around. It has no indirect draw and no way to record a pass once: a frame would have to be
re-issued call by call, with the counts as arguments, and "the recording is written once and a frame
changes a number" would have become a sentence that was true on one machine and not the other.

WebGPU keeps both. `drawIndirect` is there, and it wants `firstInstance` to be zero — which is the
feature this project already declined to ask Vulkan for, so the four draws ported without a change. A
render bundle is a command buffer recorded once, replayed with `executeBundles`. The desktop's shape
survived intact, which is the whole reason it was worth the narrower support.

**What did not survive** is mapped memory. There is no buffer the CPU writes into while the GPU holds
it, so a frame copies — one `writeBuffer` a stream, out of the same managed arrays the simulation
filled. It is the one place the two machines genuinely differ in kind rather than in spelling.

## 2026-08-30 — the module is a renderer, not a binding

The obvious shape for `town.js` was a thin binding: `createBuffer`, `setPipeline`, `draw`, one JavaScript
function per WebGPU call, with the frame written in C# exactly as the Vulkan one is. It would have read
the same on both sides.

It was refused because **the wall is not free and a binding puts a frame's worth of calls through it**.
Fifteen crossings a frame is still O(1) in the size of the town, so it would not have broken rule 1 on
paper — but it would have made the browser head's frame cost a function of how the renderer was written
rather than of what the town holds, and the whole point of counting crossings is that the number is
small and stated. So `town.js` knows what a town is made of: three pipelines, four draws, one bundle.
`frame` takes three counts and the memory behind them, and everything else happens on the far side.

## 2026-08-30 — every view onto the heap is an argument

The first cut handed JavaScript a `Uint8Array` over each instance buffer once, at startup, and kept it
for the run: no marshalling at all, and a frame that passed three integers.

It is wrong, and quietly. **A view onto the WebAssembly heap is detached the moment the runtime grows
its memory**, and the runtime grows its memory when the town does — so the page would have run
perfectly until whichever frame followed an allocation that crossed a page boundary, and then thrown
from inside `writeBuffer`. The views are arguments now, made fresh by the interop layer on each call
and dead when it returns. It costs nothing on this side: the managed array is not copied either way.

## 2026-08-30 — a picture of the page needs a window, and this is why

`qq web --shot` opens a real browser window, which sits badly beside a project whose whole visual
tier is taken with no window, no compositor and no desktop. It is not laziness, and the finding is
worth keeping because it costs an afternoon to rediscover.

**Headless Chromium runs all of this correctly except the last step.** It has WebGPU, it hands out an
adapter, the WGSL compiles, the atlas uploads, the town stands up, the bundle records and the draws
submit — every one of those was watched happening. What it cannot do is present to a WebGPU canvas:
the first frame that reaches `getCurrentTexture` loses the device, with no validation error and no
exception, and the page goes white. Rendering the same frame into an offscreen texture instead, in
the same headless browser, works.

So the shot is taken through the DevTools protocol against a window that is opened, photographed and
closed. The tool says so, and so does this, because "why not headless" is the first question anybody
will have.

## 2026-08-30 — the files go into the file system, not through a provider

Everything above the machine reads `assets/` and `towns/` by walking up from where the binary landed.
The tidy-looking answer for a page was an asset provider — an interface with two implementations,
threaded through the catalogues, the variant files, the town reader and the sheet decode.

That is fifteen call sites changed on both heads to serve one, and a second way of saying where a file
is. The runtime has a file system; the page writes into it. `ProjectPaths` then finds the root exactly
as it does beside a binary, and **not one reader above knows which machine it is on**.

The price is that the page downloads every map rather than the one it opens, because a town is opened
from inside the loop and a loop cannot await. **Fetching a town when it is picked is the improvement to
make** and it wants `Game.Start` reachable from an `await`, which is a change to the menu rather than
to this.
