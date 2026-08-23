# The machine — decision log

## 2026-08-17 — raw Vulkan, and why that is not a preference for the metal

A low-level API earns its place here by being **quiet**, not by being low-level.

One command buffer per swapchain image, recorded **once**, with the draw counts living in a buffer the
CPU writes rather than in the calls themselves, makes a windowed frame five managed→native crossings and
an offscreen one three — and not one of them takes the size of the town as an argument.

**A Vulkan renderer that re-recorded every frame would be the worst option available**, worse than
OpenGL, and avoiding exactly that is what the design is for. Owning the upload path and making the
frame's call count a constant is the goal; being close to the metal is a side effect.

## 2026-08-17 — a picture needs no window, and that changed the renderer's shape

A render check needs a frame, not a window, so the recording was split from what it is recorded against:
the same pipelines draw into a swapchain image or into an offscreen target, and the offscreen path is
three crossings rather than five.

Doing it the other way round — a windowed run that screenshots itself — makes every check depend on a
compositor, a desktop and whatever the window manager did to the size, and none of those are in the
repository.

## 2026-08-17 — shaders are compiled by the project file, not by hand

Compilation hangs off the build so it cannot be the forgotten step, and the results are embedded so the
shipped assembly has no files to find. Two traps were paid for once, and **both fail silently — the
assembly builds, ships no shaders and says nothing** — so they are pinned in comments at the target that
carries them rather than repeated here.
