"""
Hand one car of a running town to a model and let it drive, a frame and a burst of pedals at a time.

**The driving is the engine's second seat** (`--bot`, src/app/drive/, DRV-8): the model holds one named
car through the very seam a keyboard's keys go through, and everything it is told is a picture of the town
with no interface in it plus that car's own dashboard. Whoever is watching keeps the window — the map, the
camera, the panels and every other car stay theirs (CTL-5d).

This is the harness and not the driver: it spawns the run, waits for each frame, asks the model what to
hold, checks the answer is something a key could have done, and appends it. A line it cannot read is
refused and said back to the model rather than driven.

**The town stands still while the model is thinking** (`--freeze=on`, `--bot-waits`), so it drives at the
town's own pace without ever being hurried, and the frame it answers is still true when the answer lands.
**What it is told about the car is the engine's own handbook**, written beside the frames when the seat is
taken — this file quotes no figure of its own.

The key is OpenRouter's, read from `OPENROUTER_API_KEY` or `~/.qq/openrouter.key`; nothing is written into
the project.
"""

import argparse
import base64
import json
import math
import os
import re
import signal
import time
import urllib.error
import urllib.request
from pathlib import Path

import _sim
from common import cli, paths, proc

#: A whole drive: the run is minutes long and the tool waits for it.
TIMEOUT_S = 3600

ENDPOINT = "https://openrouter.ai/api/v1/chat/completions"

#: Where the key is looked for, in order. The project holds none.
KEY_FILES = ("~/.qq/openrouter.key", "~/.config/openrouter.key")

ENGINE = "traffic-dotnet.csproj"

#: Where this tool keeps a run: the steps it appends, the frames the eye writes, and the transcript.
WORK = ".tmp/pilot"

MAP_NAME = re.compile(r"^[A-Za-z0-9_-]{1,32}$")

#: The dashboard's own body line, which is the whole of what this tool reads off a reading.
AT = re.compile(r"at (-?[\d.]+),(-?[\d.]+), ([\d.]+) km/h")

#: What a model may say. The harness takes the pictures itself, so a driver only ever holds the controls.
STEP = re.compile(
    r"^(?:drive\s+\d+(?:\.\d+)?(?:\s+(?:throttle|brake|steer)=-?\d+(?:\.\d+)?|\s+handbrake=(?:on|off))+"
    r"|coast\s+\d+(?:\.\d+)?)$"
)

#: The longest a single burst may hold the pedals, in seconds of the town. A driver that cannot see for
#: four seconds is a driver in a hedge.
MOST_SECONDS = 4.0

#: How many times one turn is asked for before the car is left to coast through it.
TRIES = 3

#: What the engine writes beside the frames when the seat is taken: this car's own figures (DRV-8). The
#: harness never spells those out itself — a briefing that quotes a car retuned last week is a lie.
HANDBOOK = "handbook.md"

SYSTEM = """You are driving one car in a top-down town. You hold its pedals and its wheel yourself, in
short bursts, and you get a fresh picture after every burst.

WHAT YOU SEE
One frame of the town from above with no interface drawn in it, and your own dashboard beside it. YOUR
CAR IS EXACTLY AT THE CENTRE OF THE FRAME. Dark grey is road, light grey is pavement and kerb, green is
grass, textured blocks are buildings, and the other cars are the small bright shapes on the road.

YOUR DASHBOARD
`hand` is what you are holding this moment, `at X,Y` where you are in metres, `km/h` your speed,
`facing D` your heading in degrees, `state` whether the car is still in one piece.

WHAT YOU MAY SAY
One to three lines, nothing else. No prose, no explanation, no code fences.
  drive SECONDS throttle=N   hold the accelerator, N from 0 to 1
  drive SECONDS brake=N      hold the brake, N from 0 to 1
  drive SECONDS steer=N      turn the wheel, N from -1 (full left) to 1 (full right)
  drive SECONDS handbrake=on pull the handbrake as well
  coast SECONDS              lift off the pedals and roll
Pedals and wheel combine on one line: `drive 2 throttle=0.4 steer=-0.3`. Throttle and brake cannot both
be held. SECONDS is at most {most}, and one or two is usually right. Your lines are driven in the order
you wrote them, and then you see again.

THE WHEEL STAYS WHERE YOU PUT IT. A line that does not say `steer` leaves it exactly where your last
one left it, so say `steer=0` when you want to straighten up and leave it out when you are happy with
it. Your dashboard's `hand` line says where it is standing.{waiting}

HOW TO DRIVE IT
The handbook below is this car's own figures — read it rather than guessing. Keep to the right-hand side
of the road. Brake before a turn and not in it: steer at speed and the car understeers straight on.
If the dashboard says you are barely moving while a pedal is down, you are against something: let the
throttle go and hold the brake past a standstill to back away from it."""

