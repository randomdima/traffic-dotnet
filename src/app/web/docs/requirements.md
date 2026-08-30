# The town in a browser — requirements

The page, the module that drives WebGPU, and the boot that puts the town's own files where the town
already looks for them. **What the picture must look like is
[app/render](../../render/docs/requirements.md)** and **what a frame costs is
[runtime](../../../runtime/docs/requirements.md)**; this is the head that has no window under it.

## What the browser head is

**WEB-1 — the same town, from the same code.** The browser build compiles `src/` exactly as the desktop
build does. What differs is the machine: a canvas in place of a window, WebGPU in place of Vulkan, and
an animation callback in place of a loop. **A file under `src/**/web/` is the browser's half of
something the desktop has too**, and there is no `#if` anywhere in the shared code — the two project
files pick which half is compiled and that is the whole mechanism.

The halves, and nothing else, are:

| The desktop's | The browser's |
|---|---|
| [`Vk`](../../../runtime/Vk.cs), [`Swapchain`](../../../runtime/Swapchain.cs), [`GpuBuffer`](../../../runtime/GpuBuffer.cs), [`GpuTexture`](../../../runtime/GpuTexture.cs) | [`WebGpu`](../../../runtime/web/WebGpu.cs) and [`town.js`](../wwwroot/town.js) |
| [`AppWindow`](../../../runtime/AppWindow.cs) | [`AppWindow.Web.cs`](../../../runtime/web/AppWindow.Web.cs) |
| [`TownRenderer`](../../render/TownRenderer.cs) | [`TownRenderer.Web.cs`](../../render/web/TownRenderer.Web.cs) |
| [`Texels`](../../render/Texels.cs) | [`Texels.Web.cs`](../../render/web/Texels.Web.cs) |
| [`Game.Desktop.cs`](../../main/Game.Desktop.cs), [`Program.cs`](../../main/Program.cs) | [`Game.Web.cs`](../../main/web/Game.Web.cs), [`Boot.cs`](../../main/web/Boot.cs) |

**The input is two arrays and they hold only what the page saw.** The keys and the pointer are copied
across whole at the top of every frame, so **anything the run decides for itself cannot live in them** —
a pump would put the page's own copy straight back over the top of it, and the page has no copy of a
thing it never saw. [`AppWindow.IsClosing`](../../../runtime/web/AppWindow.Web.cs) is the one such
thing there is, and it is a latch beside the arrays rather than a slot in them.

**WEB-2 — the crossing budget holds, in the browser's own terms.** A standing town crosses the wall
between managed code and the page **three times a frame** and never a fourth: the animation callback
coming in, the input going out, and the frame. Everything inside a frame — the render pass, the
bundle, the queue, the submit — is on the far side of one call, and none of the three takes the size of
the town as an argument. This is rule 1 of [goals.md](../../../../docs/goals.md), and it is what
[`WebGpu.Crossings`](../../../runtime/web/WebGpu.cs) counts.

**WEB-3 — the page carries the visual layers and none of the instruments.** The interface, the debug
layers, the scenario panel and the figures page are the town's own picture and are all here. **The
offscreen picture, the sheet, the probes and the workshop steps are not**: `--shot`, `--sheet`,
`--bench`, `--lamps` and `--place-services` are how a run is *measured*, they need a file system and a
process that can exit, and a page has neither. A browser is where the town is watched; the desktop is
where it is answered for.

**WEB-4 — everything a frame reads is there before that frame.** A frame cannot wait on a fetch, so
[`Data`](../../main/web/Data.cs) writes `assets/` and `towns/` into the runtime's own file system and
every reader above is untouched (<see cref="ProjectPaths"/> finds them exactly as it does beside a
binary). **There is no second asset story**: no provider threaded through fifteen call sites, and no
path that means one thing here and another there.

The art is every town's, so all of it is fetched at boot. **A plan is one town's, so it is fetched when
that town is picked**, and the fetch happens in the one place a browser run may wait — the boot's own
loop, which drains the name the menu wrote down (`Game.PickMap`). What `Data` lays for a map at boot is
its *name*: an empty file, because [`ProjectPaths.ShippedMaps`](../../../core/config/ProjectPaths.cs)
reads the folder and a map with no file would be a map the menu could not offer. It is never read in
that state — the fetch stands between the click and the open.

