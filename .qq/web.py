"""
Publish the browser head, serve it, and take a picture of it running.

The desktop's picture is the game's own `--shot`, which needs no window and no display. A page has
no such thing: the town is drawn into a canvas by the browser, so the only way to look at it is to
open one, and the only way to do that without a person sitting there is to drive one.

    qq web                          publish the head and say where it landed
    qq web --serve                  publish and serve it until interrupted
    qq web --shot FILE              publish, serve, drive a browser, write the picture, tear it down
    qq web --shot F --ui frame      with the read-out open, which is the query string doing --ui
    qq web --shot F --at 3028,13339 opening looking there rather than at OBS-1b's own framing
    qq web --shot F --click 40,28   clicking there first, since the interface is inside the canvas
    qq web --shot F --drag A:B      a finger drawn from A to B, which is how the touch gestures are asked for
    qq web --shot F --device 390,844  as a handset sees it: a phone's points at a phone's pixel ratio
    qq web --shot F --debug         the Debug build laid beside the binary: seconds, not ten minutes
    qq web --shot F --ui frame --hz 30
                                    frames offered at 30 Hz, as a slow display would: the town keeps real time
    qq web --shot F --map OdesaOsm --cpu .tmp/web/cpu.md
                                    the page's CPU over its open, by function, the profile kept beside it
    qq web --cpu-read .tmp/web/cpu.cpuprofile --focus mono_interp_exec_method --managed
                                    a kept profile asked again: what enters the interpreter, by managed caller

**A page's open is read off its console**: it says the plan, the town, the handover and the memory held after
each, and whether the handover's leavings were let go (src/app/main/web/Game.Web.cs, Boot.cs).

**`--debug` is for the load path and never for the frame rate.** `dotnet build` lays the same page
against an interpreted runtime in about ten seconds, so what a page fetches, unpacks, decodes and
stands up is all there to be watched — and the town runs about ten times off the pace, so the
read-out in that picture is measuring the interpreter. Anything about how fast it draws wants the
Release publish.

**The picture wants a real browser window**, and `--shot` opens one. Headless Chromium has the API
and an adapter, and everything up to the canvas works in it — the shaders compile, the town stands
up, the draws submit — but presenting to a WebGPU canvas loses the device, so a headless shot is a
white page. The window is opened, photographed and closed.

**It wants a Chromium of its own, in here, and not the one anybody is reading in.** A Chromium that is
already running ignores the debugging port a second invocation asks for, so driving the desktop
browser means closing it first — which is somebody's tabs. Install one alongside this project:

    sudo dnf install -y chromium

Failing that, `--browser` names whatever should be driven instead. The browser is closed over the
protocol when the picture is taken, so runs do not leave a row of windows behind.

**Release, always.** `RunAOTCompilation` is on in Release and off in Debug, and the interpreter is
about ten times off a 60 Hz loop — a picture of the Debug build is a picture of a town that is not
keeping up. It costs about ten minutes on the clock, so call it as `qq web --timeout=900`.
"""

from __future__ import annotations

import argparse
import base64
import json
import os
import shutil
import socket
import struct
import subprocess
import threading
import time
import urllib.parse
import urllib.request
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import _sim
from common import cli

#: The browser head, and where its published page lands.
#: What a call may take: a publish, a browser, and `--seconds` of a town running in front of it.
TIMEOUT_S = 900

PROJECT = "traffic-dotnet.web.csproj"
BUNDLE = "bin/web/Release/net11.0/publish/wwwroot"

#: And where a plain build lays the same page, minus the ahead-of-time compilation and the brotli beside
#: each file of the framework. It is the same page and the same load path (WEB-7).
DEBUG_BUNDLE = "bin/web/Debug/net11.0/wwwroot"

#: **Since .NET 11 a build copies no `_framework/` into its `wwwroot`** (two files of two hundred): the rest
#: stays under obj/ and this map, which `dotnet run`'s dev server reads, says where each URL's file is.
#: Read beside a bundle when it is there; a publish writes none and is served as the folder it is.
ASSET_MAP = "traffic-dotnet-web.staticwebassets.runtime.json"

#: **And a Release build is not a third option.** Ahead-of-time compilation happens on publish and never
#: on build, so `-c Release` is the same interpreter over optimised IL: measured against the Debug page
#: it stands the town up in the same twenty seconds, where the publish spends a tenth of one. There are
#: two modes here because there are two, and what separates them is the AOT step alone.

#: What the publish writes beside each file of the framework, and what this server hands out in its
#: place. See :class:`Quiet`.
COMPRESSED = ".br"

#: Browsers that speak the DevTools protocol, most likely first, and looked for **on this machine
#: only** — inside the container this project is worked on in, never out on the desktop the container
#: sits in. Two reasons, and the second is the one that matters: a browser out there is the one
#: somebody is reading in, and a Chromium already running ignores the debugging port a second
#: invocation asks for, so driving it means closing it first and that is somebody's tabs.
BROWSERS = ("chromium", "chromium-browser", "google-chrome", "google-chrome-stable")

