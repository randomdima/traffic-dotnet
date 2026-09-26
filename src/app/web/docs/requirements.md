# The town in a browser — requirements

The page, the module that drives WebGPU, and the boot that puts the town's own files where the town
already looks for them. **What the picture must look like is
[app/render](../../render/docs/requirements.md)** and **what a frame costs is
[runtime](../../../runtime/docs/requirements.md)**; this is the head that has no window under it.

## What the browser head is

**WEB-1** `P4` **The same town, from the same code.** The browser build compiles `src/` exactly as the desktop
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
| [`Game.Desktop.cs`](../../main/Game.Desktop.cs), [`Game.Vulkan.cs`](../../main/Game.Vulkan.cs), [`Program.cs`](../../main/Program.cs) | [`Game.Web.cs`](../../main/web/Game.Web.cs), [`Boot.cs`](../../main/web/Boot.cs) |

**The input is two arrays and they hold only what the page saw.** They are copied across whole at the
top of every frame, so **anything the run decides for itself cannot live in them** — the pump would put
the page's copy back over it. [`AppWindow.IsClosing`](../../../runtime/web/AppWindow.Web.cs) is the one
such thing, a latch beside the arrays rather than a slot in them.

**WEB-2** `P2` **The crossing budget holds, in the browser's own terms.** A standing town crosses the wall
between managed code and the page **three times a frame** and never a fourth: the animation callback
coming in, the input going out, and the frame. Everything inside a frame — the render pass, the
bundle, the queue, the submit — is on the far side of one call, and none of the three takes the size of
the town as an argument. This is rule 1 of [goals.md](../../../../docs/goals.md).
[`WebGpu.Crossings`](../../../runtime/web/WebGpu.cs) counts the calls going out, and not the callback
coming in; the figure a frame prints is taken round the frame call alone (`Game.Draw`), so it reads one.

**WEB-3** `P4` **The page carries the visual layers and none of the instruments.** The interface, the debug
layers and the figures page are the town's own picture and are all here. **The offscreen picture, the
sheet, the probes and the workshop steps are not**: `--shot`, `--sheet`, `--bench` and `--lamps` are how a
run is *measured*, they need a file system and a process that can exit, and a page has neither. A browser
is where the town is watched; the desktop is where it is answered for.

**WEB-5** `P7` **The query string is the command line.** `?map=Test&ui=nodes,claims` is `--map Test --ui
nodes,claims`. The words are the desktop's, and the ones a page cannot answer are not offered.

## What a page fetches, and when

**WEB-4** `P4` **Everything a frame reads is there before that frame.** A frame cannot wait on a fetch, so
[`Data`](../../main/web/Data.cs) writes `assets/` and `towns/` into the runtime's own file system and
every reader above is untouched ([`ProjectPaths`](../../../core/config/ProjectPaths.cs) finds them exactly
as it does beside a binary). **There is no second asset story**: no provider threaded through fifteen call
sites, and no path that means one thing here and another there.

**Nothing is *waited on* before the menu but what the menu draws**: the map list, the figures and every
city's brief ([`Data.Boot`](../../main/web/Data.cs)). Everything else — the catalogues, every variant file
and every sheet — is fetched for the first town opened, after the first frame: the idle ring behind the
menu, or the map the query string named. [`Game`](../../main/Game.cs) reads its
catalogues at the first `Open` and not in its constructor, and the renderer the menu draws through is laid
for no sheets and takes stand-ins for the ground it does not draw
([`TownRenderer.Ground`](../../render/web/TownRenderer.Web.cs)): on the desktop those pictures are on the
disk, and in a page every one of them is a round trip.

**No town crosses the wire at all.** A city is generated from a brief of a few hundred bytes and the ring
is laid in code, so what a page fetches for a map is its brief or nothing — and the briefs come down at
boot, because the menu reads a city's description out of one.

**No town is opened before the first frame, the one a page opens on included.** The animation callback is
handed to the browser as soon as the engine is running, so what a reader has within a round trip is the
menu, and the idle ring the menu stands over (GEN-1b) or the map the query string named is stood up
behind it. A page that awaited three megabytes of art before its first frame would show a blank canvas
for the whole of that wait; the desktop, whose files are on its own disk, opens the two together.

**A map is *opened* when it is picked**, in the one place a browser run may wait — the boot's own loop,
which drains the name the menu wrote down ([`Game.TakeWanted`](../../main/Game.cs)).

