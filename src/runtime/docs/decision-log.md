# The machine — decision log

## 2026-08-29 — the frame is paced by the display

`--present` defaults to FIFO rather than mailbox, so a run draws at the refresh rate and no faster.
Mailbox costs a whole core drawing frames the display throws away, and a run of the town is looked at
rather than raced.

**It was mailbox because a frame rate under FIFO says nothing** — the read-out is the refresh rate
whatever the town costs, 120 fps on Test and 120 on Odesa. That argument stopped holding once the
wait on the presenter was measured apart from the frame (`FrameParts.BlockedMs`): the cpu figure is
what this build costs, and it is the same figure under either mode. `--present mailbox` is still
there for the frame figure itself.

## 2026-08-27 — the window opens fullscreen

A run of the town is looked at, not compared against the editor beside it, and the frame rate a
windowed run reports is only about the whole display anyway. So `AppWindow.Open` goes fullscreen and
`--windowed` is the way back — the reverse of what it was, because the run that wants to sit beside
something else is the rarer one.

**It opens windowed and moves**, rather than asking for fullscreen up front, because neither half of
that is choosable at creation: the platform places a new window where it likes, and Silk's
`WindowState.Fullscreen` always takes the *primary* display whatever the window was on. The display
wanted is the one the pointer is on — the pointer is where the run was started from, and it is the
only thing on the desktop that says so.

**A Wayland session cannot answer that**, since a client is told where the cursor is only while it is
over one of its own surfaces, and nothing else it may ask names the display in front of the person.

**Letting the desktop place the window instead does not work either**, and it was measured rather than
assumed: GLFW sets `PPosition` on every window before it is mapped — its own comment calls it a hack,
against window managers that ignore the position of unmapped windows otherwise — so the window is
always born at the origin and the compositor's placement never runs. Dodging Silk's own
`SetWindowPos` changes nothing; the window still opens at `0,0`. The display containing that corner
is then whatever is arranged there, and where the displays neither overlap it nor share an origin the
search ends at the *primary*, which is the screen nobody was looking at.

So `--display NAME|N` names it outright, and the display a run took is printed with the framebuffer at
startup, because a wrong one is otherwise a thing to describe rather than a thing to read.

Nothing about the checks moved with it: a picture still needs no window (`--shot`), so no test, gate
or probe passes through this path at all.

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
