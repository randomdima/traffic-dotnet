# The town in a hand — requirements

The activity, the glass it hands over, and the thread the loop runs on. Everything above that is the
desktop's own machine and answers to the documents the desktop does
([app/render](../../render/docs/requirements.md), [runtime](../../../runtime/docs/requirements.md)); this
is the head whose window is an activity.

## What the handset head is

**AND-1** `P4` **The same town, from the same code, on the desktop's own machine.** The handset build
compiles `src/` exactly as the desktop build does and draws with the same Vulkan, the same swapchain,
the same renderer and the same SPIR-V — so **rule 1 is not restated here**: a frame crosses the wall the
five times [runtime](../../../runtime/docs/requirements.md#the-crossing-budget) counts, because it is the
same five crossings in the same file. What differs is the bootstrap:

| The desktop's | The handset's |
|---|---|
| [`AppWindow`](../../../runtime/AppWindow.cs) | [`AppWindow.Android.cs`](../../../runtime/android/AppWindow.Android.cs), [`AndroidSurface`](../../../runtime/android/AndroidSurface.cs) |
| [`Game.Desktop.cs`](../../main/Game.Desktop.cs), [`Program.cs`](../../main/Program.cs) | [`Game.Android.cs`](../../main/android/Game.Android.cs), [`TownActivity`](../TownActivity.cs) |

**What the two Vulkan heads answer alike is written once** — the device, the renderer, the crossing
counter and the thread a town is laid on are [`Game.Vulkan.cs`](../../main/Game.Vulkan.cs), compiled by
both and by neither of the browser's halves. How the halves are picked is WEB-1's mechanism: no `#if`,
and the project file chooses.

**AND-2** `P4` **The bootstrap is one activity and nothing else.** No view, no layout, no Android widget:
the activity takes its own window's surface (`Window.TakeSurface`), hands the native window down to the
machine and runs this engine's own loop on a thread of its own. **Everything a reader touches above that
line is the town's own interface**, drawn by the same [`Interface`](../../hud/Interface.cs) the desktop
and the page draw — a platform control over it would be a second interface answering questions the first
one already answers, in a second style, on one screen.

**AND-3** `P4` **The town's files are unpacked once and then read where they always are.** An APK is a
zip and [`ProjectPaths`](../../../core/config/ProjectPaths.cs) walks a tree, so the build packs
`assets/` and `towns/` into one archive, [`Papers`](../Papers.cs) unpacks it into the app's own folder on
the first run after an install, and `ProjectPaths` is **told** where that is. **There is no second asset
story** (WEB-4). One archive rather than four hundred assets, because it is one open, one inflate and one
pass.

**AND-4** `P4` **The handset carries the picture and none of the instruments** — the line the page draws
(WEB-3), drawn in the same place: `--shot`, `--sheet`, `--bench` and `--lamps` want a file system to write
to and a process that can exit.

**AND-5** `P7` **The intent's extras are the command line.** `-e map Odesa -e ui nodes,claims` is `--map
Odesa --ui nodes,claims`, and `-e seconds` and `-e ui-scale` are the words they look like. The words are
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

**No pipeline builds this head** ([decision log](decision-log.md)), so nothing outside this desk says it
still compiles.

**What a build cannot answer is answered on an emulator, and the picture is the check** — exactly as it
is for the other two heads:

```
sdkmanager --install "platform-tools" "emulator" "system-images;android-35;google_apis;x86_64"
avdmanager create avd -n town -k "system-images;android-35;google_apis;x86_64" -d pixel_6
emulator -avd town -gpu host -no-window        # -gpu host, or the guest has no Vulkan 1.3 to open
dotnet build traffic-dotnet.android.csproj -c Debug -t:SignAndroidPackage -p:EmbedAssembliesIntoApk=true
adb install -r bin/android/Debug/net10.0-android/*-Signed.apk
adb shell am start -n dev.trafficdotnet.town/.TownActivity -e map Test -e ui nodes
adb exec-out screencap -p > .tmp/handset.png
```

**`EmbedAssembliesIntoApk` is not optional in that loop**: a Debug package is built for fast deployment
and its assemblies are pushed separately, so one installed by hand aborts on a runtime that finds none.
**`-gpu host` is not optional either** — the emulator's software driver answers Vulkan 1.1 (AND-8).

**The figure the head is judged on is the frame rate, read off the read-out in a picture.** On an x86_64
emulator over the fixture town it is 60 fps compiled ahead of time and 55 interpreted, which says the pace
is the display's rather than this build's — a handset's own figure is a fact about that handset.

**A named map lays before the first frame, and on a handset that is a black screen** (`-e map Odesa` is
the whole of a city being generated on one core). It is the desktop's own semantics and the extras are a
testing affordance; **the menu is the way in that never waits**, and what it shows while a city is laid
is the card (OBS-2n).

**The tiers do not reach this head, because the suite is `net10.0`** and compiles against the desktop head
([tests](../../../tests/)), so a test cannot construct an activity or a handset window. Everything above
the bootstrap is checked through the other two heads; **what is left to a device is the bootstrap itself**
— a surface, a size, a density, fingers, and the pictures a reader takes of it.

**A run says what it is doing in logcat.** Everything the engine prints comes out under the `town` tag
([`LogWriter`](../LogWriter.cs)), so `adb logcat -s town` is what a handset has in place of a terminal.