#: What is true only where the run was asked to stop its clock for the driver (`--bot-waits`, DRV-8).
WAITING = """
THE TOWN STANDS STILL WHILE YOU THINK. Nothing moves between your seeing a frame and your answer being
driven, so a short burst costs you nothing and a long one is driving with your eyes shut."""


def main():
    a = parse_args()
    base = _sim.root()
    key = read_key()

    work = base / WORK
    eye = work / "eye"
    steps = work / "steps.txt"
    eye.mkdir(parents=True, exist_ok=True)

    # A run of its own starts with an empty folder and an empty tape. **An attached one appends to both**:
    # the engine follows the steps file by how far into it it has read, so rewriting the file under a
    # running town is the one thing that breaks the tail.
    if not a.attach:
        for stale in list(eye.glob("*.png")) + list(eye.glob("*.txt")):
            stale.unlink()
        # The handbook goes with them, so what is read back is the one this run's own town wrote.
        (eye / HANDBOOK).unlink(missing_ok=True)

    if not a.attach and not a.no_build:
        built = _sim.dotnet(base, "build", ENGINE, "-c", "Release", "-v", "q", "--nologo")
        if built.code != 0:
            print((built.stdout + built.stderr).strip())
            return cli.fail("the build failed, so nobody drove")

    first = taken(eye)
    with steps.open("a" if a.attach else "w", encoding="utf-8") as tape:
        tape.write(preamble(a, first))

    pid = None if a.attach else spawn_run(base, a, steps, eye)
    print(f"{'attached to' if a.attach else 'opened'} {a.map}, car {a.car} is the model's; steps in {WORK}")

    # **The driver is briefed by the engine and not by this file** (DRV-8): the handbook is written where
    # the frames go the moment the seat is taken, so nothing here quotes a figure the town does not have.
    book = handbook(eye, a.wait)
    if book is None:
        if pid is not None and not a.keep:
            stop(pid)
        return cli.fail(f"the run wrote no {WORK}/eye/{HANDBOOK}, so nobody could be told what they are driving")

    briefing = SYSTEM.format(most=MOST_SECONDS, waiting=WAITING if a.freeze == "on" else "") + "\n\n" + book
    transcript = [f"# {a.model} driving car {a.car} of {a.map}\n"]
    try:
        drive(a, key, eye, steps, transcript, first, briefing)
    except KeyboardInterrupt:
        print("stopped")
    finally:
        (work / "pilot.md").write_text("\n".join(transcript), encoding="utf-8")
        if pid is not None and not a.keep:
            stop(pid)
        print(f"the whole run is in {WORK}/pilot.md, the frames in {WORK}/eye")

    return 0


