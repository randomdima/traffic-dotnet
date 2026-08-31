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

**Nothing is *waited on* before the menu but what the menu draws.** **One file** stands between the page
opening and a menu on it — the figures — and everything else is fetched when the first map is picked:
the catalogues, every variant file, every sheet, and that map's plan. Three things make it one rather
than three hundred: [`Game`](../../main/Game.cs) reads its catalogues at the first `Open` and not in
its constructor, the renderer the menu draws through is laid for no sheets, and it takes stand-ins for
the ground it does not draw ([`TownRenderer.Ground`](../../render/web/TownRenderer.Web.cs)) — on the
desktop those pictures are already on the disk, and in a page every one of them is a round trip.

**No town is opened before the first frame, the one a page opens on included.** The animation callback is
handed to the browser as soon as the engine is running, so what a reader has within a round trip is the
menu — and the idle ring the menu stands over (GEN-1b), or the map the query string named, is fetched and
stood up behind it. A page that awaited a plan and three megabytes of art before its first frame would
show a blank canvas for the whole of that wait, which is the one thing this head cannot afford; the
desktop, whose files are on the disk it started from, opens the two together.

**A map is *opened* when it is picked**, and the opening happens in the one place a browser run may wait
— the boot's own loop, which drains the name the menu wrote down (`Game.PickMap`). The bytes are
usually already here by then and the wait is nothing (WEB-9); what stands between the click and the
open is that read, wherever the plan came from. What `Data` lays for a map at boot is its *name*: an
empty file, because
[`ProjectPaths.ShippedMaps`](../../../core/config/ProjectPaths.cs) reads the folder and a map with no
file would be a map the menu could not offer. It is never read in that state — the fetch stands between
the click and the open.

**What a page waits on is round trips and not bytes, and the art is one of them.** Three hundred small
files asked for one after the next is a minute of latency against two seconds of downloading, and it
was the whole of why the page opened slowly. So the build packs `assets/` into a single archive and
the browser unpacks it ([`WebGpu.Unpack`](../../../runtime/web/WebGpu.cs)): **a plain tar, gzipped**,
because the format is somebody else's and `DecompressionStream` is the one decompressor a page has
that its .NET runtime does not.

**WEB-9 — nothing waits for something it does not need.** A page that fetches in the order it happens
to read waits for the sum of what it asked for, and three of those waits were for files nobody needed
yet. So a file is asked for at the first moment it is *known about* rather than the first moment it is
wanted ([`WebGpu.Prefetch`](../../../runtime/web/WebGpu.cs)), and above it nothing changed — `grab`
reads a prefetched file exactly where it would have fetched one, and **a prefetch that fails costs an
ordinary fetch and nothing else**. Three of them, and each is a different pairing:

| Started | While | Because |
|---|---|---|
| the map list ([`main.js`](../wwwroot/main.js)) | the runtime is downloading | the menu is drawn from it, so it is wanted as early as it can be had |
| the art, where a map was named ([`main.js`](../wwwroot/main.js)) | the runtime is downloading | both are about three megabytes and neither needs the other |
| the map list and the figures ([`Data.Boot`](../../main/web/Data.cs)) | each other | two round trips were being spent on 229 bytes |
| the plan of the map picked ([`Data.Expect`](../../main/web/Data.cs)) | the art is being decoded | one is the wire and the other is the processor |
| the art, where none was named, and every other plan ([`Data.ExpectArt`](../../main/web/Data.cs), [`ExpectEvery`](../../main/web/Data.cs)) | the menu is already up | nothing is waiting on the wire once a page is being looked at |

**The menu waits for nothing, and that includes a fetch nobody is awaiting.** A run that named a map
has that town as its destination and starts the art beside the engine; a run that did not is going to
show a menu first — which stands on two small files — so the art is not put on the same wire as them at
all, and is asked for once there is something to look at, which is also when the idle ring behind the
menu begins coming down. Three megabytes sharing a link with 229 bytes is
a menu that comes up later, and how long a page looks broken for is the figure that matters most about
it. **And none of it starts until the browser has answered the four questions**, because a page that
cannot draw this spends no bytes at all ([`main.js`](../wwwroot/main.js)).

**The other plans are the one that waits for the first frame**, and that is the line between this and
the thing the [decision log](decision-log.md) refuses: 3.4 MB standing between a reader and a menu is a
page that opens slowly, and the same 3.4 MB behind a page already being looked at is a wire that would
otherwise be idle. So they are asked for after the animation callback has been handed over, never
before it, and the map already open is not among them.

**The sheets are decoded as one batch and not one at a time** ([`WebGpu.Decode`](../../../runtime/web/WebGpu.cs)).
A loop awaiting one decode in turn is one decode at a time, and a browser decodes on threads a page
has not got: the town's 174 sheets are 216 ms in a row against 57 ms asked for together. They are all
kept for the run in any case, so asking for them at once costs no memory that asking in tens would not.

**And the adapter is asked for once.** The page asks before it downloads the engine and the run asks
when it starts, and those are the same question — so [`town.js`](../wwwroot/town.js) owns the promise
and both read it.

