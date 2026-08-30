# The town in a browser — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md).

## 2026-08-30 — four questions before the runtime, and a card while it comes

Everything the page could refuse, it refused *after* downloading four megabytes of engine: a browser
without WebGPU spent the whole wait to be told the wait had been pointless. All four questions —
WebAssembly, WebAssembly SIMD, the WebGPU API, an adapter it will actually hand out — are answerable in
milliseconds against nothing, so they are asked first and the runtime import became dynamic to let
them be. A static import is fetched before the first line of the file runs, which is the whole reason
that import reads the way it does.

**The refusal inside the run stays where it is.** `WebGpu.Start` still fails on a device that was there
a moment ago, and that is not the same question as whether this browser has the API — one is about the
browser, the other about what it gave out. Two checks, not one check in two places.

**And the bar sweeps when it cannot count.** The runtime is fetched by the loader itself, so the page
does not know how much of it there is; a bar sitting at nought for it reads as a page that has stopped.
Bytes are counted off a `fetch` wrapper for that stage alone and the bar sweeps, and the batches — which
do know how many files they asked for — fill it.

## 2026-08-30 — the art is one archive, and the runtime is not discovered

The menu came up on six files and the town still cost three hundred and thirteen fetches, thirty-two at
a time — ten waves of latency for four megabytes that download in two seconds. So the build packs
`assets/` and the page unpacks it: **a plain tar**, because the format is somebody else's and thirty
lines read it, gzipped because a fifth of the archive is catalogues and the WebP is incompressible. The
browser undoes the gzip with `DecompressionStream`, which is the one decompressor a page has that its
.NET runtime does not — the same fact that keeps the towns on gzip rather than brotli. Above the fetch
nothing changed: an unpacked file is held exactly as a warmed one is, and `grab` reads it out.

Two smaller things came with it.

**The menu's six files became one.** Five of them were the ground surfaces, and the menu draws no
ground — so the renderer it draws through takes 1×1 stand-ins for those bindings, exactly as it already
did for the tile sampler. Only the figures are left. On the desktop the pictures are on the disk and
this is worth nothing; in a page every one of them was a round trip.

**And the chain to the runtime is preloaded.** The browser learned of `town.js` by parsing `main.js`
and of the runtime by running it — four round trips before the engine was asked for. `modulepreload`
makes them one wave. The nine megabytes behind `dotnet.js` are deliberately *not* preloaded: a browser
that cannot run this page should not spend them to be told so.

`WasmStripILAfterAOT` is on as well. Ahead-of-time compiled, the bytecode beside the native code is a
second copy of the program nothing reads, and dropping it takes about a third off the smaller
assemblies. It was measured rather than assumed: the town stands and draws with it on.

## 2026-08-30 — the menu stands on what it draws, and a batch is asked for at once

Deployed to a static host, the page took about a minute to put a menu up. Neither the size of the
runtime nor the size of the art accounted for it: the framework is 13 MB raw and the host gzips it to
3.1, and the art is 4.2 MB. **What accounted for it was three hundred and nineteen round trips, taken
one after the next.** At 185 ms each that is a minute of waiting on a connection that was idle for
almost all of it.

Two things were wrong and they are separate.

**The batch.** `Data` fetched a file, awaited it, wrote it, fetched the next. `WebGpu.Warm` now hands
the whole list over in one call and the page keeps thirty-two in flight, counting them off in the
banner from the far side — where the counting has to be done, because the caller is awaiting the one
call and cannot say anything while it does. Above it nothing changed: `grab` reads a warmed file where
it would have fetched one, so every reader of the file system, and the whole decode split, is untouched.

**The menu was waiting for the town.** `Game`'s constructor read the catalogues, and the renderer it
built for the menu packed every sheet in the town into an atlas — so a page could not draw a list of
map names until it had fetched, decoded and packed all of the art it was not going to draw. The
catalogues are read at the first `Open` now, and the menu's renderer is laid for no sheets at all,
which the atlas, the tile binding and the shader table already allowed for. So the boot fetches what
the menu draws and the rest arrives behind the click that asked for a town.

**This is a saving on the desktop too**, where it was invisible: the atlas was packed twice, once for
a menu that drew none of it and once for the town.

`_looks` became nullable, which is the one cost. It is set with `_world` in `Open` and the frame
reaches it behind the same guard, so the two invariants are one invariant and the pattern in `Draw`
binds both.

## 2026-08-30 — a map picked is a name written down, not a town opened

The page fetched all nine maps at boot: 3.4 MB of the 10 it downloaded, to open one of them. The
obstacle was never the fetching — it was that `Open` is reached from `ReadInput`, inside `Game.Step`,
which in a browser *is* the animation callback. A frame cannot await, so the plan had to be on disk
before any frame ran, so every plan had to be.

