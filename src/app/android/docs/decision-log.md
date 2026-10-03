# The town in a hand — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md).

## 2026-10-03 — no pipeline builds this head

The owner dropped the Android workflow: the page's is the one pipeline, a tag cuts no release, and the APK
is built by hand. The head stays in the tree.

## 2026-09-11 — flags hide the bars, because an activity with no view has no insets controller

The first package crashed on launch: `Window.InsetsController` reaches through the decor view, and an
activity that has taken its own surface has none, so the getter throws inside the platform before anything
of ours runs. The theme hides the status bar and `WindowManagerFlags` do the rest. **It was found by
running the released APK on an emulator**, which is why the head is tested that way rather than only built.

## 2026-09-11 — the activity's own surface, and not a SurfaceView in a layout

The usual drawing surface on this platform is a `SurfaceView` inside a content view, which is a view
hierarchy, a measure pass and a layout to own. `Window.TakeSurface` is what a native activity does: it hands
over the whole window with nothing over it, so the bootstrap is one class, touches arrive at the activity
because no view consumes them first, and there is no platform control to keep in step with the interface
the town already draws (AND-2). What it gives up is a native control beside the canvas, which this head has
decided not to want.

## 2026-09-11 — the same Vulkan, rather than the browser head in a WebView

The page already runs on a handset, so wrapping it would have been days rather than weeks. It was refused
on one fact: WebGPU in the Android WebView is not shipped — it sits behind a flag — so the wrap would draw
nothing on the devices it was built for. The Vulkan path was cheap because the seam was already there: the
desktop head reaches the machine through `IVkSurface` and `AppWindow`, and a surface made of an
`ANativeWindow` is the same surface a window's is.

## 2026-09-11 — the shaders and the two Vulkan heads' machine are each written once

A third project file made two things copies: the `glslc` step, which both Vulkan heads need and the browser
head does not, and the half of `Game` that is about a device rather than a window. The step moved to
[`Shaders.targets`](../../../runtime/shaders/Shaders.targets) beside the shaders it compiles, and the device
half to [`Game.Vulkan.cs`](../../main/Game.Vulkan.cs), so `Game.Desktop.cs` and `Game.Android.cs` are each
one method — the amount of them that is really different.

## 2026-09-11 — the run ends with the glass, for now

A surface handed back while a swapchain still presents to it is a crash inside the platform. Keeping the
town standing across that would mean tearing the surface, the swapchain and every image view down and
building them again on the way back — `Vk.Open` creates the surface once and the renderer is laid against
it — which is a change to the machine and not to this head. So the run stops and the activity finishes
(AND-7); reopening lays the town again. It is the first cut's limit and the first thing to lift.

## 2026-09-11 — no launcher icon yet

The system draws its default. An icon is art, and art here lives at the matching path under `assets/` and
is cut by a workshop tool rather than drawn into a resource folder by hand — a job of its own and not part
of standing the head up.
