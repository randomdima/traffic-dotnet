# The town in a hand — requirements

The activity, the glass it hands over, and the thread the loop runs on. **What the picture must look
like is [app/render](../../render/docs/requirements.md)** and **what a frame costs is
[runtime](../../../runtime/docs/requirements.md)**; this is the head whose window is an activity.

## What the handset head is

**AND-1** `P4` **The same town, from the same code, on the desktop's own machine.** The handset build
compiles `src/` exactly as the desktop build does and draws with the same Vulkan, the same swapchain,
the same renderer and the same SPIR-V — so **rule 1 is not restated here**: a frame crosses the wall the
five times [runtime](../../../runtime/docs/requirements.md#the-crossing-budget) counts, because it is the
same five crossings in the same file. What differs is the bootstrap, and it is three files:

| The desktop's | The handset's |
|---|---|
| [`AppWindow`](../../../runtime/AppWindow.cs) | [`AppWindow.Android.cs`](../../../runtime/android/AppWindow.Android.cs), [`AndroidSurface`](../../../runtime/android/AndroidSurface.cs) |
| [`Game.Desktop.cs`](../../main/Game.Desktop.cs), [`Program.cs`](../../main/Program.cs) | [`Game.Android.cs`](../../main/android/Game.Android.cs), [`TownActivity`](../TownActivity.cs) |

**What the two Vulkan heads answer alike is written once** — the device, the renderer, the crossing
counter and the thread a town is laid on are [`Game.Vulkan.cs`](../../main/Game.Vulkan.cs), compiled by
both and by neither of the browser's halves. There is no `#if` anywhere in the shared code; the three
project files pick which half is compiled and that is the whole mechanism.

**AND-2** `P4` **The bootstrap is one activity and nothing else.** No view, no layout, no Android widget:
the activity takes its own window's surface (`Window.TakeSurface`), hands the native window down to the
machine and runs this engine's own loop on a thread of its own. **Everything a reader touches above that
line is the town's own interface**, drawn by the same [`Interface`](../../hud/Interface.cs) the desktop
and the page draw — a platform control over it would be a second interface answering questions the first
one already answers, in a second style, on one screen.

**The one call that needs the platform is `ANativeWindow_fromSurface`**, and it is in the activity
because it needs a JNI environment and a `jobject`. What crosses down to `runtime/` is a pointer, which
is why the machine knows nothing about Android.

**AND-3** `P4` **The town's files are unpacked once and then read where they always are.** An APK is a
zip and [`ProjectPaths`](../../../core/config/ProjectPaths.cs) walks a tree, so the build packs
`assets/` and `towns/` into one archive, [`Papers`](../Papers.cs) unpacks it into the app's own folder on
the first run after an install, and `ProjectPaths` is **told** where that is. **There is no second asset
story** — no provider threaded through fifteen call sites, and no path that means one thing here and
another on a desk. An archive rather than four hundred assets for the same reason the page has one: it is
one open, one inflate and one pass.

**AND-4** `P4` **The handset carries the picture and none of the instruments.** The interface, the debug
layers, the scenario panel and the figures page are the town's own picture and are all here. **The
offscreen picture, the sheet, the probes and the workshop steps are not**: `--shot`, `--sheet`, `--bench`
and `--lamps` are how a run is *measured*, and they want a file system to write to and a process that
can exit. It is the same line the page draws (WEB-3), drawn in the same place.

**AND-5** `P7` **The intent's extras are the command line.** `-e map Odesa -e ui nodes,paths` is `--map
Odesa --ui nodes,paths`, and `-e seconds` and `-e ui-scale` are the words they look like. The words are
the desktop's, and the ones a handset cannot answer are not offered.

**AND-6** `P7` **The fingers are the page's fingers.** The first one down is the pointer and the button
and the rest are the camera's, in the order they came down — the same rule, the same ordering and the same
two positions a frame apart that [`TouchGesture`](../../playercontrol/TouchGesture.cs) already reads
(CTL-9), so a pinch means on glass what it means in a mobile browser. **There is no keyboard and nothing
above [`AppWindow`](../../../runtime/android/AppWindow.Android.cs) learns that**: the one key the platform
sends is the way back, and it arrives as `Escape` because that is the key the interface answers with the
menu (OBS-2g). A wheel is a mouse's and answers zero; zooming is the pinch.

**AND-7** `P6` **A lost surface ends the run, and the window is released after the loop has stopped.**
The system takes the surface back when the activity leaves the screen, and past that call a swapchain
presenting to it is a crash in the platform's own code — so the run is told to stop and **waited out** on
the activity's own thread before the native window is given back. The activity then finishes: a process
holding no glass is a town nobody can see, and the way back in is to open it again.

**AND-8** `P6` **The floor is the driver's, and it is declared rather than discovered.** The instance is
created at Vulkan 1.3 and the shaders are SPIR-V 1.6, so a device whose loader answers 1.1 cannot create
an instance at all. The manifest asks for that version as **required**, which is what keeps a store from
offering this build to a handset it would open black on. 64-bit ABIs only, for the same reason: a device
with a 1.3 driver has a 64-bit userland.

## How it is checked

**The APK is built by a pipeline of its own** — [`.github/workflows/android.yml`](../../../../.github/workflows/android.yml),
which is a tag push and a `workflow_dispatch` and shares nothing with the page's. It builds the release
package, signs it when the keystore secrets are configured and debug-signs it when they are not, and
leaves both an APK and an AAB as the run's artifacts. **A green build is the whole of what it claims**:
that this head compiles, packs the town and packages, on a machine that is not the one it was written on.

**A tag ends in a release and a dispatch ends in an artifact.** At a tag the same run creates the GitHub
release, attaches both packages to it, and says in the notes which key signed them — because a package
that can only be reached through a run number is not something a reader can download, and an install
signed by one key is not one Android will replace with the other. A tag with a suffix — `v0.2.0-rc1` — is
a pre-release; rebuilding a tag uploads over what is attached rather than failing on a release that
already exists.

**The tiers do not reach this head, and the reason is the target framework.** The suite is `net10.0` and
compiles against the desktop head ([tests](../../../tests/)), so a test cannot construct an activity or a
handset window. What *is* checked is everything above the bootstrap, which is the engine the two other
heads are already checked through — and **what is left to a device is the bootstrap itself**: a surface,
a size, a density, fingers, and the pictures a reader takes of it.

**A run says what it is doing in logcat.** Everything the engine prints — the framebuffer line, the
crossing count, the frame budget, the claims table — comes out under the `town` tag
([`LogWriter`](../LogWriter.cs)), so `adb logcat -s town` is what a handset has in place of a terminal.