#: How to get one, said in the message rather than left to be worked out.
INSTALL = "sudo dnf install -y chromium"

#: What a browser is told before it will open a page instead of asking somebody to agree to something.
#: A driven browser has nobody sitting at it, so every one of these is a dialog that would otherwise be
#: waited on for ever.
#:
#: **Both WebGPU flags are still needed on a desktop Linux**, whatever the release notes say about it
#: being on by default: dropping either one was tried, and the page comes back "No WebGPU adapter: the
#: browser has the API but no device it will hand out."
#:
#: `--test-type` is what stops the yellow "unsupported command-line flag" bar the first of them earns —
#: a browser warning somebody about the page this tool is photographing, in a strip across the top of
#: the picture. It is the flag a driven browser is meant to carry and it says so.
#:
#: **`--ozone-platform=x11` is what lets a page tick behind another window**: on Wayland the compositor
#: sends a covered window no frame callbacks, so the page is given no animation frames and no tick
#: runs; under XWayland Chromium paces its own frames, and a covered window paints at 60 Hz.
FLAGS = (
    "--enable-unsafe-webgpu",
    "--enable-features=Vulkan",
    "--ozone-platform=x11",
    "--test-type",
    "--no-first-run",
    "--no-default-browser-check",
    "--disable-fre",
    "--disable-search-engine-choice-screen",
)


def main():
    a = parse_args()
    base = _sim.root()
    if a.cpu_read:
        kept = Path(a.cpu_read)
        out = Path(a.cpu) if a.cpu else kept.with_suffix(".md")
        out.write_text(cpu_tables(json.loads(kept.read_text()), a.limit, symbols(base / BUNDLE), 0.0, True, a.focus,
                                  a.managed))
        print(f"{out} written")
        return 0

    if a.cpu and not a.shot:
        cli.fail("--cpu samples the run a --shot takes: name the picture as well")

    if a.url:
        if not a.shot:
            cli.fail("--url is driven by a --shot: name the picture as well")
        return shoot(base, None, a)

    if not a.no_build and not publish(base, a.debug, named=a.cpu is not None):
        return 1

    bundle = base / (DEBUG_BUNDLE if a.debug else BUNDLE)
    if not bundle.is_dir():
        cli.fail(f"no page at {_sim.rel(base, bundle)}: run without --no-build")

    if a.shot:
        return shoot(base, bundle, a)
    if a.serve:
        return serve_until_interrupted(bundle, a.port)

    print(f"the page is at {_sim.rel(base, bundle)}")
    print(f"serve it with: cd {_sim.rel(base, bundle)} && python3 -m http.server {a.port}")
    print(f"then open:     http://localhost:{a.port}/")
    return 0


def publish(base: Path, debug: bool, named: bool = False) -> bool:
    """
    The head, ahead-of-time compiled. Minutes, and the whole of what makes the page worth looking at.

    **Or the same page interpreted**, in seconds, which is what `--debug` is for: the load path is
    the one thing a Debug build reproduces exactly, and it is the one worth a short edit loop.

    ``named`` writes the native module's function names beside it (``--cpu``), which a deployed page has no use for.
    """
    step = ("build", "Debug") if debug else ("publish", "Release")
    print(f"=== {step[0]} ({step[1]}{'' if debug else ', AOT'}{', named' if named else ''})")
    extra = ["-p:WasmEmitSymbolMap=true"] if named else []
    result = _sim.dotnet(base, step[0], PROJECT, "-c", step[1], "-v", "q", "--nologo", *extra)
    log = _sim.write_log(base, "web", result.stdout + result.stderr)
    if result.code != 0:
        _sim.show(result.stdout + result.stderr, r"error|Error", limit=12)
        print(f"=== full output {_sim.rel(base, log)}")
        return False

    return True


def handler(bundle: Path):
    return partial(Quiet, directory=str(bundle), assets=mapped_assets(bundle.parent / ASSET_MAP))


def mapped_assets(manifest: Path) -> dict[str, Path]:
    """Every URL path the build's asset map names, as the file it is served from; nothing without a map."""
    if not manifest.is_file():
        return {}
    data = json.loads(manifest.read_text(encoding="utf-8"))
    roots, found = data["ContentRoots"], {}

    def walk(node: dict, prefix: str):
        for name, child in (node.get("Children") or {}).items():
            path = f"{prefix}{name}"
            if asset := child.get("Asset"):
                found[path] = Path(roots[asset["ContentRootIndex"]]) / asset["SubPath"]
            walk(child, f"{path}/")

    walk(data["Root"], "")
    return found