**WEB-6 — a page is the size of its town, and the town is the size of what it draws.** What a browser
fetches before the first frame is **under six megabytes** for the fixture map and never over eight for
the heaviest: the .NET runtime ahead-of-time compiled and served brotli, 2.8 MB of art, 40 KB of page,
and one map. **How a sheet is stored is
[app/render](../../render/docs/requirements.md#how-a-sheet-is-stored)'s rule**, not a thing done to
the browser build: both heads read the same sheets.

**The page carries no image codec, because the browser is one.** A decoder is the largest thing this
head can decline to ship — it is priced by what it makes the ahead-of-time compiler emit rather than by
what it weighs — so [`Texels.Web.cs`](../../render/web/Texels.Web.cs) is `createImageBitmap` and
[`ImageHeader`](../../../core/config/ImageHeader.cs) reads a size off the file's own header on both
machines. **The decode is split because only half of it can wait**: making a bitmap is a promise and the
atlas is packed from inside `Game.Start`, which a frame reaches; drawing one to a canvas and reading its
texels back is synchronous. So [`Data`](../../main/web/Data.cs) makes every bitmap at boot, where
waiting is allowed, and the packer reads one sheet out where it stands. **A sheet the boot did not
decode is a fault and not a fetch** — there is no way back to a promise from inside a frame.

**The desktop keeps its decoder** and is the second opinion the header reader is checked against over
every picture the town ships ([tests/config](../../../tests/config/ImageHeaderTests.cs)). It also still
*writes* pictures, which is a thing no page does.

What is this slice's is the one thing only a page pays for. **A town is fetched compressed, and only
the one being opened**: a `.town` is better than half zero bytes, because its lane index is laid out for
reading rather than for sending, so the nine of them are 23 MB raw against 3.4 gzipped — of which a run
fetches 39 KB for Test and 1.34 MB for Odesa. The build squeezes them and
[`Data`](../../main/web/Data.cs) inflates on the way into the file system. **Gzip and not brotli**: the
browser's runtime carries zlib and no brotli, so the only brotli a page can read is one the server
marks `Content-Encoding` — which is a fact about the host, and is what `_framework` already leans on.

**And the published folder holds one runtime.** Nothing sweeps up a hashed assembly when the next
publish replaces it, so `_framework` is cleared before a publish and only brotli is emitted — the two
together are the difference between 21 MB on disk and 93.

**WEB-7 — what `dotnet publish` writes is the whole of what gets deployed.** The target is a stateless
static host: the folder is handed over and nothing of ours runs beside it. So **every file in it is a
real file** — no symlink into a working copy, which is a page that only serves on the machine it was
built on — and everything the page will ask for is prepared by the build: the art copied, the towns
squeezed, the manifest written from the same item lists. `dotnet build` lays the identical tree beside
the binary, so what is served in development is what is deployed.

**Brotli is the host's half of this and cannot be the build's.** The publish writes a `.br` beside each
file of the framework, and a server that maps them serves 3.6 MB where the raw files are 16.3. Nothing
in the page can do it instead — the loader fetches the framework itself, and the runtime in a browser
has no brotli to unpack one with (which is the same fact that keeps the towns on gzip). **So a host that
does not negotiate encodings serves the raw copies**, and the figure in WEB-6 is a claim about a host
that does.

**WEB-5 — the query string is the command line.** `?map=Test&ui=nodes,paths` is `--map Test --ui
nodes,paths`. The words are the desktop's, and the ones a page cannot answer are not offered.

## How it is checked

**`qq web --shot FILE` is the browser head's `--shot`**: it publishes, serves, drives a browser to the
page, lets the town run, photographs it and puts everything away. The picture is the check, exactly as
it is for the desktop, and what the page said about itself on the way is printed beside it.

**It opens a window and cannot not**, for the reason in the [decision log](decision-log.md): headless
Chromium runs every part of this but the presentation of a WebGPU canvas.

## What a page cannot promise

**The frame is paced by the compositor.** [`Pacing`](../../../runtime/Pacing.cs) is a want on both
machines and a promise on neither, and a browser gives what it gives; there is no fence to wait on,
so [`TownRenderer.BlockedMs`](../../render/web/TownRenderer.Web.cs) is nothing rather than a different
figure — **this renderer never waits.**

**The rate is still measured, and it is the rate the town is drawn at.** A browser paces by choosing
when to ask for the next frame, so the wait falls between one frame and the next instead of inside the
submit; `Game.Step` times it there and it reaches the read-out as the same `blocked` either way. A
figure taken off the work alone would say three hundred on a display doing a hundred and twenty, which
is a read-out answering a question nobody asked.

**And a page that is not being looked at is not asked to draw at all.** A hidden tab stops the
animation callback, and the frame that resumes has waited seconds. **That is a stall and not a frame**:
past the gap [`SimClock`](../../../core/simulation/SimClock.cs) will still chase, the clock has already
dropped the time it could not simulate, and the read-out draws the line in the same place rather than
at a number of its own.

**WebGPU is asked for and may be refused.** No adapter, no device, a browser without the API at all —
each of them is a sentence under the canvas rather than a page that draws nothing.