def drive(a, key, eye: Path, steps: Path, transcript: list[str], shot: int, briefing: str):
    """One frame, one answer, one burst of steps — until the minutes are up or the eye stops answering."""
    deadline = time.monotonic() + (a.minutes * 60)
    history = []
    before = None

    frame = wait_for(eye, shot, a.wait)
    while time.monotonic() < deadline:
        if frame is None:
            print("the eye stopped answering: is the window still open?")
            return

        dash = frame.with_suffix(".txt").read_text(encoding="utf-8").strip()
        if "wrecked" in dash:
            print(f"{shot:3}  the car is wrecked, and a wreck takes no hand (CTL-5): the drive is over")
            transcript.append(f"\n## {shot} — {frame.name}\n\n```\n{dash}\n```\n\nwrecked, so the drive ended here.")
            return

        # What the last burst came to, which is the one reading the dashboard does not carry: a driver told
        # only where it is cannot tell a car that crept from a car that is wedged against a kerb.
        note = moved(before, dash)
        before = dash

        thinking = time.monotonic()
        said = ask(key, a, frame, dash, note, briefing, history)
        thought = time.monotonic() - thinking
        lines, refused = read_answer(said)
        transcript.append(
            f"\n## {shot} — {frame.name} ({thought:.1f}s)\n\n```\n{dash}\n{note}\n```\n\n```\n{said.strip()}\n```")
        if refused:
            transcript.append(f"\nrefused: {'; '.join(refused)}")

        held = " | ".join(lines) if lines else "nothing it could hold"
        print(f"{shot:3}  {dash.splitlines()[1][11:]:<44} {thought:4.1f}s  {held}")
        history.append({"frame": frame, "dash": dash, "note": note, "said": said, "refused": refused})
        history[:] = history[-a.memory:]

        shot += 1
        with steps.open("a", encoding="utf-8") as tape:
            for line in lines:
                tape.write(line + "\n")
            tape.write(f"shot s{shot:03}\n")

        frame = wait_for(eye, shot, a.wait)


def ask(key, a, frame: Path, dash: str, note: str, briefing: str, history) -> str:
    """
    One turn of the model: **the last few turns as they happened** — each frame with the dashboard it
    carried and what was held after it — and then this one. A driver given only the picture in front of it
    cannot tell which way it came into a junction or that the street it is on has been bending.
    """
    messages = [{"role": "system", "content": briefing}]
    for older in history[-a.memory:]:
        messages.append({"role": "user", "content": seen(older["frame"], older["dash"], older["note"])})
        messages.append({"role": "assistant", "content": older["said"]})
        if older["refused"]:
            messages.append({"role": "user", "content": "refused: " + "; ".join(older["refused"])})

    messages.append({"role": "user", "content": seen(frame, dash, note, "\n\nWhat do you hold now?")})

    # The budget covers the thinking as well as the answer — these models will not turn reasoning off, and
    # a turn that spends its tokens thinking comes back as the tail of a sentence, which is what the first
    # run did. Low effort and a generous ceiling: what is wanted is three short lines, quickly.
    body = json.dumps({
        "model": a.model,
        "messages": messages,
        "max_tokens": 1200,
        "reasoning": {"effort": "low", "exclude": True},
    }).encode("utf-8")
    request = urllib.request.Request(
        ENDPOINT, data=body, method="POST",
        headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"},
    )
    # A turn that does not come back is a turn the car coasts through, not a run to tear down: the town
    # is still going and the next frame is seconds away.
    for attempt in range(TRIES):
        try:
            with urllib.request.urlopen(request, timeout=60) as answer:
                said = json.load(answer)
            if "choices" not in said:
                raise ValueError(f"no choices: {json.dumps(said)[:200]}")
            return said["choices"][0]["message"]["content"] or ""
        except (urllib.error.URLError, ValueError, KeyError) as trouble:
            why = getattr(trouble, "reason", trouble)
            if isinstance(trouble, urllib.error.HTTPError):
                why = f"{trouble.code} {trouble.read().decode('utf-8', 'replace')[:200]}"
            print(f"     the model did not answer ({why})")
            if attempt + 1 < TRIES:
                time.sleep(2)

    return ""