def serve_until_interrupted(bundle: Path, port: int) -> int:
    server = ThreadingHTTPServer(("127.0.0.1", port), handler(bundle))
    # Flushed, because this line is how anything else knows the page is up. Python block-buffers a
    # pipe, and a serve that never returns never flushes: redirected or run as an editor's background
    # task, the "serving" it is waited for is still sitting in the buffer when it is killed.
    print(f"serving {bundle} at http://localhost:{port}/ — ctrl-c to stop", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


def shoot(base: Path, bundle: Path | None, a) -> int:
    """
    Serve, open a browser at the page, wait in real seconds, photograph, and put it all away. With no
    bundle, the page is the one hosted at ``--url`` and nothing is served.
    """
    command = browser_command(a.browser)
    if command is None:
        cli.fail(
            f"no browser found: looked for {', '.join(BROWSERS)} on PATH. Install one with "
            f"`{INSTALL}`, or name one with --browser.")

    server = None
    if bundle is not None:
        server = ThreadingHTTPServer(("127.0.0.1", a.port), handler(bundle))
        threading.Thread(target=server.serve_forever, daemon=True).start()

    # A browser in a sandbox of its own — a flatpak one — cannot see a profile under this project,
    # so where it goes is an argument. **Kept between runs rather than thrown away**: a fresh profile
    # is a browser that has agreed to nothing, and it puts its welcome in front of the page every
    # time. It lives in .tmp/, which is wiped wholesale whenever anybody wants it gone.
    profile = Path(a.profile) if a.profile else base / ".tmp" / "web-profile"
    profile.mkdir(parents=True, exist_ok=True)
    # The query string is the command line (WEB-5), so the switches are spelled the way the desktop's are.
    asked = [f"map={a.map}"] if a.map else []
    if a.ui:
        asked.append(f"ui={a.ui}")
    if a.view:
        asked.append(f"view={a.view}")
    if a.at:
        asked.append("at=" + "%20".join(a.at.split(",")))
    if a.managed_world:
        asked.append("managed-world")
    query = "?" + "&".join(asked) if asked else ""
    browser = subprocess.Popen(
        [*command, *FLAGS, f"--user-data-dir={profile}", f"--remote-debugging-port={a.debug_port}",
         f"--window-size={a.width},{a.height}", "about:blank"],
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    try:
        page = wait_for_page(a.debug_port)
        out = Path(a.shot)
        out.parent.mkdir(parents=True, exist_ok=True)
        cpu = (Path(a.cpu), a.limit, bundle) if a.cpu else None
        address = a.url.rstrip("/") + "/" if a.url else f"http://localhost:{a.port}/"
        capture(page, address + query, out, a.seconds, a.click, a.drag, a.ask, a.device, cpu, a.hz)
        print(f"{out} written after {a.seconds:g} s on {a.map or 'the start menu over the idle ring'}")
        return 0
    finally:
        shut_down(a.debug_port, browser)
        if server is not None:
            server.shutdown()
            server.server_close()


def shut_down(port: int, launcher: subprocess.Popen):
    """
    Close the browser, and mean it. **Killing what was launched does not do it**: `flatpak run` hands
    the app off to the session and returns, so terminating that leaves a browser standing with the
    page still in it, and a tool run a few times leaves a row of them. `Browser.close` is asked of the
    browser itself, over the same protocol the page was driven with.
    """
    try:
        listed = json.loads(urllib.request.urlopen(f"http://127.0.0.1:{port}/json/version").read())
        sock = connect(listed["webSocketDebuggerUrl"])
        sock.settimeout(10.0)
        send(sock, {"id": 99, "method": "Browser.close"})
        try:
            receive(sock)
        except (socket.timeout, TimeoutError, EOFError):
            # It closing the connection on us is the browser doing as it was told.
            pass
        sock.close()
    except Exception as trouble:
        print(f"   the browser would not close over the protocol ({trouble}); killing what was launched")

    launcher.terminate()


def browser_command(named: str | None) -> list[str] | None:
    if named:
        return named.split()

    for browser in BROWSERS:
        found = shutil.which(browser)
        if found:
            return [found]

    return None


def wait_for_page(port: int, seconds: float = 30.0) -> dict:
    until = time.time() + seconds
    while time.time() < until:
        try:
            listed = json.loads(urllib.request.urlopen(f"http://127.0.0.1:{port}/json/list").read())
            page = next((p for p in listed if p["type"] == "page"), None)
            if page:
                return page
        except Exception:
            pass
        time.sleep(0.5)

    cli.fail(f"the browser never opened its debugging port {port}")


#: What the browser is asked, to find the rate it is actually painting at. The read-out in the picture
#: quotes the engine's own figure for the same thing, and the two are only worth one line each because
#: a page that draws every frame it is offered has them agree.
PAINT_RATE = """
new Promise(done => {
    const marks = [];
    const tick = at => {
        marks.push(at);
        if (marks.length <= 40) requestAnimationFrame(tick);
        else {
            const gaps = marks.slice(1).map((m, i) => m - marks[i]).sort((a, b) => a - b);
            done(1000 / gaps[gaps.length >> 1]);
        }
    };
    requestAnimationFrame(tick);
})
"""


def capture(page: dict, url: str, out: Path, seconds: float, click: str | None = None,
            pull: str | None = None, ask: str | None = None, device: str | None = None,
            cpu: tuple[Path, int, Path] | None = None, hz: float | None = None):
    """Navigate, let the town run for real seconds, then ask what it is painting at and photograph it."""
    sock = connect(page["webSocketDebuggerUrl"])
    send(sock, {"id": 1, "method": "Runtime.enable"})

    # Before the navigate, because what the page reads at start-up is the viewport and the pixel ratio
    # it was opened with (OBS-2k): a size imposed on a page already standing is a second layout.
    if device:
        pretend(sock, device)

    if hz:
        throttle(sock, hz)

    if cpu:
        sock.settimeout(30.0)
        for at, (method, params) in enumerate([("Profiler.enable", {}),
                                               ("Profiler.setSamplingInterval", {"interval": CPU_SAMPLE_US}),
                                               ("Profiler.start", {})]):
            send(sock, {"id": 60 + at, "method": method, "params": params})
            await_reply(sock, 60 + at)

    global navigated
    navigated = time.time()
    send(sock, {"id": 2, "method": "Page.navigate", "params": {"url": url}})

    # **A window that is not in front is not painted**, and a page that is not painted is a town that
    # never runs a frame: the animation callback simply stops, the read-out has nothing to measure, and
    # a click lands in an input queue nothing comes back to read. Asked for once here and again before
    # the click, because anything at all may have been raised over it in between.
    front(sock)

    if cpu:
        left = seconds - profile_the_open(sock, seconds, *cpu)
        settle(sock, max(0.0, left))
    else:
        settle(sock, seconds)

    if click:
        front(sock)
        press(sock, click)

    # After the click, so a gesture can be aimed at a panel the click opened.
    if pull:
        front(sock)
        drag(sock, pull)

    # **A window nobody can see is never asked to paint**, so this answers only if it is on top: the
    # promise waits on an animation callback that a hidden or fully covered window does not get. That
    # is worth saying rather than hanging on, because it is also the state in which the read-out in the
    # picture has nothing worth reading.
    sock.settimeout(15.0)
    send(sock, {"id": 3, "method": "Runtime.evaluate",
                "params": {"expression": PAINT_RATE, "awaitPromise": True, "returnByValue": True}})
    try:
        rate = await_reply(sock, 3).get("result", {}).get("result", {}).get("value")
    except (socket.timeout, TimeoutError):
        rate = None

    if isinstance(rate, (int, float)):
        print(f"   the browser is painting at {rate:.0f} Hz, which is what the read-out's fps is against")
    else:
        print("   the browser never painted a frame: the window is covered or off-screen, and the "
              "read-out in this picture is measuring a town nobody asked to draw")

    if ask:
        # Not the rate probe's fifteen seconds: an ask may await a promise that lets the town run on under it.
        sock.settimeout(120.0)
        send(sock, {"id": 5, "method": "Runtime.evaluate",
                    "params": {"expression": ask, "awaitPromise": True, "returnByValue": True}})
        answer = await_reply(sock, 5).get("result", {})
        print(f"   asked: {json.dumps(answer.get('result', {}).get('value', answer))}")

    # Its own timeout, and a long one: a Debug build paints a whole town into the canvas between the ask
    # and the answer, which is a minute's work on the machine and not a browser that has gone away. The
    # rate probe above deliberately gives up after fifteen seconds, and inheriting that here was a
    # picture lost to a slow frame rather than to anything wrong.
    sock.settimeout(120.0)
    send(sock, {"id": 4, "method": "Page.captureScreenshot"})
    try:
        shot = await_reply(sock, 4)
    except (socket.timeout, TimeoutError):
        raise SystemExit("the browser never answered the screenshot: it is painting too slowly to "
                         "photograph, so try again without --debug")

    if "result" not in shot:
        raise SystemExit(f"the browser refused the screenshot: {json.dumps(shot.get('error', shot))}")

    out.write_bytes(base64.b64decode(shot["result"]["data"]))


#: How often the page's CPU is sampled under --cpu, in microseconds: a traced city's open is a minute and a half, so
#: a millisecond is ninety thousand samples, which is plenty and a profile the protocol hands back in one piece.
CPU_SAMPLE_US = 1000

#: What the page says once its town has stood (src/app/main/web/Boot.cs), or that it never will.
STOOD = (" stood in ", "page threw")

#: How long `--cpu` waits for the town to stand, whatever `--seconds` says: the run after it is `--seconds`, and a traced
#: city's open on the page is minutes.
OPEN_BOUND_S = 900.0


def profile_the_open(sock: socket.socket, seconds: float, out: Path, limit: int, bundle: Path) -> float:
    """
    The page's CPU from the navigate until the town has stood, written to ``out`` by function, self and inclusive.
    Answers the seconds it took, so the caller lets the town run for what is left of ``seconds``.
    """
    sock.settimeout(1.0)
    begun = time.time()
    stood = False
    while not stood and time.time() < begun + max(seconds, OPEN_BOUND_S):
        try:
            message = receive(sock)
        except (socket.timeout, TimeoutError):
            continue

        report(message)
        stood = any(word in said for said in console_text(message) for word in STOOD) \
            or message.get("method") == "Runtime.exceptionThrown"

    took = time.time() - begun
    sock.settimeout(120.0)
    send(sock, {"id": 63, "method": "Profiler.stop"})
    profile = await_reply(sock, 63).get("result", {}).get("profile")
    if not profile:
        print("   the browser handed back no profile")
        return took

    out.parent.mkdir(parents=True, exist_ok=True)
    out.with_suffix(".cpuprofile").write_text(json.dumps(profile))
    out.write_text(cpu_tables(profile, limit, symbols(bundle), took, stood))
    print(f"   the page's CPU over its open written to {out}, and the profile itself beside it "
          f"(DevTools opens a .cpuprofile; --cpu-read summarises one again)")
    return took


def console_text(message: dict) -> list[str]:
    if message.get("method") != "Runtime.consoleAPICalled":
        return []
    return [str(argument["value"]) for argument in message["params"].get("args", []) if "value" in argument]


def symbols(bundle: Path) -> dict[int, str]:
    """
    The native module's function names by index, where the publish wrote them (WasmEmitSymbolMap): a stripped module
    names its functions by number alone. The map stays in the intermediate tree and is never published.
    """
    names = {}
    base = _sim.root()
    for found in [*(bundle / "_framework").glob("*.symbols"), *(base / "obj" / "web").glob("*/net11.0/wasm/for-publish/*.symbols")]:
        for line in found.read_text(errors="replace").splitlines():
            index, _, name = line.partition(":")
            if index.isdigit():
                names[int(index)] = name
    return names


#: What an ahead-of-time compiled managed method's native name begins with: the assembly's, the framework's, or a
#: generic instance's.
MANAGED = ("traffic_dotnet_web_", "corlib_", "aot_instances_", "System_")


def cpu_tables(profile: dict, limit: int, names: dict[int, str], took: float, stood: bool,
               focus: str | None = None, managed: bool = False) -> str:
    """Self and inclusive sample shares by function, off a DevTools CPU profile."""
    import re
    from collections import Counter

    nodes = {node["id"]: node for node in profile["nodes"]}
    parent = {child: node["id"] for node in profile["nodes"] for child in node.get("children", [])}

    def name(node: dict) -> str:
        frame = node["callFrame"]
        called = frame.get("functionName") or ""
        numbered = re.fullmatch(r"(?:wasm-function\[|\$func)(\d+)\]?", called)
        if numbered and int(numbered.group(1)) in names:
            return names[int(numbered.group(1))]
        if called:
            return called
        return f"({frame.get('url', '').rsplit('/', 1)[-1] or 'program'})"

    hits = Counter(profile.get("samples", []))
    total = sum(hits.values()) or 1
    own, under = Counter(), Counter()
    for node_id, count in hits.items():
        own[name(nodes[node_id])] += count
        seen = set()
        at = node_id
        while at is not None:
            called = name(nodes[at])
            if called not in seen:
                seen.add(called)
                under[called] += count
            at = parent.get(at)

    def table(counts: Counter) -> list[str]:
        return ["| % | ms | function |", "|---:|---:|---|"] + [
            f"| {100 * count / total:.2f} | {count * CPU_SAMPLE_US / 1000:.0f} | `{called[:160]}` |"
            for called, count in counts.most_common(limit)]

    # The nearest caller that is not the focus itself, of every sample the focus was on the stack of; and
    # everything under the focus's innermost frame in those samples, each function once a sample — where
    # the focus's time goes, which the inclusive table of the whole run cannot say.
    callers, callees = Counter(), Counter()
    if focus:
        for node_id, count in hits.items():
            below = []
            at = node_id
            while at is not None and focus not in name(nodes[at]):
                below.append(name(nodes[at]))
                at = parent.get(at)
            if at is None:
                continue
            for called in set(below):
                callees[called] += count
            while at is not None and focus in name(nodes[at]):
                at = parent.get(at)
            while managed and at is not None and not name(nodes[at]).startswith(MANAGED):
                at = parent.get(at)
            if at is not None:
                callers[name(nodes[at])] += count

    ending = "until the town said it had stood" if stood else "and the town never said it had stood"
    focused = [f"## What calls `{focus}`", "", *table(callers), "",
               f"## What `{focus}` spends its time in", "", *table(callees), ""] if focus else []
    return "\n".join([
        "# The page's CPU over its open", "",
        f"{total} samples a {CPU_SAMPLE_US} µs apart, {took:.1f} s from the navigate {ending}.", "",
        *focused, "## Self", "", *table(own), "", "## Inclusive", "", *table(under), ""])


def front(sock: socket.socket):
    """Raise the window, so the browser goes on being asked to paint it."""
    sock.settimeout(30.0)
    send(sock, {"id": 89, "method": "Page.bringToFront"})
    try:
        await_reply(sock, 89)
    except (socket.timeout, TimeoutError):
        pass


def press(sock: socket.socket, point: str):
    """
    A click at a point of the page, in interface pixels. **The only way to reach this interface from a
    script**: the menu, the panels and the tabs are drawn into the canvas, so there is no element to
    find and nothing but a real click at a real place will do. What the picture shows is where to aim.
    """
    # Its own, rather than the one-second poll the console loop left behind: an acknowledgement that
    # takes longer than that is a busy browser, not a lost one.
    sock.settimeout(30.0)

    where = at(point)

    # The page listens on `pointerdown`, and a mouse event only becomes a well-formed pointer event
    # with the button state on it — `buttons` is the mask of what is held, not a repeat of `button`.
    # The move first, because a pointer that has never been anywhere has not entered the canvas.
    for event in (
        {"type": "mouseMoved", "button": "none", "buttons": 0},
        {"type": "mousePressed", "button": "left", "buttons": 1, "clickCount": 1},
        {"type": "mouseReleased", "button": "left", "buttons": 0, "clickCount": 1},
    ):
        send(sock, {"id": 90, "method": "Input.dispatchMouseEvent", "params": {**where, **event}})
        await_reply(sock, 90)

    print(f"   clicked {where['x']:.0f},{where['y']:.0f}")
    settle(sock, 4.0)


#: How many moves a drag is drawn in. **The gesture is read as the difference between two frames**
#: (src/app/hud/Menu.cs, src/app/playercontrol/TouchGesture.cs), so a finger teleported from one end to
#: the other in a single move is one enormous frame of travel rather than the stroke somebody made.
DRAG_STEPS = 16


def drag(sock: socket.socket, path: str):
    """
    A finger drawn from one point of the page to another, both in interface pixels. **The one gesture a
    click cannot stand in for**: a handset has no wheel, so a panel longer than the window is scrolled by
    dragging it (CTL-9), and nothing else here can ask for that.
    """
    sock.settimeout(30.0)

    # Chromium refuses a touch on a device that has not got one, so the window is told it has.
    send(sock, {"id": 91, "method": "Emulation.setTouchEmulationEnabled",
                "params": {"enabled": True, "maxTouchPoints": 2}})
    await_reply(sock, 91)

    start, _, end = path.partition(":")
    fromPx, toPx = at(start), at(end)
    for step in range(DRAG_STEPS + 1):
        along = step / DRAG_STEPS
        touch = {"x": fromPx["x"] + (toPx["x"] - fromPx["x"]) * along,
                 "y": fromPx["y"] + (toPx["y"] - fromPx["y"]) * along}

        send(sock, {"id": 92, "method": "Input.dispatchTouchEvent", "params": {
            "type": "touchStart" if step == 0 else "touchMove", "touchPoints": [{**touch, "id": 0}]}})
        await_reply(sock, 92)

        # A frame between one position and the next, or the whole stroke lands inside one and the page
        # reads a single jump.
        time.sleep(1.0 / 60.0)

    send(sock, {"id": 93, "method": "Input.dispatchTouchEvent",
                "params": {"type": "touchEnd", "touchPoints": []}})
    await_reply(sock, 93)

    print(f"   dragged {fromPx['x']:.0f},{fromPx['y']:.0f} to {toPx['x']:.0f},{toPx['y']:.0f}")
    settle(sock, 2.0)


def at(point: str) -> dict:
    x, _, y = point.partition(",")
    return {"x": float(x), "y": float(y)}


def pretend(sock: socket.socket, device: str):
    """
    A viewport and a pixel ratio of somebody else's, as `W,H,DPR`. **A handset is not a small window**
    (OBS-2k): what makes one is three device pixels to the point, and a desktop browser sized down to a
    phone's width reports its own ratio and hides the very thing the interface is laid out against.
    """
    sock.settimeout(30.0)
    said = device.split(",")
    width, height = int(said[0]), int(said[1])
    ratio = float(said[2]) if len(said) > 2 else HandsetRatio

    send(sock, {"id": 94, "method": "Emulation.setDeviceMetricsOverride", "params": {
        "width": width, "height": height, "deviceScaleFactor": ratio, "mobile": True}})
    await_reply(sock, 94)
    print(f"   pretending to be {width}x{height} points at {ratio:g}x")


#: What a handset reports when the ratio is not named, since every one of them reports about this.
HandsetRatio = 3.0


#: The animation callback held to a rate of its own, as a slow display or a throttled tab would offer it. **Frames are
#: let through, not callbacks**: the page has several loops asking at once, and every one of them asking in a frame let
#: through runs in it, as a slow display would run them — a gate on callbacks shares the rate out between the loops and
#: stalls an open that waits on a painted frame.
THROTTLE = """
(() => {
    const paint = window.requestAnimationFrame.bind(window);
    const gapMs = 1000 / %s - 2;
    let shown = -Infinity;
    window.requestAnimationFrame = then => paint(function wait(at) {
        if (at !== shown) {
            if (at - shown < gapMs) return paint(wait);
            shown = at;
        }
        then(at);
    });
})();
"""


def throttle(sock: socket.socket, hz: float):
    """Every document this page opens asks for its frames at ``hz`` at most (SIM-9: the town keeps real time below it)."""
    sock.settimeout(30.0)
    # Registered on a page whose domain is off, the script is taken and never run.
    send(sock, {"id": 96, "method": "Page.enable"})
    await_reply(sock, 96)
    send(sock, {"id": 95, "method": "Page.addScriptToEvaluateOnNewDocument", "params": {"source": THROTTLE % hz}})
    await_reply(sock, 95)
    print(f"   the page's animation callback held to {hz:g} Hz")


def settle(sock: socket.socket, seconds: float):
    """
    Let the frames that answer a gesture be drawn, **listening the whole time**: what the page says about
    itself just after being touched is the most interesting thing it ever says, and a blind sleep here
    throws exactly that away.
    """
    sock.settimeout(1.0)
    until = time.time() + seconds
    while time.time() < until:
        try:
            report(receive(sock))
        except (socket.timeout, TimeoutError):
            continue


def await_reply(sock: socket.socket, wanted: int) -> dict:
    """The answer to one request, saying whatever the page says on the way."""
    while True:
        message = receive(sock)
        if message.get("id") == wanted:
            return message
        report(message)


#: When the page was told to navigate, so every line it says carries how far into the load it was said.
#: A stage that names itself (src/app/main/web/Data.cs) plus the second it was named in is the whole of
#: what a slow boot has to be read from: the gap between two lines is what that stage cost.
navigated = 0.0


def since() -> str:
    return f"{time.time() - navigated:5.1f}s" if navigated else "     "


def report(message: dict):
    """What the page says about itself, which is the whole of what a failed shot has to go on."""
    method = message.get("method")
    if method == "Runtime.consoleAPICalled":
        for argument in message["params"].get("args", []):
            if "value" in argument:
                print(f"   {since()} page:", argument["value"], flush=True)
    elif method == "Runtime.exceptionThrown":
        detail = message["params"]["exceptionDetails"]
        thrown = detail.get("exception", {})
        print(f"   {since()} page threw:", detail.get("text"), thrown.get("description", ""), flush=True)
    elif method == "Inspector.targetCrashed":
        print(f"   {since()} the page crashed", flush=True)


def connect(url: str) -> socket.socket:
    """
    A WebSocket, by hand. The protocol is a handshake and a masked frame header, and a dependency
    for those two is a dependency this project's tools do not have.
    """
    hostport, path = url.split("://", 1)[1].split("/", 1)
    host, port = hostport.split(":")
    sock = socket.create_connection((host, int(port)))
    key = base64.b64encode(os.urandom(16)).decode()
    sock.sendall((
        f"GET /{path} HTTP/1.1\r\nHost: {hostport}\r\nUpgrade: websocket\r\n"
        f"Connection: Upgrade\r\nSec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n"
    ).encode())
    head = b""
    while b"\r\n\r\n" not in head:
        head += sock.recv(1)
    return sock


def send(sock: socket.socket, message: dict):
    payload = json.dumps(message).encode()
    header = bytearray([0x81])
    length = len(payload)
    if length < 126:
        header.append(0x80 | length)
    elif length < (1 << 16):
        header.append(0x80 | 126)
        header += struct.pack(">H", length)
    else:
        header.append(0x80 | 127)
        header += struct.pack(">Q", length)

    mask = os.urandom(4)
    header += mask
    sock.sendall(bytes(header) + bytes(b ^ mask[i % 4] for i, b in enumerate(payload)))


def receive(sock: socket.socket) -> dict:
    while True:
        first, second = exactly(sock, 2)
        length = second & 0x7F
        if length == 126:
            length = struct.unpack(">H", exactly(sock, 2))[0]
        elif length == 127:
            length = struct.unpack(">Q", exactly(sock, 8))[0]

        payload = exactly(sock, length)
        if first & 0x0F == 1:
            return json.loads(payload)


def exactly(sock: socket.socket, count: int) -> bytes:
    buffer = b""
    while len(buffer) < count:
        chunk = sock.recv(count - len(buffer))
        if not chunk:
            raise EOFError("the browser closed its debugging connection")
        buffer += chunk

    return buffer


class Quiet(SimpleHTTPRequestHandler):
    """
    A static server that does not narrate three hundred and nineteen fetches, and that hands out the
    brotli the publish already wrote.

    **The compressed copies are the whole distribution.** `dotnet.native.wasm` is 12.4 MB raw and
    2.6 MB brotli and the publish emits both, so a server that answers with the raw one is a page
    that reads 19.6 MB to say what 6.5 MB says — which is what this served until it did this, and
    what made WEB-6's figures true of a host nobody had written. The browser unwraps
    `Content-Encoding` before the fetch resolves, so nothing above knows: it is the same arrangement
    `_framework` was built against and the same one any host serving this folder would make.

    **The art archive is not this**, and must not become it. `assets.tar.gz` is unpacked by the page out
    of the bytes it was handed; marking it `Content-Encoding: gzip` would have the browser unwrap it
    first and leave the page untarring something already inflated. Only the framework's `.br` companions
    are served this way, and only because nothing in the page ever sees them.

    **And it caches nothing**, which is the one way this server is deliberately not a host. Every
    assembly is laid down under a hash of its contents and `dotnet.js` names them, so a browser
    holding the last build's `dotnet.js` asks for a module this one never wrote and the page dies on
    a 404 for `dotnet.native.<hash>.js`. The profile is kept between runs (see :func:`shoot`), so
    without this that is every rebuild.
    """

    def __init__(self, *args, assets: dict[str, Path], **kwargs):
        # Set before the base constructor, which handles the request.
        self.assets = assets
        super().__init__(*args, **kwargs)

    def translate_path(self, path: str) -> str:
        mapped = self.assets.get(urllib.parse.unquote(path.split("?", 1)[0].split("#", 1)[0]).lstrip("/"))
        return str(mapped) if mapped else super().translate_path(path)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def send_head(self):
        squeezed = self.squeezed()
        if squeezed is None:
            return super().send_head()

        stream = squeezed.open("rb")
        stat = os.fstat(stream.fileno())
        self.send_response(200)
        # The type is the uncompressed file's — application/wasm, not whatever a .br is called.
        self.send_header("Content-type", self.guess_type(str(squeezed)[:-len(COMPRESSED)]))
        self.send_header("Content-Encoding", "br")
        self.send_header("Content-Length", str(stat.st_size))
        self.send_header("Last-Modified", self.date_time_string(stat.st_mtime))
        self.end_headers()
        return stream

    def squeezed(self) -> Path | None:
        """The brotli copy beside what was asked for, if there is one and the browser will take it."""
        offered = self.headers.get("Accept-Encoding", "")
        if "br" not in {token.split(";")[0].strip() for token in offered.split(",")}:
            return None

        beside = Path(self.translate_path(self.path) + COMPRESSED)
        return beside if beside.is_file() else None

    def log_message(self, *args):
        pass


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq web",
        description="Publish the browser head, serve it, and take a picture of it running.",
        allow_abbrev=False,
    )
    parser.add_argument("--shot", metavar="FILE", help="write a picture of the running page here")
    parser.add_argument("--serve", action="store_true", help="serve the published page until interrupted")
    parser.add_argument(
        "--map", metavar="NAME", default="",
        help="the map to open (default: none, which is the page's own start menu over the idle ring)")
    parser.add_argument("--ui", metavar="SWITCHES", help="the debug layers to open, as --ui does (frame, nodes, claims…)")
    parser.add_argument("--view", type=float, metavar="METRES", help="open on this span, as --view does")
    parser.add_argument("--at", metavar="X,Y", help="open looking here, in the town's metres, as a shot's --at does "
                                                    "(`qq meta --at LAT,LON` says where a place stands)")
    parser.add_argument("--managed-world", action="store_true",
                        help="step the world in managed code rather than on the page's device, as --managed-world does")
    parser.add_argument("--click", metavar="X,Y", help="click here before the picture, in interface pixels")
    parser.add_argument("--drag", metavar="X,Y:X,Y",
                        help="draw a finger from one point to the other, in interface pixels: the touch "
                             "gestures, which a click cannot stand in for")
    parser.add_argument("--device", metavar="W,H[,DPR]",
                        help="pretend to be a handset of this many points at this pixel ratio (default 3)")
    parser.add_argument("--ask", metavar="JS", help="evaluate this in the page and print what it answers")
    parser.add_argument("--hz", type=float, metavar="RATE",
                        help="hold the page's animation callback to this rate, as a slow display or a throttled tab")
    parser.add_argument("--cpu", metavar="FILE",
                        help="sample the page's CPU from the navigate until the town says it has stood, and write "
                             "where it went here (markdown): self and inclusive time by function, top --limit rows")
    parser.add_argument("--limit", type=int, default=40, help="rows of each --cpu table")
    parser.add_argument("--focus", metavar="TEXT",
                        help="of --cpu or --cpu-read, also what calls the functions whose names hold this, by caller")
    parser.add_argument("--managed", action="store_true",
                        help="of --focus, the nearest caller that is a compiled managed method, past the interpreter's "
                             "and the jiterpreter's own frames, which name nothing")
    parser.add_argument("--cpu-read", metavar="PROFILE",
                        help="summarise a .cpuprofile an earlier --cpu kept, into --cpu's file or beside it, and stop")
    parser.add_argument("--seconds", type=float, default=45.0, help="how long to let it run before the picture")
    parser.add_argument("--port", type=int, default=8730, help="the port to serve on")
    parser.add_argument("--debug-port", type=int, default=9230, help="the browser's DevTools port")
    parser.add_argument("--width", type=int, default=1280)
    parser.add_argument("--height", type=int, default=800)
    parser.add_argument("--browser", metavar="CMD", help="the browser to drive, if it is not on PATH")
    parser.add_argument("--profile", metavar="DIR", help="where the browser keeps its throwaway profile")
    parser.add_argument("--debug", action="store_true",
                        help="build and serve the Debug page instead of publishing: seconds rather than "
                             "ten minutes, and an interpreted town that draws at a tenth of the pace")
    parser.add_argument("--no-build", action="store_true", help="use the page already built or published")
    parser.add_argument("--url", metavar="URL",
                        help="of --shot, drive a page hosted there (the deployed one) rather than serving the local build")
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)
