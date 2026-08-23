# The machine — requirements

The window, the device, the swapchain, the pipelines and the shaders: everything between the simulation
and the driver. **What the picture must look like is [app/render](../../app/render/docs/requirements.md)**;
this is the layer that gets it there.

## The crossing budget

**The frame's managed→native crossing count is O(1) in the size of the town.** The boundary is not
forbidden — a window has to be opened and a buffer has to reach a GPU — it is **counted, bounded and
constant**.

- **One command buffer per swapchain image, recorded once.** Draw counts live in a buffer the CPU writes
  rather than in the calls themselves, so a windowed frame is five crossings — acquire, wait, reset,
  submit, present — and an offscreen one is three, and not one of them takes the size of the town as an
  argument.
- **Nothing is marshalled.** Blittable structs, `Span<T>` over memory the driver already owns, and
  function pointers. No array copied at the boundary, no string built per frame, no delegate allocated,
  no layout conversion, no pinning that outlives a call.
- **A re-recording renderer is the failure mode**, not a slower version of the same thing. It is what the
  gate in `src/tests/gates/CrossingGateTests.cs` exists to catch.

## What is embedded and what is found

Shaders are compiled to SPIR-V **by the build** and embedded, so a missing compiler fails the build
rather than the run and the shipped assembly has no shader files to find. The typeface is embedded for
the same reason, and is cut by a workshop tool and committed — **a tool's output, never a build step**.

Everything else the run needs — the art and the `.json` figures — is found by walking up from wherever
the binary landed, so a run from an IDE, from `dotnet run` and from the output directory all resolve the
same.

## What this layer may not know

Nothing above it. The machine opens a window and moves bytes; **it does not know what a person is, what a
sprite sheet is for, or what the town contains**. The renderer that draws a town is not this — it is
[app/render](../../app/render/docs/requirements.md), which reads this layer and not the other way about
([docs/slice-map.md](../../../docs/slice-map.md)).
