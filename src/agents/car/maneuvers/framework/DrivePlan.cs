namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// One step of a plan: which entry of the catalogue, and the one number that makes it the step it is —
/// the bay to leave, the bay to park in. <b>A step is parametrised and never bespoke</b>: two legs that
/// park in different bays are the same chain with a different subject, which is what lets the planner
/// emit a plan without knowing anything about how an entry is driven.
/// </summary>
/// <param name="Id">The entry.</param>
/// <param name="Subject">What the step is about, or −1 where the entry needs no subject.</param>
internal readonly record struct PlannedStep(Maneuver Id, int Subject)
{
    public static PlannedStep None => new(Maneuver.None, NoSubject);

    public const int NoSubject = -1;
}

/// <summary>
/// <b>The chain the route search hands a driver</b>: the ordered, parametrised steps that take this leg
/// from where the car stands to where the trip wants it. Nothing else in the town decides what a car
/// does next — the planner names the steps, the catalogue drives them, and a step that fails names its
/// own successor.
/// </summary>
/// <remarks>
/// <para>
/// <b>The chain is a skeleton and not a script</b> (MAN-2). It carries the steps that are facts about the
/// leg — leaving this bay, running the route, parking in that bay, standing in it — and nothing about the
/// junctions, queues and crossings between them, because everything past the next junction is a
/// prediction about other agents. Those entries are reached from `P-4`'s own exits as the road produces
/// them, and each hands back to `P-4` when it is done.
/// </para>
/// <para>
/// <b>A step is taken, never peeked-and-assumed.</b> An entry that succeeds without naming a successor is
/// asking for the next step, and an empty chain answers `P-4` — which re-derives from the pose the car
/// actually reached, and is what MAN-3 asks for anyway.
/// </para>
/// <para>
/// It is arrays for the whole fleet rather than a list per car, for the reason everything here is: a leg
/// begins several times a second across a town, and a plan that allocated would allocate then.
/// </para>
/// </remarks>
internal sealed class DrivePlan
{
    /// <summary>
    /// The longest chain a leg needs: leave the bay, run the line, park in a bay to turn at a car park and
    /// leave it the other way (GEN-4l), run the line on, park, stand. The two spare are for a leg re-derived
    /// mid-way, which is the same chain with the step it has already taken still in it.
    /// </summary>
    public const int StepsPerLeg = 9;

    readonly PlannedStep[] _steps;
    readonly int[] _count;
    readonly int[] _taken;

    public DrivePlan(int cars)
    {
        _steps = new PlannedStep[cars * StepsPerLeg];
        _count = new int[cars];
        _taken = new int[cars];
    }

    /// <summary>Drop whatever chain this car was carrying. Every replan starts here (MAN-3).</summary>
    public void Clear(int car)
    {
        _count[car] = 0;
        _taken[car] = 0;
    }

    /// <summary>
    /// Put one step on the end. <b>Refused past the bound rather than grown</b> — a chain that does not
    /// fit is a plan that has stopped being a skeleton, and the honest answer is to plan again from
    /// further along.
    /// </summary>
    public bool Add(int car, Maneuver id, int subject = PlannedStep.NoSubject)
    {
        if (_count[car] >= StepsPerLeg) return false;

        _steps[(car * StepsPerLeg) + _count[car]++] = new PlannedStep(id, subject);
        return true;
    }

    /// <summary>How many steps of this car's chain have not been taken yet.</summary>
    public int Left(int car) => _count[car] - _taken[car];

    /// <summary>The next step without taking it, which is what a read-out draws.</summary>
    public PlannedStep Next(int car) =>
        Left(car) > 0 ? _steps[(car * StepsPerLeg) + _taken[car]] : PlannedStep.None;

    /// <summary>The next step, taken. <see cref="PlannedStep.None"/> where the chain is spent.</summary>
    public PlannedStep Take(int car)
    {
        if (Left(car) <= 0) return PlannedStep.None;

        return _steps[(car * StepsPerLeg) + _taken[car]++];
    }

    /// <summary>
    /// What subject the chain holds for an entry, wherever in it that entry stands. <b>A reactive
    /// manoeuvre that hands a leg back needs the bay the plan was aiming at</b>, and searching the chain
    /// for it is cheaper and far less brittle than carrying a copy of it beside the chain.
    /// </summary>
    public int SubjectFor(int car, Maneuver id)
    {
        for (var step = 0; step < _count[car]; step++)
        {
            var planned = _steps[(car * StepsPerLeg) + step];
            if (planned.Id == id) return planned.Subject;
        }

        return PlannedStep.NoSubject;
    }
}