`PickMap` is the seam, and it is a partial of the same kind as `Boot` and `NewRenderer`. The desktop's
half opens the map where it stands, because its plan is on the disk the run started from. The browser's
half writes the name down and returns, and **the boot's own wait loop is what drains it** — the loop
that was already there keeping `Main` alive is the one place in a browser run where waiting is allowed,
so it fetches the plan and calls `Game.Start`. Nothing above knows: `Open` is unchanged, and by the time
it runs the file is where `ProjectPaths` looks for it.

**A map's name and a map's bytes came apart, and the menu wanted the name.** `ProjectPaths.ShippedMaps`
reads the `towns/` folder, so a map nothing had fetched would not be on the menu to pick. `Data` lays an
empty file per map at boot for that reason — the listing is the name, and the bytes land under it when
something asks. It is the only place in this engine where a file on disk is not yet what it claims to
be, and it is never read in that state: the fetch is what stands between the click and the open.

## 2026-08-30 — the towns stay gzipped, because a page cannot unpack brotli

Brotli is a quarter smaller than gzip over these nine plans — 2.5 MB against 3.4, and 1.07 against 1.34
on Odesa alone — and every other byte the page fetches is already brotli. It was tried and refused, and
the reason is not a trade: **`BrotliStream` does not work in a browser.** The runtime's wasm build
carries zlib and no brotli at all; the API is annotated unsupported on this platform (CA1416) and there
is not one brotli symbol in `dotnet.native.wasm`. `DecompressionStream` in the page has no brotli
either.

The only brotli a page can read is one the *server* marks `Content-Encoding: br` and the browser unwraps
before the fetch resolves — which is exactly what `_framework` relies on, and is a fact about the host
rather than about this build. Making a town depend on it would mean a page that half-loads on a host
nobody configured, to save 270 KB on the heaviest map now that only one map is fetched. Gzip stays.

## 2026-08-30 — the timezone database is not something this town reads

`InvariantTimezone` was left at its default, so the whole tz database was linked into the native blob —
290 `America/*` zones and the rest — for an engine whose only clocks are a tick count and a `Stopwatch`.
Switched off it is 244 KB of the blob and 91 KB brotli, for a question nothing here asks.
`InvariantGlobalization` was already on and is the same argument about ICU.

## 2026-08-30 — no image codec on this head, because the browser is one

ImageSharp was 205 KB brotli of IL and dragged `System.Text.Encoding.CodePages` behind it — 698 KB of
code-page tables for a TIFF decoder nothing calls — but on this head that understates it by a long way:
it was 4.56 MB of the 27 MB of object code the ahead-of-time compiler emitted, and it is generic over
its pixel type, so an unknown further share of the 10.9 MB of generic instantiations was its too. **An
assembly here is priced by what it makes that compiler emit, not by what it weighs on the wire.**

Narrowing it was tried first and does nothing. Handing every call a `Configuration` holding the PNG and
WebP modules alone should let the trimmer drop the other seven codecs; **it was written, measured and
reverted.** `DecoderOptions` initialises its `Configuration` from `Configuration.Default`, and every
`Load` and `Identify` overload constructs one, so the factory that news up all nine modules is rooted
whatever you pass. `TiffDecoder` and `CodePages` shipped byte for byte.

So the cut was the whole library, and it went in three pieces:

- **A size comes off the header.** `ImageHeader` reads PNG's IHDR and WebP's three chunk shapes, which
  is forty lines and no dependency at all. It is shared, so the desktop's car catalogue stopped calling
  `Image.Identify` too, and it is checked against ImageSharp over every picture the town ships — the
  same independent-implementation arrangement the physics has against Box2D.
- **`Rgba32` became `Texel`**, this project's own four bytes, because the packer and the mip chain are
  shared with a desktop that keeps the library for writing shots. Both are RGBA in that order, so the
  desktop meets it as a cast at the one point a decode hands pixels over.
- **The decode became the page's own**, and that is the part with a real difficulty in it.

**The difficulty is that only half a browser decode can wait.** `createImageBitmap` is a promise, and
the atlas is packed inside `Game.Start`, which is reached from a frame — and a frame cannot await. But
`drawImage` and `getImageData` are *synchronous*. So the two halves go in different places: `Data` makes
every bitmap at boot, where waiting is already allowed and where the bytes are already parked from the
fetch, and `Texels.Web` reads one sheet's texels out where the packer stands. **Nothing above either of
them changed** — `SheetAtlas`, the gutter, the mip chain and the page-at-a-time discipline are the
desktop's own code, and the browser never learned what an atlas is.

Two things fell out of doing it that way. `parked` on the JavaScript side became a slot that either a
fetch or the run can fill and either a copy or a decode can read, so the art is fetched once and decoded
where it lies — no bytes move twice. And the bitmaps are kept for the run rather than dropped once the
atlas is built, because the art is every town's: picking a second map packs the atlas again, and a
bitmap closed after the first one would be a decode that had to happen inside a frame.

**The options on `createImageBitmap` are load-bearing.** Left to itself the browser premultiplies alpha
and may put the display's colour profile through the texels; this town's art is alpha-cut sprites drawn
at their own texel grid, and either one is a sheet that no longer matches what the desktop draws.

