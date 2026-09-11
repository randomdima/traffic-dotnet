# The town in a hand — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md).

## 2026-09-11 — the activity's own surface, and not a SurfaceView in a layout

The usual way to get a drawing surface on this platform is a `SurfaceView` inside a content view, which
is a view hierarchy, a measure pass and a layout to own. `Window.TakeSurface` is what a native activity
does and it hands over the activity's whole window with nothing over it — so the bootstrap is one class,
touches arrive at the activity because no view consumes them first, and there is no platform control
anywhere in the build to keep in step with the interface the town already draws (AND-2). What it gives
up is the ability to put a native control beside the canvas, which is the thing this head has decided not
to want.

## 2026-09-11 — the same Vulkan, rather than the browser head in a WebView

The page already runs on a handset, so wrapping it would have been days rather than weeks. It was refused
on one fact: WebGPU in the Android WebView is not shipped — it sits behind a flag — so the wrap would
draw nothing on the devices it was built for, and the first thing to debug would be somebody else's
release schedule. The Vulkan path costs three files because the seam was already there: the desktop head
reaches the machine through `IVkSurface` and `AppWindow`, and a surface made of an `ANativeWindow` is the
same surface a window's is.

## 2026-09-11 — the shaders and the two Vulkan heads' machine are each written once

Adding a third project file made two things copies: the `glslc` step, which both Vulkan heads need and
the browser head has no use for, and the half of `Game` that is about a device rather than about a
window. The step moved to [`Shaders.targets`](../../../runtime/shaders/Shaders.targets) beside the
shaders it compiles, and the device half moved to [`Game.Vulkan.cs`](../../main/Game.Vulkan.cs) — so
`Game.Desktop.cs` and `Game.Android.cs` are each one method, which is the amount of them that is really
different.

## 2026-09-11 — the run ends with the glass, for now

A surface handed back while a swapchain still presents to it is a crash inside the platform, so
something had to give when the activity leaves the screen. Keeping the town standing across that would
mean tearing the surface, the swapchain and every image view down and building them again on the way
back — `Vk.Open` creates the surface once and the renderer is laid against it — which is a change to the
machine and not to this head. So the run stops, the window is released and the activity finishes
(AND-7); reopening lays the town again. It is the first cut's limit and the first thing to lift.

## 2026-09-11 — no launcher icon yet

The APK ships without one, so the system draws its default. An icon is art, and art in this project lives
at the matching path under `assets/` and is cut by a workshop tool rather than drawn into a resource
folder by hand — which is a job of its own and not part of standing the head up.
