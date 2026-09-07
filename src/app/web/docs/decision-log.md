# The town in a browser — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md).

## 2026-08-30 — the callback is handed over before any town is opened

A desktop run has its plan and art on disk, so opening before the first frame costs nothing; a page has
neither, and would show a blank canvas through a plan and three megabytes of archive. `WebGpu.Ticker` is
handed `Game.Step` as soon as the engine runs and the town is opened after it from the boot's own `await`.
The only thing given up is that `?map=Odesa` has a menu in the first frame and its town a moment later.

## 2026-08-30 — nothing waits for what it does not need yet

Everything was fetched in the order it happened to be read, one thing at a time: measured with no latency,
the art was not asked for until 237 ms and the plan not until 544. The art and the engine are the pairing
that matters — about three megabytes each, neither needing the other — so `main.js` starts the archive
before the runtime asks for it. It is conditional on a map having been named, because a menu waits for
nothing, not even a fetch nobody is awaiting. The decode was the other half: one awaited
`createImageBitmap` per file cost 216 ms for 174 sheets against 57 asked for together. The other eight
plans come down after the callback is handed over, which is 3.4 MB behind a picture that is already
drawing rather than in front of a menu. `EventSourceSupport`, `MetadataUpdaterSupport` and
`DebuggerSupport` were switched off, moved nothing, and are not in the project file — a knob that buys
nothing is a knob somebody has to read.

## 2026-08-30 — four questions before the runtime, and a card while it comes

A browser without WebGPU spent the whole four-megabyte wait to be told the wait was pointless. All four
questions are answerable in milliseconds, so the runtime import became dynamic to let them be asked first —
a static import is fetched before the first line of the file runs. The refusal inside the run stays: what
this browser has and what it gave out are two questions. The bar sweeps for the runtime stage, whose size
the page cannot know, and the batches fill it.

## 2026-08-30 — the art is one archive, and the runtime is not discovered

The town cost 313 fetches, thirty-two at a time — ten waves of latency for four megabytes. The build packs
`assets/` as a plain tar, gzipped because a fifth of it is catalogues and the WebP is incompressible; the
browser undoes it with `DecompressionStream`, the one decompressor a page has that its .NET runtime does
not. The menu's six files became one, five having been ground surfaces a menu does not draw. The chain to
the runtime is `modulepreload`ed into one wave, but the nine megabytes behind `dotnet.js` deliberately are
not: a browser that cannot run this page should not spend them to be told so.

## 2026-08-30 — the menu stands on what it draws

A static host took about a minute to put a menu up, and what accounted for it was 319 round trips at
185 ms. `Game`'s constructor read the catalogues and packed every sheet in the town into an atlas, so a
page could not draw a list of map names until it had fetched, decoded and packed art it was not going to
draw. The catalogues are read at the first `Open` and the menu's renderer is laid for no sheets at all.
It is a saving on the desktop too, where the atlas was being packed twice.

## 2026-08-30 — a map picked is a name written down, not a town opened

The page fetched all nine maps at boot to open one. The obstacle was never the fetching: `Open` is reached
from inside `Game.Step`, which in a browser *is* the animation callback, and a frame cannot await.
`PickMap` is the seam — the desktop's half opens the map where it stands, the browser's writes the name
down and lets the boot's own wait loop drain it. `Data` lays an empty file per map at boot, because
`ProjectPaths.ShippedMaps` reads the folder and the listing is the name; it is the one place here where a
file on disk is not yet what it claims, and it is never read in that state.

## 2026-08-30 — the towns stay gzipped, because a page cannot unpack brotli

Brotli is a quarter smaller over these plans and was refused, not as a trade: `BrotliStream` does not work
in a browser — the wasm build carries zlib and no brotli symbol at all — and `DecompressionStream` has
none either. The only brotli a page can read is one the *server* marks `Content-Encoding: br`, which is a
fact about the host. Depending on it would half-load on a host nobody configured, to save 270 KB.

## 2026-08-30 — the timezone database is not something this town reads

`InvariantTimezone` at its default linked the whole tz database into the native blob for an engine whose
only clocks are a tick count and a `Stopwatch`. Switched off it is 244 KB of blob and 91 KB brotli.

## 2026-08-30 — no image codec on this head, because the browser is one