def read_answer(said: str):
    """
    The lines of an answer that are steps, and what was thrown away. **Checked here as well as in the
    engine**: a line the engine refuses costs a round trip of the town, and the model is told what it
    said wrong on the next turn either way.
    """
    lines, refused = [], []
    for raw in said.splitlines():
        line = raw.strip().strip("`").strip()
        if not line:
            continue
        if not STEP.match(line):
            refused.append(f"{line!r} is not a step")
            continue
        seconds = float(line.split()[1])
        if seconds <= 0 or seconds > MOST_SECONDS:
            refused.append(f"{line!r} holds for {seconds}s, and a burst is 0 to {MOST_SECONDS}s")
            continue
        lines.append(line)

    return lines[:3], refused


def seen(frame: Path, dash: str, note: str, asking: str = "") -> list[dict]:
    """One turn's worth of what the driver was looking at: its frame, its dashboard and what had changed."""
    return [
        {"type": "text", "text": f"{dash}\n{note}{asking}"},
        {"type": "image_url", "image_url": {"url": data_url(frame)}},
    ]


def moved(before: str | None, dash: str) -> str:
    """
    **What the burst that just ended came to**, in one line. The dashboard is a snapshot of the car and
    says nothing about how it got there, so a driver reading one cannot tell a car that crept forward from
    a car wedged against a kerb with the throttle down. Both readings are ones the harness was already
    given: this briefs the driver, it does not measure the town.
    """
    now = AT.search(dash)
    was = AT.search(before) if before else None
    if now is None or was is None:
        return "this is your first look at it"

    wentM = math.dist((float(was[1]), float(was[2])), (float(now[1]), float(now[2])))
    line = f"since the frame before this one the car has moved {wentM:.1f} m"
    if wentM < 0.5 and "throttle" in dash.splitlines()[0]:
        return f"{line} — a pedal was down and it went nowhere, so something is holding the car"

    return line


def handbook(eye: Path, waitS: float):
    """
    The car's own figures, which the engine writes beside the frames when the seat is taken (DRV-8). It is
    also the first sign of life from the run, so waiting for it is waiting for the town to stand.
    """
    path = eye / HANDBOOK
    until = time.monotonic() + waitS
    while time.monotonic() < until:
        if path.is_file() and path.stat().st_size > 0:
            return path.read_text(encoding="utf-8").strip()
        time.sleep(0.2)

    return None


def taken(eye: Path) -> int:
    """
    The number the next frame is asked for by. Nought for a run of its own; past whatever is already
    there for an attached one, whose earlier frames belong to whoever was driving before.
    """
    numbers = [int(frame.stem[-3:]) for frame in eye.glob("*-s???.png") if frame.stem[-3:].isdigit()]
    return max(numbers) + 1 if numbers else 0


def wait_for(eye: Path, shot: int, waitS: float):
    """
    The frame a `shot s###` step will leave, once both it and its dashboard are on the disk. **Polled
    hard**: a driver's whole round trip is its thinking plus this, and the town is standing still through
    both where the run was asked to wait (`--freeze=on`).
    """
    name = f"s{shot:03}"
    until = time.monotonic() + waitS
    while time.monotonic() < until:
        for frame in eye.glob(f"*-{name}.png"):
            if frame.with_suffix(".txt").is_file() and frame.stat().st_size > 0:
                time.sleep(0.02)
                return frame
        time.sleep(0.03)

    return None


def preamble(a, first: int) -> str:
    """
    The harness's own first lines: the run keys a watched drive wants (DRV-7) and the first look. **The
    model never says these** — the pace and the town's own agents belong to whoever is watching, and this
    only sets them where a run starts.
    """
    lines = ["# Driven by a model through qq pilot. Appended to while the town runs."]
    if a.pace > 0:
        lines.append(f"pace {a.pace}")
    if a.agents != "keep":
        lines.append(f"agents {a.agents}")
    lines.append(f"view {a.view}")
    lines.append(f"shot s{first:03}")
    return "\n".join(lines) + "\n"


