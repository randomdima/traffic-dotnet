using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.PlayerControl;
using TrafficSimulation.Bench;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Drive;

/// <summary>A frame a step asked for, handed to whoever in this run has a renderer.</summary>
internal readonly record struct DriveFrame(string Name, string Path, float ViewM, Vector2? AtM, string[] Ui);

/// <summary>
/// <b>DRV-1 — what a step does to a town, and nothing about when.</b> The pedals, the wheel and the
/// handbrake become the hand <see cref="TownWorld.Hands"/> is given every tick; a selection is the call a
/// click makes; an order is <see cref="PlayerHands.Order"/>; the lever is <see cref="TownWorld.WorkTheAction"/>.
/// <b>Nothing here writes into a body.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>One machine and two runs.</b> A headless drive plays a script into this
/// (<see cref="DriveRun"/>) and a windowed one follows a file into it while somebody watches
/// (<see cref="DriveTape"/>), so what a step <em>means</em> is stated once however the town is being
/// looked at.
/// </para>
/// <para>
/// <b>A frame is asked for rather than taken</b> (<see cref="Photograph"/>): offscreen the shot path draws
/// one, in a window the frame on the glass is read back, and a run with no renderer at all says so. What
/// the step says about where to look and how much to span is this class's either way.
/// </para>
/// </remarks>
internal sealed class DriveHands(
    TownWorld world, ScenarioWatch[] scenario, string map, string framesDir, float viewM, string[] ui)
    : IAtTheWheel
{
    readonly List<string> _told = [];

    /// <summary>What the drive is holding down, which the loop pushes through the seam every tick.</summary>
    public HandInput Hand { get; private set; }

    /// <summary>Where the camera is pinned, or nothing at all, which rides the unit under the hand.</summary>
    public Vector2? LookM { get; private set; }

    /// <summary>What a <c>shot</c> step is answered with, or nothing on a run that draws nothing.</summary>
    public Func<DriveFrame, string>? Photograph { get; init; }

    /// <summary>
    /// <b>The player's <c>Pause</c> key</b>, which holds the town's own agents while their bodies go on
    /// stepping (<see cref="Hud.RunState.AgentsHeld"/>). A watched run opens with them held, so a drive
    /// that wants traffic round it presses the same key a reader would.
    /// </summary>
    public Action<bool>? Hold { get; init; }

    /// <summary>
    /// <b>The player's pace keys</b>, as a figure rather than as the three they offer. What it is for is
    /// the hand rather than the town: whoever is writing the steps takes seconds to decide the next one,
    /// and a town run at a fraction of real time spends a fraction of itself waiting for them. <b>Nothing
    /// about the drive changes with it</b> — a step still holds for the seconds of the town it names.
    /// </summary>
    public Action<float>? Pace { get; init; }

    /// <summary>
    /// <b>What a selection asks for outside the town</b>: the camera stood on the unit picked out
    /// (OBS-1a), which is what a click asks for and what a run being watched needs — a window still
    /// looking where it opened is a drive nobody can see.
    /// </summary>
    public Action? Picked { get; init; }

    /// <summary>How much town a frame spans, which the script may change as it goes.</summary>
    public float ViewM { get; private set; } = viewM;

    int _frames;

    /// <summary>How many cars a listing names. Enough to choose from and few enough to read.</summary>
    const int CarsListed = 10;

    string[] _ui = ui;

    /// <summary>
    /// One step, carried out — <b>everything about it but the ticks it asks for</b>, which are the run's to
    /// spend. A step this cannot carry out throws rather than being reported as having happened.
    /// </summary>
    /// <returns>Whatever the step itself had to say: a listing, a frame's path, an answer about a lever.</returns>
    public IReadOnlyList<string> Take(in DriveStep step)
    {
        _told.Clear();
        switch (step.Verb)
        {
            case DriveVerb.SelectCar:
                // Refused rather than left unpicked: the selection drops a number off the end of the fleet
                // on purpose (a click can catch a car that has gone), and a step naming one is a mistake
                // that would otherwise read as a car that would not drive.
                if (step.Car >= world.Cars.Count)
                {
                    throw new ArgumentException(
                        $"drive step at line {step.Line}: there is no car {step.Car} in {map}, " +
                        $"which stands {world.Cars.Count}.");
                }

                Pick(new Selection(SelectionKind.Car, step.Car));
                break;

            case DriveVerb.SelectAt:
                Pick(Under(step.PointM, step.Line));
                break;

            case DriveVerb.SelectNearest:
                // Picked where it stands rather than taken by its number, so what is selected is what a
                // click on that car would have selected — a body overlapping it is what a reader would
                // have caught too.
                Pick(Under(world.Cars.PositionM[NearestCar(step.PointM, step.Line)], step.Line));
                break;

            case DriveVerb.Cars:
                List(step.PointM);
                break;

            case DriveVerb.LookAt:
                LookM = step.PointM;
                break;

            case DriveVerb.LookAtCar:
                LookM = null;
                break;

            case DriveVerb.View:
                ViewM = step.ViewM;
                break;

            case DriveVerb.Ui:
                _ui = step.Ui ?? [];
                break;

            case DriveVerb.Drive:
                Selected(step.Line);
                Hand = step.Hand;
                break;

            case DriveVerb.Wait:
                // CTL-5b: letting the keys go coasts and does not hand the unit back, so there is no way for
                // a player to stop holding the wheel without the reset — and no way for a script either.
                if (world.HandsOn)
                {
                    throw new ArgumentException(
                        $"drive step at line {step.Line}: the wheel is held, and letting go of the keys coasts " +
                        "rather than handing the unit back (CTL-5b). Coast, or release the wheel first.");
                }

                Hand = HandInput.None;
                break;

            case DriveVerb.Release:
                world.ReleaseHands();
                Hand = HandInput.None;
                break;

            case DriveVerb.Order:
                Selected(step.Line);
                PlayerHands.Order(world, step.PointM);
                break;

            case DriveVerb.Action:
                Selected(step.Line);
                _told.Add(world.WorkTheAction() ? "the lever was worked" : "nothing picked out has a lever to work");
                break;

            case DriveVerb.Agents:
                if (Hold is not { } pause) throw new ArgumentException(
                    $"drive step at line {step.Line}: this run has no run keys to press.");

                pause(!step.On);
                _told.Add(step.On ? "the town's agents are deciding again" : "the town's agents are held");
                break;

            case DriveVerb.Pace:
                if (Pace is not { } pace) throw new ArgumentException(
                    $"drive step at line {step.Line}: this run has no clock to pace — it runs as fast as it can.");

                pace(step.Seconds);
                _told.Add($"the town runs at {step.Seconds:F2}x real time");
                break;

            case DriveVerb.Shot:
                _told.Add(Photograph is { } camera
                    ? camera(new DriveFrame(
                        step.Name!, Path.Combine(framesDir, $"{_frames++:D2}-{step.Name}.png"), ViewM, Where(), _ui))
                    : "nothing draws on this run, so there is no frame to take");
                break;
        }

        return _told;
    }

    /// <summary>
    /// <b>The hand a run that has run out of steps holds</b>: the wheel still taken and nothing pressed,
    /// which is a player who has stopped typing (CTL-5b). A car left on the throttle while nobody is saying
    /// anything would drive into whatever is in front of it.
    /// </summary>
    public void Coast() => Hand = Hand.Held ? new HandInput(true, 0f, 0f, false, Vector2.Zero) : HandInput.None;

    /// <summary>How long a step holds for, in seconds of the town. Nought for the steps that are done at once.</summary>
    public static float SecondsOf(in DriveStep step) =>
        step.Verb is DriveVerb.Drive or DriveVerb.Wait ? step.Seconds : 0f;

    /// <summary>
    /// A unit picked out (CTL-1), by the same call a click makes — <b>which gives up the wheel</b>, so the
    /// hand a drive is holding is dropped with it.
    /// </summary>
    void Pick(Selection unit)
    {
        world.Select(unit);
        Hand = HandInput.None;
        Picked?.Invoke();
    }

    /// <summary>The unit whose own footprint covers a place, or nothing — a click that landed on the town.</summary>
    Selection Under(Vector2 pointM, int line)
    {
        var unit = world.Pick(pointM);
        return unit.Any
            ? unit
            : throw new ArgumentException(
                $"drive step at line {line}: nothing stands at {pointM.X:F1},{pointM.Y:F1}. " +
                "`cars X Y` lists what is near a place.");
    }

    /// <summary>The car standing nearest a place, whatever it is doing — a wreck included, since one reads as a wreck.</summary>
    int NearestCar(Vector2 pointM, int line)
    {
        var cars = world.Cars;
        var nearest = -1;
        var nearestSq = float.MaxValue;
        for (var car = 0; car < cars.Count; car++)
        {
            var distanceSq = (cars.PositionM[car] - pointM).LengthSquared();
            if (distanceSq >= nearestSq) continue;

            nearest = car;
            nearestSq = distanceSq;
        }

        return nearest >= 0
            ? nearest
            : throw new ArgumentException($"drive step at line {line}: {map} stands no car at all.");
    }

    /// <summary>
    /// What is standing near a place, nearest first: the numbers <c>select car N</c> takes, with enough
    /// beside each to tell them apart.
    /// </summary>
    void List(Vector2 pointM)
    {
        var cars = world.Cars;
        var near = new List<(float DistanceM, int Car)>(cars.Count);
        for (var car = 0; car < cars.Count; car++)
        {
            near.Add(((cars.PositionM[car] - pointM).Length(), car));
        }

        near.Sort((left, right) => left.DistanceM.CompareTo(right.DistanceM));
        foreach (var (distanceM, car) in near.Take(CarsListed))
        {
            _told.Add(
                $"car {car,-5} {distanceM,7:F1} m away at {cars.PositionM[car].X:F1},{cars.PositionM[car].Y:F1}  " +
                $"{CarCatalog.Shared.Variants[cars.Variant[car]].Id,-14} {DrivingWords.CarName(cars, car)}");
        }
    }

    /// <summary>Whatever the drive is driving, or a refusal: a hand with nothing picked out reaches nothing.</summary>
    void Selected(int line)
    {
        if (world.SelectedCount == 0)
        {
            throw new ArgumentException(
                $"drive step at line {line}: nothing is picked out, so a hand reaches nothing. Select a car first.");
        }
    }

    /// <summary>Where a frame is centred: where the script pinned it, or where the unit under the hand is.</summary>
    Vector2? Where() =>
        LookM ?? (world.SelectedCount > 0 && world.Whereabouts(world.Lead, out var atM, out _) ? atM : null);

    /// <summary>
    /// What the step left behind: where the body is and how fast, then <b>the very rows the unit panel
    /// draws</b> (<see cref="UnitReadout"/>, OBS-2m). The pose is this file's own because no panel writes
    /// it — everything else about the unit is read off the one machine the interface reads.
    /// </summary>
    public DriveReading Reading(in DriveStep step, int number, long tick, IReadOnlyList<string> told)
    {
        var rows = new List<string>(UnitReadout.MostRows);
        var unit = "nothing picked out";
        var body = string.Empty;

        if (world.SelectedCount > 0)
        {
            var lead = world.Lead;
            unit = world.SelectedCount > 1
                ? $"{world.SelectedCount} units"
                : $"{(lead.Kind == SelectionKind.Car ? "car" : "walker")} {lead.Index}";

            Span<char> text = stackalloc char[UnitReadout.MostRows * UnitReadout.RoomPerRow];
            Span<int> ends = stackalloc int[UnitReadout.MostRows];
            var count = UnitReadout.Lines(world, lead, scenario, text, ends);
            for (var row = 0; row < count; row++)
            {
                rows.Add(new string(text[(row > 0 ? ends[row - 1] : 0)..ends[row]]));
            }

            body = Pose(lead);
        }

        return new DriveReading(
            number, step.Said, step.Line, tick, world.ElapsedS, unit, body, Holding(), [.. rows], [.. told]);
    }

    /// <summary>
    /// Where the body stands and which way it is pointing — the one thing a reader has that a log has not,
    /// since a panel is drawn over the town and a log is not.
    /// </summary>
    string Pose(Selection unit)
    {
        if (!world.Whereabouts(unit, out var atM, out var velocityMps)) return "not on the ground";

        var pose = $"at {atM.X:F1},{atM.Y:F1}, {velocityMps.Length() * 3.6f:F1} km/h";
        return unit.Kind == SelectionKind.Car
            ? $"{pose}, facing {float.RadiansToDegrees(world.Cars.HeadingRad[unit.Index]):F0}°"
            : pose;
    }

    /// <summary>What the drive has its hands on, in the words the script itself uses.</summary>
    string Holding()
    {
        if (!Hand.Held) return "hands off";

        var pedal = Hand.Throttle switch
        {
            > 0f => $"throttle {Hand.Throttle:F2}",
            < 0f => $"brake {-Hand.Throttle:F2}",
            _ => "no pedal",
        };

        return $"{pedal}, steer {Hand.Steer:F2}{(Hand.Handbrake ? ", handbrake" : string.Empty)}";
    }
}