## 2026-08-30 — the publish is the deployment, so it holds real files

`wwwroot/assets` was a symlink into the working copy, made because the art was thirty megabytes of PNG
and a build that copied it was a build nobody ran twice. That reason went when the art became 2.8 MB of
WebP, and the arrangement was always wrong for the thing it is now aimed at: a stateless static host is
handed the published folder and nothing else, and a link into somebody's home directory is a page that
serves on one machine.

Everything is copied now, by `dotnet build` and `dotnet publish` alike, so what is served in development
is what is deployed. The guard that unlinks a stale `wwwroot/assets` or `wwwroot/towns` before copying
is not tidiness: a tree carrying one of those from an earlier build would have taken the copy *through*
the link and written the whole publish into `assets/` and `towns/` themselves.

**Brotli is the one thing the build cannot finish.** The publish writes a `.br` beside each framework
file — 3.6 MB against 16.3 raw — but only the host can serve them, because the loader fetches the
framework itself and a browser runtime has no brotli to unpack one with. A host that does not negotiate
encodings serves the raw copies, and WEB-6's figure is a claim about a host that does.

## 2026-08-30 — the page's own fetch, not an HttpClient

`Data` fetched the manifest and its 328 files through an `HttpClient`, which is the familiar API and on
this machine is a shim over the very `fetch` the page already has — reached through the very interop
`WebGpu` already owns. So the build carried a whole HTTP stack, and its handler pipeline, header
collections and URI parser, to make 328 GETs of static files sitting beside the page.

It is two imports on the wall instead: `grab` fetches one file and answers its length, `take` copies the
bytes into an array made at that length. **Two calls and not one, and the reason is the wall's own third
rule** — a `MemoryView` is a window onto the WebAssembly heap handed *out*, and there is no shape that
hands one back, so the length has to come first. `WebGpu.Origin` went with it: `fetch` resolves a
relative path against the document, which is what a path in the manifest was always written against, so
the crossing that asked the page where it was is gone rather than replaced.

**It was worth more than the three assemblies it names.** `System.Net.Http`, `System.Private.Uri` and
`System.Net.Primitives` are 58 KB brotli between them, but what they drag through the ahead-of-time
compiler is a megabyte of object code and a wider slice of `CoreLib` — the published runtime came down
327 KB brotli, of which 240 is the native blob and 25 is `CoreLib` alone. **The lesson is that on this
head an assembly is priced by what it makes the AOT compiler emit, not by what it weighs on the wire.**

## 2026-08-30 — whether the run is over is not something the page saw

Exit did nothing. The input the town reads is two arrays the page owns and a frame copies across, and
whether the window was closing was an eleventh axis in one of them — so the menu set it, and the pump
at the top of the very next frame copied the page's own copy straight over the top of it. **The page
has no opinion about whether the run is over**, so the slot it kept for one was always a zero, and the
way out of the game was a flag that lived for eight milliseconds.

It is a field now, set once and by nothing else, and the axis is gone from both sides. **The shape is
the lesson rather than the bug**: an axis is by definition what the page saw, so anything the run
decides for itself cannot be one of them. There is no third thing in those arrays to check — the keys
and the pointer are the page's all the way down.

It also made a liar of the last diagnosis. Exit was read as working but invisible, and a banner was
added on that basis; the banner is worth having and stays, but what was actually wrong was this, and
the picture that proved it was the one that came back with the tab still open and nothing said.

## 2026-08-30 — the frame is timed around the wait, not through it

The read-out on the page quoted three hundred frames a second on a display doing a hundred and twenty,
because the frame was timed from the top of `Game.Step` to the bottom of it. On the desktop that is the
whole frame — the wait for the display happens inside the submit, where the renderer times it — and in
a page it is only the work: a browser paces by choosing when to ask for the next frame, so the wait
falls *between* two of them and was being timed by nobody. The same span is the step the hands are read
over, so the camera panned at a fifth of its speed as well.

**The obvious home for it was the web renderer**, beside the desktop's fence. It was refused: the
renderer would have had to report a wait it did not do and that ended before it was called, and the
frame subtracts a renderer's blocked time out of the submit it happened inside. So `Game.Step` times
what a frame waited before it began, which is nothing on a machine with a loop and the whole of the
pacing in a page, and both machines say `blocked` and mean it.

**A hidden tab is what makes that a rule rather than a subtraction.** The animation callback stops
when nobody is looking, and the frame that resumes has waited forty-five seconds — which went straight
into the read-out's window and published nought frames a second. The line between a wait and a stall is
not a new number: `SimClock` already caps how far behind it will chase and drops the time it could not
simulate, and past that bound the clock has stopped calling the gap time the town lived through. The
read-out stops there too, and the stall gives `SimClock.Resynchronise` the caller it never had.

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

The price was that the page downloaded every map rather than the one it opened, because a town is
opened from inside the loop and a loop cannot await. That is paid off below.
