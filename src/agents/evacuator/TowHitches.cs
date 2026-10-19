namespace TrafficSimulation.Agents.Evacuator;

/// <summary>
/// <b>Every tow in the town, read from either end</b> (EVA-5): what each tractor's arm holds and by which end, and which
/// tractor holds each end of each body. A body is a car or a rail vehicle, numbered as one run — every car by its own
/// index, then every rail vehicle after them (<see cref="OfWagon"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>One arm to a tractor and one fork to an end.</b> A body has two ends and each takes one fork, so two tractors may
/// hold one body from its two sides and never from one; and a tractor may itself be held, which is how a chain is made.
/// Nothing here refuses a loop — a tractor holding, through others, the one that holds it — because nothing pulls it
/// apart; what walks a chain is bounded instead (<see cref="Hauler"/>).
/// </para>
/// <para>
/// <b>Laid over the whole fleet and every rail vehicle</b>, for the reason <see cref="RecoveryDuty"/> is: a car's index
/// means the same thing here as everywhere else.
/// </para>
/// </remarks>
internal sealed class TowHitches
{
    readonly int[] _load;
    readonly bool[] _loadByTheTail;
    readonly float[] _forkAlongM;
    readonly int[] _heldBy;

    public TowHitches(int cars, int wagons)
    {
        Cars = cars;
        Bodies = cars + wagons;
        _load = new int[cars];
        Array.Fill(_load, Nothing);
        _loadByTheTail = new bool[cars];
        _forkAlongM = new float[cars];
        _heldBy = new int[Bodies * 2];
        Array.Fill(_heldBy, Nothing);
    }

    public const int Nothing = -1;

    /// <summary>How many cars there are: a body numbered below this is a car, and one at or past it a rail vehicle.</summary>
    public int Cars { get; }

    /// <summary>How many bodies can be towed: every car and every rail vehicle.</summary>
    public int Bodies { get; }

    public int OfWagon(int wagon) => Cars + wagon;

    public bool IsACar(int body) => body >= 0 && body < Cars;

    /// <summary>The body on this tractor's arm, or <see cref="Nothing"/>.</summary>
    public int Load(int tractor) => _load[tractor];

    public bool IsTowing(int tractor) => _load[tractor] != Nothing;

    /// <summary>The car on this tractor's arm, or <see cref="Nothing"/> where it holds a rail vehicle or nothing.</summary>
    public int CarOnTheBar(int tractor) => IsACar(_load[tractor]) ? _load[tractor] : Nothing;

    /// <summary>Which end of its load this tractor's fork holds.</summary>
    public bool LoadByTheTail(int tractor) => _loadByTheTail[tractor];

    /// <summary>
    /// <b>Where on its load the fork is</b>: how far along the load's own axis from its middle, forward positive — what the
    /// arm is drawn reaching to, laid with the hitch so nothing that draws it has to know what kind of body it holds.
    /// </summary>
    public float ForkAlongM(int tractor) => _forkAlongM[tractor];

    /// <summary>The tractor holding this end of a body, or <see cref="Nothing"/>.</summary>
    public int HeldBy(int body, bool byTheTail) => _heldBy[(body * 2) + (byTheTail ? 1 : 0)];

    public bool IsHeld(int body) => _heldBy[body * 2] != Nothing || _heldBy[(body * 2) + 1] != Nothing;

    public bool IsHeldAt(int body, bool byTheTail) => HeldBy(body, byTheTail) != Nothing;

    /// <summary>
    /// <b>The tractor a body is moved by</b>: the one at its nose, or at its tail where its nose is free — or
    /// <see cref="Nothing"/> where it is on no arm.
    /// </summary>
    /// <remarks>Held at both ends, the two tractors pull it between them, and the nose's is the one named.</remarks>
    public int Hauler(int body) => _heldBy[body * 2] is var nose and not Nothing ? nose : _heldBy[(body * 2) + 1];

    /// <summary>
    /// <b>Whoever moves the whole chain a body is in</b>: its hauler's hauler, on to the first tractor nothing holds — or
    /// the body itself where it is on no arm. A loop has no such tractor, and the walk ends after as many steps as there
    /// are cars, on whichever one it stands at.
    /// </summary>
    public int FrontOfTheChain(int body)
    {
        for (var steps = 0; steps < Cars; steps++)
        {
            var hauler = Hauler(body);
            if (hauler == Nothing) return body;

            body = hauler;
        }

        return body;
    }

    /// <summary>A body onto a tractor's arm by one end. The arm must be empty and the end free.</summary>
    public void Hitch(int tractor, int body, bool byTheTail, float forkAlongM)
    {
        _load[tractor] = body;
        _loadByTheTail[tractor] = byTheTail;
        _forkAlongM[tractor] = forkAlongM;
        _heldBy[(body * 2) + (byTheTail ? 1 : 0)] = tractor;
    }

    /// <summary>A tractor's arm emptied; the body it let go of, or <see cref="Nothing"/>.</summary>
    public int Unhitch(int tractor)
    {
        var body = _load[tractor];
        if (body == Nothing) return Nothing;

        _heldBy[(body * 2) + (_loadByTheTail[tractor] ? 1 : 0)] = Nothing;
        _load[tractor] = Nothing;
        return body;
    }
}
