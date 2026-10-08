# The machine — decision log

## 2026-10-08 — rows changed in many places are copied by a kernel, not by the copy engine

`ResidentRows.Send` copied a table's marked rows as one copy of many regions, and on the PCIe link of 5 GT/s × 4 it
was measured on a region costs about 0.6 µs whatever its size (`--bench devicecopy`: 1 MB in 1 000 runs at 0.97 GB/s
against 1.81 in one). A town ticked on the device sent 428 regions a tick and 618 a rebuild — mostly the eight tables
of every car whose line was laid again. Past eight runs a send is now `copy_runs.comp` reading the host's mapped copy
over the bus, a workgroup a run, which pays a dispatch whatever the count; so `Vk.AfterTransfer` and
`Vk.BeforeTransfer` order a kernel's writes as they order a copy's. OdesaOsm (`--bench devicetown`): 6.22 → 5.80 ms a
tick, 34 regions a tick and 24 a rebuild.

## 2026-09-11 — the swapchain asks for identity, and lets the presenter turn the frame

`PreTransform` was the surface's own current transform, which on a desktop is identity and on a handset
is a quarter turn the moment a landscape run is on a portrait panel. It is a promise about the images
handed over, not a request — so the town came up on its side, drawn correctly and then read as though it
had already been rotated. Identity is asked for wherever the surface supports it, which every surface
this engine has met does, and the presentation engine turns the frame instead. The alternative is the
rotation in the projection matrix and the extent swapped with it, which saves a composition pass on a
phone and costs the renderer a second way of being right; it is worth doing when a frame budget says so
and not before.

## 2026-08-29 — the frame is paced by the display

`--present` defaults to FIFO, because mailbox costs a whole core drawing frames the display throws away
and a run of the town is looked at rather than raced. It was mailbox while a frame rate under FIFO said
nothing; measuring the presenter wait apart from the frame (`FrameParts.BlockedMs`) ended that, since the cpu figure
is the same under either mode. `--present mailbox` remains for the frame figure itself.

## 2026-08-27 — the window opens fullscreen

`AppWindow.Open` goes fullscreen and `--windowed` is the way back, because the run that wants to sit
beside something else is the rarer one. It opens windowed and moves, since neither half is choosable at
creation: Silk's `WindowState.Fullscreen` always takes the *primary* display. It takes the display the
pointer is on where the session can say; a Wayland session cannot, so `--display NAME|N` names it outright,
and the display taken is printed at startup. No test, gate or probe passes through this path — a picture needs no window.

## 2026-08-17 — raw Vulkan, and why that is not a preference for the metal

A low-level API earns its place by being **quiet**, not by being low-level: the arrangement that keeps a
frame to a handful of crossings is [requirements.md](requirements.md#the-crossing-budget), and a Vulkan
renderer that re-recorded every frame would be worse than OpenGL.

## 2026-08-17 — a picture needs no window, and that changed the renderer's shape

The recording was split from what it is recorded against, so the same pipelines draw into a swapchain
image or an offscreen target. A windowed run that screenshots itself would make every check depend on a
compositor, a desktop and whatever the window manager did to the size, none of which are in the
repository.

## 2026-08-17 — shaders are compiled by the project file, not by hand

Compilation hangs off the build so it cannot be the forgotten step, and the results are embedded so the
shipped assembly has no files to find. Two traps that fail silently — the assembly builds, ships no
shaders and says nothing — are pinned in comments at the target that carries them.
