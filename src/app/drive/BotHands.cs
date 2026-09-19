using System.Numerics;
using System.Text;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Drive;

/// <summary>
/// <b>DRV-8 — what a step does when the hand is not the reader's.</b> The pedals, the wheel and the
/// handbrake become the hand <see cref="TownWorld.HandOnCar"/> is given every tick for one named car
/// (CTL-5d); a <c>shot</c> step is answered by the seat's own eye. <b>Nothing here touches the selection,
/// the camera or the panels</b>, which are whoever is watching the window's.
/// </summary>
/// <remarks>
/// <para>
/// <b>It may hold less than a script may.</b> Picking units out, orders, the lever and the reset all reach
/// the reader's own selection, so a hand that is driving beside them is refused every one of them — the
/// words are <see cref="Allowed"/> and a step outside them is said and dropped (DRV-7).
/// </para>
/// <para>
/// <b>The reading is a dashboard and not the panel</b> (DRV-8): where the car is, how fast, which way it
/// points and whether it is still in one piece. What the town knows about it — the line it is on, the room
/// ahead, the route — is the reader's panel to look at and never this seat's, because a driver that was
/// told what the town knows would be driving on something no driver has.
/// </para>
/// </remarks>
internal sealed class BotHands(TownWorld world, SimConfig config, int car, string framesDir, float viewM)
    : IAtTheWheel
{
    readonly List<string> _told = [];

    int _frames;

    /// <summary>
    /// <b>Where this driver last put the wheel</b>, which stands until a line moves it (DRV-8). It is the
    /// driver's own setting and not the car's angle: the rack winds at its own rate (CAR-3a) and a coast
    /// still lets the wheel go, so what this restores is what was asked for and never what was reached.
    /// </summary>
    float _steer;

    /// <summary>What this seat may ask for, quoted at whoever wrote a line it cannot take.</summary>
    public const string Allowed =
        "drive SECONDS [throttle=N] [brake=N] [steer=N] [handbrake=on] | coast SECONDS | shot NAME | " +
        "view METRES | pace N | agents on|off";

    /// <summary>The car this hand is on. It is named once, when the seat is taken, and never changes.</summary>
    public int Car => car;

    /// <summary>
    /// <b>The wheel is taken when the seat is</b>, and not when the first pedal is pressed: a car whose
    /// driver has not said anything yet is a car with somebody in it (CTL-5b), and one that went on driving
    /// its own route until the first <c>drive</c> step would be a bot that inherited a car at speed.
    /// </summary>
    public HandInput Hand { get; private set; } = new(true, 0f, 0f, false, Vector2.Zero);

    /// <summary>What a <c>shot</c> step is answered with — the seat's own eye (DRV-8), or nothing at all.</summary>
    public Func<DriveFrame, string>? Photograph { get; init; }

    /// <summary>The player's <c>Pause</c> key, which is the run's and is pressed here only by a preamble.</summary>
    public Action<bool>? Hold { get; init; }

    /// <summary>The player's pace keys, for the same reason a live drive has them (DRV-7).</summary>
    public Action<float>? Pace { get; init; }

    /// <summary>How much town the eye spans, which a step may change as it goes.</summary>
    public float ViewM { get; private set; } = viewM;

    public IReadOnlyList<string> Take(in DriveStep step)
    {
        _told.Clear();
        switch (step.Verb)
        {
            case DriveVerb.Drive:
                // DRV-8: **the wheel is where this driver left it** until a line names it again. A step is a
                // burst and not a whole hand, so one about the pedals would otherwise throw the wheel
                // straight — and a driver that has to restate its own steering every line spends every line
                // restating it.
                _steer = step.Steered ? step.Hand.Steer : _steer;
                Hand = new HandInput(
                    true, step.Hand.Throttle, _steer, step.Hand.Handbrake,
                    new Vector2(_steer, -step.Hand.Throttle));
                break;

            case DriveVerb.View:
                ViewM = step.ViewM;
                break;

            case DriveVerb.Shot:
                if (Photograph is not { } eye)
                {
                    _told.Add("this seat has no eye, so there is no frame to take");
                    break;
                }

                var path = Path.Combine(framesDir, $"{_frames++:D3}-{step.Name}.png");
                _told.Add(eye(new DriveFrame(step.Name!, path, ViewM, At(), ["none"])));

                // The same dashboard the reading carries, written beside the frame it belongs to (DRV-8):
                // what reads a bot's frames is a program watching a folder rather than somebody reading a
                // terminal, and a picture whose figures arrived down another pipe is two readings to line up.
                File.WriteAllText(Path.ChangeExtension(path, ".txt"), Dashboard());
                break;

            case DriveVerb.Agents:
                if (Hold is not { } pause) throw Refused(step, "this run has no run keys to press");

                pause(!step.On);
                _told.Add(step.On ? "the town's agents are deciding again" : "the town's agents are held");
                break;

            case DriveVerb.Pace:
                if (Pace is not { } pace) throw Refused(step, "this run has no clock to pace");

                pace(step.Seconds);
                _told.Add($"the town runs at {step.Seconds:F2}x real time");
                break;

            default:
                throw Refused(step, $"a second driver holds its own car and nothing else. It may say: {Allowed}");
        }

        return _told;
    }

    /// <summary>
    /// <b>The hand a seat holds when nothing has been said for a while</b> (CTL-5b): the wheel still taken
    /// and the pedals let go of. It is what makes a bot that has stopped answering a car rolling to a stop.
    /// </summary>
    public void Coast() => Hand = Hand.Held ? new HandInput(true, 0f, 0f, false, Vector2.Zero) : HandInput.None;

    /// <summary>
    /// <b>The dashboard</b> (DRV-8): what the car itself knows — where it stands, how fast it is going,
    /// which way it points, what it is and whether it is still in one piece.
    /// </summary>
    public DriveReading Reading(in DriveStep step, int number, long tick, IReadOnlyList<string> told) =>
        new(number, step.Said, step.Line, tick, world.ElapsedS, $"car {car}", Body(), Holding(), Rows(), [.. told]);

    /// <summary>
    /// <b>DRV-8 — the handbook: what a driver is told about the car before it has driven a metre.</b> Every
    /// figure is this car's own, read off the build the solver actually drives (<see cref="CarBuild"/>) and
    /// the eye's own framing, so a briefing cannot quote a car that was retuned since it was written.
    /// </summary>
    /// <remarks>
    /// <b>It is the spec sheet and not the town's opinion</b> (DRV-8): what this car will do, what a pedal
    /// and the wheel are worth on it, what the streets it is on were laid for, and how much town a frame
    /// holds. Nothing in it is about where this car is or what is in front of it — that is the dashboard's,
    /// tick by tick, and the picture's.
    /// </remarks>
    public string Handbook(int eyeWidthPx, int eyeHeightPx)
    {
        ref readonly var build = ref world.Cars.BuildOf(car);
        var paved = config.Terrain.PavedCoefficient;
        var brakingMps2 = build.UtmostBrakingMps2(paved);
        var streetMps = config.CityGen.StreetDesignSpeedMps;
        var junctionMps = config.CityGen.JunctionDesignSpeedMps;
        var pxPerM = MathF.Min(eyeWidthPx, eyeHeightPx) / ViewM;

        return $"""
            # The car you are driving

            Car {car} is a {CarCatalog.Shared.Variants[world.Cars.Variant[car]].Id}: {world.Cars.MassKg[car]:F0} kg, {build.LengthM:F2} m long and {build.WidthM:F2} m wide.

            ## What it will do
            top speed        {build.MaxSpeedMps * 3.6f:F0} km/h forward, {build.ReverseMaxMps * 3.6f:F0} km/h in reverse
            full throttle    {build.AccelerationMps2:F1} m/s2 — standing still to {streetMps * 3.6f:F0} km/h in {streetMps / build.AccelerationMps2:F1} s
            full brake       {brakingMps2:F1} m/s2 — {streetMps * 3.6f:F0} km/h to standing still in {streetMps * streetMps / (2f * brakingMps2):F0} m and {streetMps / brakingMps2:F1} s
            a pedal          takes {build.AccelerationMps2 / build.PedalRateMps3:F2} s to travel from nothing to the floor, so a burst shorter than that never gets all of what it asked for
            the wheel        locks at {float.RadiansToDegrees(build.MaxSteerRad):F0} degrees and takes {build.MaxSteerRad / build.SteerRateRadPerS:F2} s to wind that far
            tightest turn    {build.TurningRadiusM:F1} m of radius at full lock, and only while the car is rolling
            grip             {build.GripMps2:F1} m/s2 — at {streetMps * 3.6f:F0} km/h the tightest corner it holds is {build.CorneringRadiusM(streetMps, paved, config.Driving.GripMargin):F0} m of radius, at {junctionMps * 3.6f:F0} km/h it is {build.CorneringRadiusM(junctionMps, paved, config.Driving.GripMargin):F1} m

            ## How it is driven
            A pedal and the wheel are held where a step put them until the next step moves them, and let go
            of when the steps run out — so the car rolls on rather than stopping when you stop talking.
            The brake and the reverse are one pedal: held past a standstill it backs the car up, and in
            reverse the wheel turns the car the other way round. A standing car does not turn at all.
            The handbrake locks the rear wheels. There is no gear to change and no clutch.

            ## What the town does about you
            These streets are laid for {streetMps * 3.6f:F0} km/h and a turn at a junction for {junctionMps * 3.6f:F0} km/h.
            The traffic is not told you are there: nothing gives way to you and nothing waits for you.
            A kerb, a wall or a tree stops {world.Cars.MassKg[car]:F0} kg dead, and enough of that wrecks the car —
            a wreck takes no hand at all, so the drive is over.

            ## What you see
            every frame      {eyeWidthPx}x{eyeHeightPx} px of {eyeWidthPx / pxPerM:F0} m by {eyeHeightPx / pxPerM:F0} m of town, {pxPerM:F1} px to the metre
            your car         is exactly in the middle of it, {build.LengthM * pxPerM:F0} px long
            the frame        never turns: the town's north is up the frame, whichever way the car points
            headings         facing -90 is up the frame, 0 is right, 90 is down, 180 or -180 is left

            """;
    }

    /// <summary>The whole dashboard as one block, which is what is written beside a frame.</summary>
    string Dashboard()
    {
        var text = new StringBuilder();
        text.Append("hand       ").Append(Holding()).Append('\n');
        text.Append("body       ").Append(Body()).Append('\n');
        foreach (var row in Rows()) text.Append(row).Append('\n');

        return text.ToString();
    }

    /// <summary>Where the car stands, how fast it is going and which way it points.</summary>
    string Body()
    {
        var atM = world.Cars.PositionM[car];
        return $"at {atM.X:F1},{atM.Y:F1}, {world.Cars.VelocityMps[car].Length() * 3.6f:F1} km/h, " +
               $"facing {float.RadiansToDegrees(world.Cars.HeadingRad[car]):F0}°";
    }

    /// <summary>What the car is and whether it is still in one piece — the rest of what a driver has.</summary>
    string[] Rows() =>
    [
        $"make       {CarCatalog.Shared.Variants[world.Cars.Variant[car]].Id}, {world.Cars.MassKg[car]:F0} kg",
        $"state      {(world.Cars.Broken[car] ? "wrecked" : "in one piece")}",
    ];

    /// <summary>Where the eye looks: at the car it is driving, always — a bot has no camera to pan.</summary>
    Vector2? At() => world.Cars.PositionM[car];

    /// <summary>What this hand has hold of, in the words the steps themselves use.</summary>
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

    static ArgumentException Refused(in DriveStep step, string why) =>
        new($"bot step at line {step.Line}, \"{step.Said}\": {why}.");
}
