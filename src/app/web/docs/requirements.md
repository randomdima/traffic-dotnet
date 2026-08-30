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
| [`Game.Desktop.cs`](../../main/Game.Desktop.cs), [`Program.cs`](../../main/Program.cs) | [`Game.Web.cs`](../../main/web/Game.Web.cs), [`Boot.cs`](../../main/web/Boot.cs) |

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

**WEB-4 — everything the run reads is there before the first frame.** A town is opened from inside the
loop and a loop cannot wait on a fetch, so [`Data`](../../main/web/Data.cs) writes `assets/` and
`towns/` into the runtime's own file system first and every reader above is untouched
(<see cref="ProjectPaths"/> finds them exactly as it does beside a binary). **There is no second asset
story**: no provider threaded through fifteen call sites, and no path that means one thing here and
another there.

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
figure.

**WebGPU is asked for and may be refused.** No adapter, no device, a browser without the API at all —
each of them is a sentence under the canvas rather than a page that draws nothing.