def spawn_run(base: Path, a, steps: Path, eye: Path) -> int:
    """The window, opened for somebody to watch. It outlives this tool, so it is killed on the way out."""
    argv = [
        "dotnet", "run", "--project", ENGINE, "-c", "Release", "--no-build", "--",
        "--map", a.map, "--windowed",
        # Real seconds, and the drive's deadline is real minutes: what the town spends is the drive's
        # business, and where it waits for its driver (`--bot-waits`) it spends much less than this.
        "--seconds", str(a.minutes * 60 + 300),
        "--bot", _sim.rel(base, steps),
        "--bot-car", str(a.car),
        "--bot-frames", _sim.rel(base, eye),
        "--bot-out", f"{WORK}/log.md",
        "--bot-eye", str(a.eye),
        "--bot-view", str(a.view),
    ]
    if a.freeze == "on":
        argv.append("--bot-waits")

    return proc.spawn(argv, log=base / WORK / "game.log", cwd=base)


def stop(pid: int):
    """The window shut the way a reader would shut it, and never harder than that."""
    try:
        os.kill(pid, signal.SIGTERM)
    except ProcessLookupError:
        pass


def data_url(frame: Path) -> str:
    return "data:image/png;base64," + base64.b64encode(frame.read_bytes()).decode("ascii")


def read_key() -> str:
    """The key, from the environment or from a file outside the project. It is never printed."""
    key = os.environ.get("OPENROUTER_API_KEY", "").strip()
    if key:
        return key

    for candidate in KEY_FILES:
        path = Path(candidate).expanduser()
        if path.is_file():
            key = path.read_text(encoding="utf-8").strip()
            if key:
                return key

    return cli.fail(
        "no OpenRouter key: set OPENROUTER_API_KEY or put it in " + paths.display(Path(KEY_FILES[0]).expanduser())
    )


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq pilot",
        description="Hand one car of a running town to a model and let it drive.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--map", default="Odesa", type=map_name,
                        help="the map to stand the town up from")
    parser.add_argument("--car", default=0, type=cli.counting, metavar="INT",
                        help="which car of the fleet the model drives")
    parser.add_argument("--model", default="google/gemini-3.8-flash",
                        choices=[
                            "google/gemini-3.8-flash",
                            "google/gemini-3.5-flash",
                            "google/gemini-3.5-flash-lite",
                            "google/gemini-2.5-flash",
                            "google/gemini-3.1-flash-lite",
                        ],
                        help="which model does the driving, through OpenRouter")
    parser.add_argument("--minutes", default=10, type=cli.counting, metavar="INT",
                        help="how long to drive for, in real minutes")
    parser.add_argument("--freeze", default="on", choices=["on", "off"],
                        help="stop the town's clock whenever the model is thinking, so it drives at full pace "
                             "without ever being hurried")
    parser.add_argument("--pace", default=1.0, type=float, choices=[0.0, 0.1, 0.2, 0.3, 0.5, 1.0],
                        help="how fast the town runs against real time while it is running (0 = leave it)")
    parser.add_argument("--agents", default="on", choices=["on", "off", "keep"],
                        help="whether the town's own traffic is deciding, or 'keep' to leave the run as it is")
    parser.add_argument("--view", default=45, type=cli.counting, metavar="INT",
                        help="how much town the model's eye spans, in whole metres")
    parser.add_argument("--eye", default=768, type=cli.counting, metavar="INT",
                        help="how wide the model's frames are, in pixels")
    parser.add_argument("--memory", default=5, type=cli.counting, metavar="INT",
                        help="how many earlier turns the model is shown again, each with its own frame")
    parser.add_argument("--wait", default=90.0, type=float, choices=[30.0, 60.0, 90.0, 180.0],
                        help="how long to wait for a frame before giving up on the run")
    parser.add_argument("--attach", action="store_true",
                        help="drive a run that is already open on .tmp/pilot/steps.txt instead of opening one")
    parser.add_argument("--keep", action="store_true",
                        help="leave the window open when the drive ends")
    parser.add_argument("--no-build", action="store_true",
                        help="drive with the engine already built")
    return parser.parse_args()


def map_name(value: str) -> str:
    if not MAP_NAME.match(value):
        raise argparse.ArgumentTypeError(f"{value!r} is not a map name")
    return value


if __name__ == "__main__":
    cli.entry(main)
