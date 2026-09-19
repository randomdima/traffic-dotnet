using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Drive;

/// <summary>
/// <b>A hand a tape may push steps into</b>: the player's own over whatever is picked out
/// (<see cref="DriveHands"/>), or a second driver's over the car it named (<see cref="BotHands"/>, DRV-8).
/// <b>What a step means is the hand's and when it happens is the tape's</b>, which is the whole of why the
/// two are apart.
/// </summary>
internal interface IAtTheWheel
{
    /// <summary>What is being held down, which the loop pushes through the seam every tick.</summary>
    HandInput Hand { get; }

    /// <summary>One step, carried out. A step this hand may not take throws rather than being dropped.</summary>
    IReadOnlyList<string> Take(in DriveStep step);

    /// <summary>The wheel kept and the pedals let go of, which is what a tape that has run out holds.</summary>
    void Coast();

    /// <summary>What the step left behind, in the figures this hand reads.</summary>
    DriveReading Reading(in DriveStep step, int number, long tick, IReadOnlyList<string> told);
}

/// <summary>
/// <b>DRV-7 — the steps as a tape the run follows, which may still be being written.</b> A step with a
/// while to it holds the hand for that many ticks and the next one begins where it ends; the steps that
/// are done at once all happen in the tick they come due in.
/// </summary>
/// <remarks>
/// <para>
/// <b>A reading is taken when a step ends, not when it starts</b>, because what is worth printing is what
/// the step did. The last step's reading is taken when the tape runs dry.
/// </para>
/// <para>
/// <b>A tape that has run out coasts</b> (<see cref="DriveHands.Coast"/>): the wheel is still taken, as
/// CTL-5b says it is, and nothing is pressed — so a car whose driver has stopped saying anything rolls to
/// a stop instead of carrying on into whatever is in front of it. That is the whole of what makes a live
/// drive safe to think in front of.
/// </para>
/// </remarks>
internal sealed class DriveTape(IAtTheWheel hands, DriveLog log, int tickRateHz)
{
    readonly Queue<DriveStep> _waiting = new();

    /// <summary>The step being held, while it is being held, and what it had to say when it was taken.</summary>
    DriveStep? _inHand;

    IReadOnlyList<string> _told = [];

    /// <summary>The tick the step in hand runs to. Nothing is due before it.</summary>
    long _until;

    int _taken;

    /// <summary>What the run pushes through the seam this tick.</summary>
    public HandInput Hand => hands.Hand;

    /// <summary>Whether everything written so far has been driven, with nothing left in hand.</summary>
    public bool Idle => _waiting.Count == 0 && _inHand is null;

    /// <summary>How many steps have been taken, which is what a reading is numbered by.</summary>
    public int Taken => _taken;

    /// <summary>More steps, read off the end of a script somebody is still writing.</summary>
    public void Add(IReadOnlyList<DriveStep> steps)
    {
        foreach (var step in steps) _waiting.Enqueue(step);
    }

    /// <summary>
    /// The tape brought up to this tick: every step that has come due is taken, and the one that holds
    /// longest is left in hand.
    /// </summary>
    /// <returns>Whether a step ended in this tick, which is when there is something new to read.</returns>
    public bool At(long tick)
    {
        var ended = false;
        while (tick >= _until)
        {
            if (_inHand is { } finished)
            {
                log.Took(hands.Reading(finished, _taken, tick, _told));
                _inHand = null;
                ended = true;
            }

            if (!_waiting.TryDequeue(out var next))
            {
                // Nothing left to do: the hand lets go of the pedals and keeps the wheel, and the tape
                // waits on this tick rather than running ahead of the run.
                hands.Coast();
                _until = tick;
                return ended;
            }

            _told = hands.Take(next);
            _taken++;
            _inHand = next;
            _until = tick + Math.Max(0L, (long)MathF.Round(DriveHands.SecondsOf(next) * tickRateHz));

            // A step with a while to it is in hand until that while is up; one that is done at once is
            // finished inside this tick, and the next step comes due immediately.
            if (_until > tick) return ended;

            log.Took(hands.Reading(next, _taken, tick, _told));
            _inHand = null;
            ended = true;
        }

        return ended;
    }
}