**What a page waits on is round trips and not bytes, and the art is one of them.** The build packs
`assets/` into a single archive and the browser unpacks it
([`WebGpu.Unpack`](../../../runtime/web/WebGpu.cs)): **a plain tar, gzipped**, because the format is
somebody else's and `DecompressionStream` is the one decompressor a page has that its .NET runtime does
not.

**WEB-9** `P7` **Nothing waits for something it does not need.** A file is asked for at the first moment it
is *known about* rather than the first moment it is wanted
([`WebGpu.Prefetch`](../../../runtime/web/WebGpu.cs)), and above it nothing changed — `grab` reads a
prefetched file exactly where it would have fetched one, and **a prefetch that fails costs an ordinary
fetch and nothing else**. Each is a different pairing:

| Started | While | Because |
|---|---|---|
| the map list ([`main.js`](../wwwroot/main.js)) | the runtime is downloading | the menu is drawn from it, so it is wanted as early as it can be had |
| the art, where a map was named ([`main.js`](../wwwroot/main.js)) | the runtime is downloading | both are about three megabytes and neither needs the other |
| the map list and the figures, then the briefs ([`Data.Boot`](../../main/web/Data.cs)) | each other | small files asked for one after the next are a round trip apiece |
| the art, where none was named ([`Data.ExpectArt`](../../main/web/Data.cs)) | the menu is already up | nothing is waiting on the wire once a page is being looked at |

**The menu waits for nothing, and that includes a fetch nobody is awaiting.** A run that named no map is
going to show a menu first, which stands on a few small files, so the art is not put on the same wire as
them at all and is asked for once there is something to look at. **And none of it starts until the
browser has answered the four questions** ([`main.js`](../wwwroot/main.js)), because a page that cannot
draw this spends no bytes.

**The sheets are decoded as one batch and not one at a time**
([`WebGpu.Decode`](../../../runtime/web/WebGpu.cs)), because a browser decodes on threads a page has not
got. **The adapter is asked for once**: the page asks before it downloads the engine and the run asks when
it starts, so [`town.js`](../wwwroot/town.js) owns the promise and both read it. **And the chain to the
runtime is told to the browser rather than discovered by it** — the `modulepreload` links in
[`index.html`](../wwwroot/index.html) make four round trips one wave — except the nine megabytes behind
`dotnet.js`, which a browser that cannot run this page should not spend to find that out.

