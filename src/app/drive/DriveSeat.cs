using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Drive;

/// <summary>
/// <b>DRV-7 — the seat a drive is driven from, in a run somebody is watching.</b> It ties the file being
/// written to the hand on the wheel: every frame it takes whatever has been appended, brings the tape up
/// to the tick the loop has reached, and hands back what is held.
/// </summary>
/// <remarks>
/// <b>It is the same machine a headless drive uses</b> — the steps are <see cref="DriveScript"/>'s, what
/// they do is <see cref="DriveHands"/>'s and when they happen is <see cref="DriveTape"/>'s. What this adds
/// is only that the script is read again as it grows, and that the readings are said as they are taken
/// rather than at the end, because somebody is reading them while the town runs.
/// </remarks>
internal sealed class DriveSeat
{
    readonly DriveTail _tail;
    readonly DriveTape _tape;

    public DriveSeat(
        TownWorld world, ScenarioWatch[] scenario, in DriveAsk ask, SimConfig config,
        Func<DriveFrame, string>? photograph, Action<bool>? hold, Action? picked, Action<float>? pace)
    {
        Hands = new DriveHands(world, scenario, ask.Map, ask.FramesDir, ask.ViewM, ask.Ui)
        {
            Photograph = photograph,
            Hold = hold,
            Picked = picked,
            Pace = pace,
        };

        Log = new DriveLog(ask.Map, ask.Script) { Echo = true };
        _tail = new DriveTail(ask.Script);
        _tape = new DriveTape(Hands, Log, config.Sim.TickRateHz);
    }

    public DriveHands Hands { get; }

    public DriveLog Log { get; }

    /// <summary>What to push through the seam this tick.</summary>
    public HandInput Hand => _tape.Hand;

    /// <summary>
    /// The drive brought up to the tick the run has reached, reading whatever has been written since the
    /// last one. <b>A step that cannot be carried out is said and dropped</b>, and the run goes on: a
    /// mistyped line in a live drive is a line to write again, not a town to tear down.
    /// </summary>
    public void Follow(long tick)
    {
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
}
