using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Drive;

/// <summary>
/// <b>What a second seat is of</b> (DRV-8): the car it holds, the file it is steered from, where its eye
/// leaves frames and how big they are.
/// </summary>
/// <param name="Car">The car in the fleet this seat drives, which it names once and never changes.</param>
/// <param name="ViewM">How much town the eye spans across its short side, as <c>--view</c> asks for it.</param>
/// <param name="Waits">
/// Whether <b>the town stands still while this driver is thinking</b> (DRV-8): with nothing left on the
/// tape the clock stops until a step arrives, so a driver that takes ten seconds to look at a frame has
/// not moved a metre in them.
/// </param>
internal readonly record struct BotAsk(
    int Car,
    string Steps,
    string FramesDir,
    string? Out,
    int EyeWidthPx,
    int EyeHeightPx,
    float ViewM,
    bool Waits);

/// <summary>
/// <b>DRV-8 — the second seat, in a run somebody else is watching.</b> It is the live drive's own machine
/// (<see cref="DriveSeat"/>) with three things changed: the car is named rather than picked out, the frames
/// are the seat's own eye rather than the glass, and what a step may ask for is a driver's and not a
/// reader's (<see cref="BotHands"/>).
/// </summary>
/// <remarks>
/// <b>Nothing of the run is this seat's.</b> The selection, the camera, the panels, the switches and the
/// keys stay with whoever has the window, which is the whole point of it: two drivers in one town, one of
/// them watching.
/// </remarks>
internal sealed class BotSeat : IDisposable
{
    readonly DriveTail _tail;
    readonly DriveTape _tape;
    readonly BotEye? _eye;
    readonly bool _waits;

    /// <summary>What the handbook is called, where the frames go. Whoever briefs the driver reads it.</summary>
    public const string HandbookFile = "handbook.md";

    public BotSeat(
        in BotAsk ask, SimConfig config, TownWorld world, BotEye? eye, Action<bool>? hold, Action<float>? pace)
    {
        if (ask.Car < 0 || ask.Car >= world.Cars.Count)
        {
            throw new ArgumentException(
                $"there is no car {ask.Car} in this town, which stands {world.Cars.Count}.");
        }

        _eye = eye;
        _waits = ask.Waits;
        Hands = new BotHands(world, config, ask.Car, ask.FramesDir, ask.ViewM)
        {
            Photograph = eye is null ? null : wanted => eye.Draw(wanted, world, _tick),
            Hold = hold,
            Pace = pace,
        };

        Log = new DriveLog($"car {ask.Car}", ask.Steps) { Echo = true };
        _tail = new DriveTail(ask.Steps);
        _tape = new DriveTape(Hands, Log, config.Sim.TickRateHz);

        // DRV-8: the handbook is written when the seat is taken, beside the frames — a driver that has to be
        // told what it is driving is told by the town that stood the car up, and not by whoever wrote the
        // harness a month ago.
        var handbook = Path.GetFullPath(Path.Combine(ask.FramesDir, HandbookFile));
        Directory.CreateDirectory(Path.GetDirectoryName(handbook)!);
        File.WriteAllText(handbook, Hands.Handbook(ask.EyeWidthPx, ask.EyeHeightPx));
    }

    long _tick;

    public BotHands Hands { get; }

    public DriveLog Log { get; }

    /// <summary>What to push through this car's own seam this tick (CTL-5d).</summary>
    public HandInput Hand => Hands.Hand;

    /// <summary>
    /// <b>DRV-8 — whether the town should be standing still</b>: this seat was asked to be waited for and
    /// has nothing left on its tape. It is read once a frame, not once a tick, because a town that is
    /// waiting is a town no tick is due in.
    /// </summary>
    public bool WaitingToBeTold => _waits && _tape.Idle;

    /// <summary>
    /// The seat brought up to the tick the run has reached, reading whatever has been appended since the
    /// last one. <b>A step it cannot take is said and dropped</b> and the run goes on — a bot that asked
    /// for something it has not got is a line to write again, not a town to tear down.
    /// </summary>
    public void Follow(long tick)
    {
        _tick = tick;
        try
        {
            _tape.Add(_tail.More());
            _tape.At(tick);
        }
        catch (ArgumentException refused)
        {
            Log.Refused(refused.Message);
        }
    }

    public void Dispose() => _eye?.Dispose();
}