**WEB-6** `P7` **A page is the size of its town, and the town is the size of what it draws.** What a browser
fetches before the first frame is **under six megabytes** for the fixture map and never over eight for
the heaviest: the .NET runtime ahead-of-time compiled and served brotli, 2.8 MB of art, 40 KB of page,
and the briefs. **What it fetches before the menu is a few small files**, which is the figure that
decides how long a page looks broken for — the rest arrives beside the engine, in one archive rather than
three hundred fetches. **What it fetches after that first frame is not counted here**: a figure about what
a page waits on is not a figure about what a page has spent. **How a sheet is stored is
[app/render](../../render/docs/requirements.md#how-a-sheet-is-stored)'s rule**, not a thing done to the
browser build: every head reads the same sheets.

**The page carries no image codec, because the browser is one.**
[`Texels.Web.cs`](../../render/web/Texels.Web.cs) is `createImageBitmap`, and
[`ImageHeader`](../../../core/config/ImageHeader.cs) reads a size off the file's own header on every
machine. **The decode is split because only half of it can wait**: making a bitmap is a promise and the
atlas is packed from inside `Game.Start`, which a frame reaches, while reading a bitmap's texels back is
synchronous. So [`Data`](../../main/web/Data.cs) makes every bitmap on the way in, where waiting is
allowed, and **a sheet the fetch did not decode is a fault and not a second fetch**. The Vulkan heads keep
their decoder, and it is the second opinion the header reader is checked against over every picture the
town ships ([tests/config](../../../tests/config/ImageHeaderTests.cs)).

**WEB-8** `P7` **The page says what it is doing while it does it.** The opening is a card in front of the
canvas ([`loading.js`](../wwwroot/loading.js)): the name, what this is, a bar and the stage it is in.
**A stage that can be counted fills the bar** and a stage that cannot sweeps it, because a bar sitting at
nought while the runtime comes down reads as a page that has stopped. **`say` writes there while it is up
and into the banner under the canvas once it is gone**, so an empty line is the boot saying the town is
standing — and **what the opening cost is said on the way out**, because it is the one figure a picture
cannot carry and the one every change to the boot is judged on.

## What a publish holds

**WEB-7** `P4` **What `dotnet publish` writes is the whole of what gets deployed.** The target is a stateless
static host: the folder is handed over and nothing of ours runs beside it. So **every file in it is a
real file** — no symlink into a working copy, which is a page that only serves on the machine it was
built on — and everything the page will ask for is prepared by the build: the art packed, the briefs
copied, the manifest written from the same item lists. `dotnet build` lays the identical tree beside
the binary, so what is served in development is what is deployed.

**Brotli is the host's half of this and cannot be the build's.** The publish writes a `.br` beside each
file of the framework, and a server that maps them serves 3.6 MB where the raw files are 16.3. Nothing
in the page can do it instead — the loader fetches the framework itself, and the runtime in a browser
has no brotli to unpack one with. **So a host that does not negotiate encodings serves the raw copies**,
and the figure in WEB-6 is a claim about a host that does.

**And the published folder holds one runtime.** Nothing sweeps up a hashed assembly when the next
publish replaces it, so `_framework` is cleared before a publish and only brotli is emitted — the two
together are the difference between 21 MB on disk and 93.

## How it is checked

**`qq web --shot FILE` is the browser head's `--shot`**: it publishes, serves, drives a browser to the
page, lets the town run, photographs it and puts everything away. The picture is the check, exactly as
it is for the desktop, and what the page said about itself on the way is printed beside it. **It opens a
window and cannot not**, for the reason in the [decision log](decision-log.md).

**The gestures are asked for at a place and not through an element** — `--click X,Y` and `--drag
X,Y:X,Y`, both in interface pixels — because the menu, the panels and the tabs are drawn into the canvas
and there is nothing in the page to find. **The drag is a finger and not a held mouse** (`CTL-9`), and
this is the only way a panel dragged or a camera pinched is ever driven.

**`--device W,H` is the machine the page is hardest on.** A desktop browser sized down to a phone's width
reports its own pixel ratio, and the ratio is half of what `OBS-2k` answers — so the one window where the
interface is laid on something other than the display's own factor is one a plain `--shot` cannot show.

**`qq web --debug` is the same page in ten seconds**, and the loop the boot is worked on in: a plain build
lays the identical tree (WEB-7), so everything a page fetches, unpacks, decodes and stands up is there to
be watched, with the second each stage began. **It reproduces no clock**: nothing is compiled ahead of
time in it — that happens on publish and never on build, so `-c Release` is the same interpreter — and
standing a town up is twenty seconds of interpreted arithmetic against a published page's tenth of one.
**A boot figure is read off a publish** (WEB-6); what `--debug` answers is what happened and in what order.

## What a page cannot promise

**The frame is paced by the compositor.** [`Pacing`](../../../runtime/Pacing.cs) is a want on both
machines and a promise on neither; there is no fence to wait on, so
[`TownRenderer.BlockedMs`](../../render/web/TownRenderer.Web.cs) is nothing — **this renderer never
waits.** **The rate is still the rate the town is drawn at**: a browser paces by choosing when to ask for
the next frame, so `Game.Step` times the wait between frames and it reaches the read-out as the same
`blocked` either way.

**A page that is not being looked at is not asked to draw at all.** A hidden tab stops the animation
callback, and the frame that resumes has waited seconds. **That is a stall and not a frame**: past the gap
[`SimClock`](../../../core/simulation/SimClock.cs) will still chase, the clock drops the time it could not
simulate, and the read-out draws the line in the same place rather than at a number of its own.

**WebGPU is asked for and may be refused**, and each refusal — no adapter, no device, no API at all — is a
sentence in front of the canvas rather than a page that draws nothing. **What can be refused before the
download is refused before the download**: [`main.js`](../wwwroot/main.js) asks four questions —
WebAssembly, WebAssembly SIMD, the WebGPU API, and an adapter it will actually hand out — before it
imports the runtime, and tells a reader on a browser that cannot run this which browsers can. **The
refusal inside the run stays**: `WebGpu.Start` can still fail on a device that was there a moment ago —
one question is about the browser and the other about the device it gave out.