ImageSharp was 205 KB brotli of IL and 4.56 MB of the 27 MB of object code the AOT compiler emitted, plus
an unknown share of generic instantiations. Narrowing it was written, measured and reverted:
`DecoderOptions` initialises its `Configuration` from `Configuration.Default`, so the factory that news up
all nine modules is rooted whatever you pass. The cut went in three pieces — `ImageHeader` reads PNG's
IHDR and WebP's chunks in forty lines, checked against ImageSharp over every shipped picture; `Rgba32`
became this project's own `Texel`; and the decode became the page's own. Only half a browser decode can
wait, so `Data` makes every bitmap at boot and `Texels.Web` reads a sheet's texels out synchronously where
the packer stands — nothing above either changed. Bitmaps are kept for the run, since picking a second map
packs the atlas again. The options on `createImageBitmap` are load-bearing: premultiplied alpha or a
colour profile is a sheet that no longer matches what the desktop draws. **On this head an assembly is
priced by what it makes the AOT compiler emit, not by what it weighs on the wire.**

## 2026-08-30 — the publish is the deployment, so it holds real files

`wwwroot/assets` was a symlink made when the art was thirty megabytes of PNG, and a link into somebody's
home directory is a page that serves on one machine. Everything is copied by build and publish alike. The
guard that unlinks a stale link first is not tidiness: the copy would otherwise go *through* it and write
the whole publish into `assets/`. Brotli is the one thing the build cannot finish — only the host can
serve the `.br` copies, so WEB-6's figure is a claim about a host that negotiates encodings.

## 2026-08-30 — the page's own fetch, not an HttpClient

An `HttpClient` on this machine is a shim over the very `fetch` the page has, reached through the interop
`WebGpu` already owns — so the build carried a whole HTTP stack to make 328 GETs of static files beside
the page. It is two imports on the wall: `grab` fetches and answers a length, `take` copies into an array
made at that length. Two calls and not one, because a `MemoryView` is a window handed *out* and nothing
hands one back. The three assemblies are 58 KB brotli but drag a megabyte of object code, and the
published runtime came down 327 KB brotli.

## 2026-08-30 — whether the run is over is not something the page saw

Exit was an eleventh axis in the input arrays the page owns, so the pump at the top of the next frame
copied the page's zero over it and the way out lived for eight milliseconds. It is a field now. The shape
is the lesson: an axis is by definition what the page saw, so anything the run decides for itself cannot
be one.

## 2026-08-30 — the frame is timed around the wait, not through it

Timing from the top of `Game.Step` to the bottom quoted 300 fps on a 120 Hz display, because a browser
paces by choosing when to ask for the next frame, so the wait falls *between* two of them. The same span
is the step the hands are read over, so the camera panned at a fifth speed. Putting it in the web renderer
was refused — it would have to report a wait it did not do and that ended before it was called — so
`Game.Step` times what a frame waited before it began. A hidden tab makes that a rule rather than a
subtraction: a resumed frame has waited forty-five seconds. The bound is `SimClock`'s own cap, and the
stall gives `SimClock.Resynchronise` the caller it never had.

## 2026-08-30 — WebGPU, and not WebGL2

WebGL2 cannot hold the shape this engine is built around: no indirect draw and no way to record a pass
once, so "the recording is written once and a frame changes a number" would have been true on one machine
and not the other. WebGPU has `drawIndirect` wanting `firstInstance` zero — the feature this project
already declined to ask Vulkan for — and render bundles, so the four draws ported unchanged. What did not
survive is mapped memory: a frame copies through `writeBuffer`, which is the one place the two machines
differ in kind.

## 2026-08-30 — the module is a renderer, not a binding

A thin binding would read the same on both sides and puts a frame's worth of calls through the wall.
Fifteen crossings a frame is still O(1), so it would not break rule 1 on paper — but it makes the frame
cost a function of how the renderer was written rather than of what the town holds. `town.js` knows what a
town is made of: three pipelines, four draws, one bundle.

## 2026-08-30 — every view onto the heap is an argument

Handing JavaScript a `Uint8Array` over each instance buffer once at startup is wrong quietly: a view onto
the WebAssembly heap is detached the moment the runtime grows its memory, which happens when the town
does. The views are made fresh per call and dead when it returns, and it costs nothing — the managed array
is not copied either way.

## 2026-08-30 — a picture of the page needs a window, and this is why

Headless Chromium runs all of this correctly except the last step: it has WebGPU, the WGSL compiles, the
atlas uploads and the draws submit — but the first frame that reaches `getCurrentTexture` loses the
device, with no validation error and no exception. Rendering into an offscreen texture in the same
headless browser works. The shot is taken through the DevTools protocol against a real window, and this is
written down because it costs an afternoon to rediscover.

## 2026-08-30 — the files go into the file system, not through a provider

An asset provider is fifteen call sites changed on both heads to serve one, and a second way of saying
where a file is. The runtime has a file system and the page writes into it, so `ProjectPaths` finds the
root exactly as it does beside a binary and not one reader above knows which machine it is on.
