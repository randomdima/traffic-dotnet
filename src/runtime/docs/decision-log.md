# The machine — decision log

## 2026-08-29 — the frame is paced by the display

`--present` defaults to FIFO, because mailbox costs a whole core drawing frames the display throws away
and a run of the town is looked at rather than raced. It was mailbox while a frame rate under FIFO said
nothing; measuring the presenter wait apart from the frame (`FrameParts.BlockedMs`) ended that, since the
cpu figure is the same under either mode. `--present mailbox` remains for the frame figure itself.

## 2026-08-27 — the window opens fullscreen

`AppWindow.Open` goes fullscreen and `--windowed` is the way back, because the run that wants to sit
beside something else is the rarer one. It opens windowed and moves, since neither half is choosable at
creation: Silk's `WindowState.Fullscreen` always takes the *primary* display, and GLFW sets `PPosition`
on every window before it is mapped, so the window is always born at `0,0` and the compositor's placement
never runs. A Wayland session cannot say which display the pointer is on, so `--display NAME|N` names it
outright and the display taken is printed at startup. No test, gate or probe passes through this path —
a picture needs no window.

## 2026-08-17 — raw Vulkan, and why that is not a preference for the metal

A low-level API earns its place by being **quiet**, not by being low-level. One command buffer per
swapchain image recorded once, with draw counts in a buffer the CPU writes, makes a windowed frame five
crossings and an offscreen one three, none of them taking the size of the town as an argument. A Vulkan
renderer that re-recorded every frame would be worse than OpenGL, and avoiding that is what the design is
for.

## 2026-08-17 — a picture needs no window, and that changed the renderer's shape

The recording was split from what it is recorded against, so the same pipelines draw into a swapchain
image or an offscreen target. A windowed run that screenshots itself would make every check depend on a
compositor, a desktop and whatever the window manager did to the size, none of which are in the
repository.

## 2026-08-17 — shaders are compiled by the project file, not by hand

Compilation hangs off the build so it cannot be the forgotten step, and the results are embedded so the
shipped assembly has no files to find. Two traps that fail silently — the assembly builds, ships no
shaders and says nothing — are pinned in comments at the target that carries them.