**And the chain to the runtime is told to the browser rather than discovered by it.** Left alone it
learns of `town.js` from parsing `main.js` and of the runtime from running it, which is four round
trips before the engine is asked for; the `modulepreload` links in
[`index.html`](../wwwroot/index.html) make them one wave. **The nine megabytes behind `dotnet.js` are
not preloaded** — a browser that cannot run this page should not spend them to find that out.

**WEB-6 — a page is the size of its town, and the town is the size of what it draws.** What a browser
fetches before the first frame is **under six megabytes** for the fixture map and never over eight for
the heaviest: the .NET runtime ahead-of-time compiled and served brotli, 2.8 MB of art, 40 KB of page,
and one map. **What it fetches before the menu is one file**, which is the figure that decides how
long a page looks broken for — the rest arrives beside the engine, in one archive rather than three
hundred fetches. **What it fetches after that first frame is not counted here**: the other eight plans
come down while the reader is deciding what to click (WEB-9), and a figure about what a page waits on
is not a figure about what a page has spent. **How a sheet is stored is
[app/render](../../render/docs/requirements.md#how-a-sheet-is-stored)'s rule**, not a thing done to
the browser build: both heads read the same sheets.

**The page carries no image codec, because the browser is one.** A decoder is the largest thing this
head can decline to ship — it is priced by what it makes the ahead-of-time compiler emit rather than by
what it weighs — so [`Texels.Web.cs`](../../render/web/Texels.Web.cs) is `createImageBitmap` and
[`ImageHeader`](../../../core/config/ImageHeader.cs) reads a size off the file's own header on both
machines. **The decode is split because only half of it can wait**: making a bitmap is a promise and the
atlas is packed from inside `Game.Start`, which a frame reaches; drawing one to a canvas and reading its
texels back is synchronous. So [`Data`](../../main/web/Data.cs) makes every bitmap on the way in, where
waiting is allowed, and the packer reads one sheet out where it stands. **A sheet the fetch did not
decode is a fault and not a second fetch** — there is no way back to a promise from inside a frame,
which is why the art is laid before the plan it is going to be drawn on is even read.

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
built on — and everything the page will ask for is prepared by the build: the art packed, the towns
squeezed, the manifest written from the same item lists. `dotnet build` lays the identical tree beside
the binary, so what is served in development is what is deployed.

**Brotli is the host's half of this and cannot be the build's.** The publish writes a `.br` beside each
file of the framework, and a server that maps them serves 3.6 MB where the raw files are 16.3. Nothing
in the page can do it instead — the loader fetches the framework itself, and the runtime in a browser
has no brotli to unpack one with (which is the same fact that keeps the towns on gzip). **So a host that
does not negotiate encodings serves the raw copies**, and the figure in WEB-6 is a claim about a host
that does.

**WEB-8 — the page says what it is doing while it does it.** The opening is a card in front of the
canvas ([`loading.js`](../wwwroot/loading.js)): the name, what this is, a bar and the stage it is in.
**A stage that can be counted fills the bar** — a batch knows how many files it asked for — and a stage
that cannot sweeps it, because a bar sitting at nought while the runtime comes down reads as a page
that has stopped. **`say` writes there while it is up and into the banner under the canvas once it is
gone**, so an empty line is the boot saying the town is standing and the card is what it takes away —
and **what the opening cost is said on the way out**, because it is the one figure a picture cannot
carry and the one every change to the boot is judged on.

**WEB-5 — the query string is the command line.** `?map=Test&ui=nodes,paths` is `--map Test --ui
nodes,paths`. The words are the desktop's, and the ones a page cannot answer are not offered.

## How it is checked

**`qq web --shot FILE` is the browser head's `--shot`**: it publishes, serves, drives a browser to the
page, lets the town run, photographs it and puts everything away. The picture is the check, exactly as
it is for the desktop, and what the page said about itself on the way is printed beside it.

**It opens a window and cannot not**, for the reason in the [decision log](decision-log.md): headless
Chromium runs every part of this but the presentation of a WebGPU canvas.

**`qq web --debug` is the same page in ten seconds**, and it is the loop the boot is worked on in: a
plain build lays the identical tree (WEB-7), so everything a page fetches, unpacks, decodes and stands
up is there to be watched. **What it does not reproduce is any clock at all** — nothing is compiled
ahead of time in it, and the ahead-of-time step happens on publish and never on build, so `-c Release`
is the same interpreter and not a third option. A read-out in one of those pictures is measuring the
interpreter, and so is the load: the wire and the browser's own work are faithful to a tenth of a
second — the fetch, the unpack, the decode and the laying are under two seconds either way — but
standing a town up is managed arithmetic, and it is twenty seconds against a published page's tenth of
one. **A boot figure is read off a publish** (WEB-6); what `--debug` answers is what happened and in
what order, which is what its console prints the elapsed second of each stage for.

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
each of them is a sentence in front of the canvas rather than a page that draws nothing.

**And what can be refused before the download is refused before the download.**
[`main.js`](../wwwroot/main.js) asks four questions — WebAssembly, WebAssembly SIMD, the WebGPU API,
and an adapter it will actually hand out — and only then imports the runtime, which is why that import
is dynamic. A reader on a browser that cannot run this is told so in milliseconds against no download
at all, and told which browsers can. **The refusal inside the run stays where it is**: `WebGpu.Start`
can still fail on a device that was there a moment ago, and the two are not one check in two places —
one is about the browser and one is about the device it gave out.
