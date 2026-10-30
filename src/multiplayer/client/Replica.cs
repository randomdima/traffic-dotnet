using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Multiplayer.Wire;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Multiplayer.Client;

/// <summary>
/// <b>A player's copy of what moves in its area</b> (MP-7): fleets of its own, the shape of the server's, posed each
/// frame between the two snapshots either side of the instant drawn, and carried on past the newest for no longer than
/// it may. It holds nothing it was not sent (MP-1), and steps nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fields it fills are the ones a picture reads</b> (<c>MovingScene</c>), so the town is drawn from it exactly
/// as from the server's own fleets. What only the server's actors know — a car's line, a walker's plan — it never has.
/// </para>
/// <para>
/// <b>Its fleets are as long as a snapshot, not as the town</b>: a mover is given a slot as it is first drawn and keeps
/// it until a frame draws it no more, so a car's slot is not its number and every question by number goes through
/// <see cref="Slot"/>. A town of seventy thousand movers costs a player the thousand it can be sent.
/// </para>
/// </remarks>
internal sealed class Replica
{
    readonly Welcome _welcome;
    readonly WireShape _shape;
    readonly ReceivedSnapshots _snapshots;
    readonly int[] _carsDrawn;
    readonly int[] _peopleDrawn;
    readonly int[] _moversDrawn;
    readonly Vector2[] _peopleLastM;
    readonly long[] _personPosedAt;
    readonly byte[] _drivers;

    /// <summary>Each mover's slot in its fleet while it holds one, by mover number, or −1; a wagon's is its own index.</summary>
    readonly int[] _slotOf;

    /// <summary>The mover in each car slot and each walker slot, or −1 for a slot free.</summary>
    readonly int[] _carIn;
    readonly int[] _personIn;

    /// <summary>The pose each slot was last drawn in, which is how a slot no longer drawn is found and freed.</summary>
    readonly long[] _carDrawnAt;
    readonly long[] _personDrawnAt;
    readonly SlotStack _freeCars;
    readonly SlotStack _freePeople;
    long _poses;
    int _carsCount;
    int _peopleCount;
    int _moversCount;

    public Replica(Welcome welcome, SimConfig config)
    {
        _welcome = welcome;
        _shape = welcome.Shape;
        var movers = welcome.Movers;
        var mostSent = config.Multiplayer.MostMoversSent;
        _snapshots = new ReceivedSnapshots(_shape, config.Multiplayer.BaselinesKept, mostSent);

        // Laid whole and once, as many as one snapshot can hold of each; where each stands is written as it is drawn.
        var unused = new Rng(0, 0);
        var carSlots = Math.Max(1, Math.Min(movers.Cars, mostSent));
        var personSlots = Math.Max(1, Math.Min(movers.People, mostSent));
        Cars = new CarFleet(carSlots, 1, CarBuilds.OfTheFleet(config, CarCatalog.Shared), 0f);
        for (var car = 0; car < carSlots; car++) Cars.Add(Vector2.Zero, 0f, 0, false, unused);
        People = new PersonFleet(personSlots);
        for (var person = 0; person < personSlots; person++) People.Add(Vector2.Zero, 0f, 0f, 0f, 0, unused, false);
        Wagons = new WagonView[movers.Wagons];
        Hitches = new TowHitches(carSlots, movers.Wagons);
        _carsDrawn = new int[carSlots];
        _peopleDrawn = new int[personSlots];
        _moversDrawn = new int[mostSent];
        _drivers = new byte[movers.Count];
        _slotOf = new int[movers.Count];
        Array.Fill(_slotOf, -1);
        for (var wagon = 0; wagon < movers.Wagons; wagon++) _slotOf[movers.OfWagon(wagon)] = wagon;
        _carIn = new int[carSlots];
        _personIn = new int[personSlots];
        Array.Fill(_carIn, -1);
        Array.Fill(_personIn, -1);
        _carDrawnAt = new long[carSlots];
        _personDrawnAt = new long[personSlots];
        _freeCars = new SlotStack(carSlots);
        _freePeople = new SlotStack(personSlots);
        _peopleLastM = new Vector2[personSlots];
        _personPosedAt = new long[personSlots];
        Array.Fill(_personPosedAt, -1L);
    }

    public CarFleet Cars { get; }

    public PersonFleet People { get; }

    /// <summary>Every rail vehicle; those not in the area are out of the world.</summary>
    public WagonView[] Wagons { get; }

    /// <summary>The tow arms between the cars drawn: none, since a hitch is not sent (docs/tech-debt.md).</summary>
    public TowHitches Hitches { get; }

    /// <summary>The car slots drawn this frame, in mover order.</summary>
    public ReadOnlySpan<int> CarsDrawn => _carsDrawn.AsSpan(0, _carsCount);

    public ReadOnlySpan<int> PeopleDrawn => _peopleDrawn.AsSpan(0, _peopleCount);

    /// <summary>Every mover drawn this frame by its number, in mover order.</summary>
    public ReadOnlySpan<int> MoversDrawn => _moversDrawn.AsSpan(0, _moversCount);

    /// <summary>The newest snapshot held, which every input acknowledges.</summary>
    public uint Newest => _snapshots.Newest;

    /// <summary>How the server numbers what moves, which says what kind a number is.</summary>
    public MoverIds Movers => _welcome.Movers;

    /// <summary>The newest snapshot's account of the seat: what the player is drawn as by its mover number, or −1, and its state.</summary>
    public int OwnMover { get; private set; } = -1;

    public SeatState Seat { get; private set; }

    /// <summary>The seat whose player a drawn mover is, or −1 for one no player is (MP-12).</summary>
    public int SeatOf(int mover) => _drivers[mover] - 1;

    /// <summary>Who the server last said <paramref name="seat"/>'s player is (MP-15), or false before it has said.</summary>
    public bool Card(int seat, out SeatCard card) => _snapshots.Card(seat, out card);

    /// <summary>The newest input the server said it had applied, its low sixteen bits.</summary>
    public uint InputApplied { get; private set; }

    /// <summary>Where a mover stands in its fleet — a car's or a walker's slot, a wagon's index — or −1 for one not held.</summary>
    public int Slot(int mover) => mover < 0 ? -1 : _slotOf[mover];

    /// <summary>A snapshot read and kept, and the server tick it stands for; or false (<see cref="ReceivedSnapshots.Take"/>).</summary>
    public bool Take(ReadOnlySpan<byte> message, out uint serverTick)
    {
        serverTick = 0;
        if (!_snapshots.Take(message)) return false;

        var header = _snapshots.Find(_snapshots.Newest)!.Header;
        OwnMover = header.OwnMover;
        Seat = header.Seat;
        InputApplied = header.InputApplied;
        serverTick = header.ServerTick;
        return true;
    }

    /// <summary>
    /// <b>The area posed at <paramref name="tick"/></b>, a fractional server tick: each mover of the later of the two
    /// snapshots either side placed between them, or carried on past the newest for at most
    /// <see cref="Welcome.LongestExtrapolationS"/>. Allocates nothing.
    /// </summary>
    public void Pose(double tick)
    {
        if (!Bracket(tick, out var older, out var newer)) return;

        var spanTicks = (double)newer.Header.ServerTick - older.Header.ServerTick;
        var share = spanTicks <= 0d
            ? 0f
            : (float)Math.Min((tick - older.Header.ServerTick) / spanTicks, 1d + (_welcome.LongestExtrapolationS * _welcome.TickRateHz / spanTicks));
        share = MathF.Max(0f, share);
        var spanS = (float)(spanTicks / _welcome.TickRateHz);

        _poses++;
        _carsCount = 0;
        _peopleCount = 0;
        _moversCount = 0;
        for (var wagon = 0; wagon < Wagons.Length; wagon++) Wagons[wagon].Flags &= ~WagonView.InWorld;

        // The slots of those still drawn marked first, so one let go of this frame is free for one entering it.
        foreach (ref readonly var now in newer.Held) Mark(now.Id);
        Free(_carIn, _carDrawnAt, _freeCars, null);
        Free(_personIn, _personDrawnAt, _freePeople, _personPosedAt);

        var from = older.Held;
        var o = 0;
        foreach (ref readonly var now in newer.Held)
        {
            while (o < from.Length && from[o].Id < now.Id) o++;
            var was = o < from.Length && from[o].Id == now.Id ? from[o] : now;
            Place(was, now, share, spanS);
        }
    }

    void Mark(int mover)
    {
        var slot = _slotOf[mover];
        if (slot < 0) return;

        switch (_welcome.Movers.KindOf(mover))
        {
            case MoverKind.Car: _carDrawnAt[slot] = _poses; break;
            case MoverKind.Person: _personDrawnAt[slot] = _poses; break;
        }
    }

    /// <summary>Every slot of a fleet not drawn this pose given back; <paramref name="posedAt"/>, where given, forgets its stride.</summary>
    void Free(int[] moverIn, long[] drawnAt, SlotStack free, long[]? posedAt)
    {
        for (var slot = 0; slot < moverIn.Length; slot++)
        {
            if (moverIn[slot] < 0 || drawnAt[slot] == _poses) continue;

            _slotOf[moverIn[slot]] = -1;
            moverIn[slot] = -1;
            if (posedAt is not null) posedAt[slot] = -1;
            free.Push(slot);
        }
    }

    /// <summary>The slot a mover of a fleet is drawn in, given one if it has none; −1 where every slot is taken.</summary>
    int Taken(int mover, int[] moverIn, SlotStack free)
    {
        var slot = _slotOf[mover];
        if (slot >= 0 || free.Count == 0) return slot;

        slot = free.Pop();
        moverIn[slot] = mover;
        _slotOf[mover] = slot;
        return slot;
    }

    void Place(in MoverState was, in MoverState now, float share, float spanS)
    {
        var atWasM = _shape.FromPosition(was.X, was.Y);
        var atNowM = _shape.FromPosition(now.X, now.Y);
        var atM = Vector2.Lerp(atWasM, atNowM, share);
        var velocityMps = spanS > 0f ? (atNowM - atWasM) / spanS : Vector2.Zero;
        var headingRad = Turned(_shape.FromHeading(was.Heading), _shape.FromHeading(now.Heading), share);
        _drivers[now.Id] = now.Driver;
        switch (_welcome.Movers.KindOf(now.Id))
        {
            case MoverKind.Car:
                var car = Taken(now.Id, _carIn, _freeCars);
                if (car < 0) return;

                _carDrawnAt[car] = _poses;
                Cars.PositionM[car] = atM;
                Cars.VelocityMps[car] = velocityMps;
                Cars.HeadingRad[car] = headingRad;
                Cars.Forward[car] = Heading.Unit(headingRad);
                Cars.TiltRad[car] = new Vector2(
                    float.Lerp(_shape.FromSmallAngle(was.TiltAlong), _shape.FromSmallAngle(now.TiltAlong), share),
                    float.Lerp(_shape.FromSmallAngle(was.TiltAcross), _shape.FromSmallAngle(now.TiltAcross), share));
                Cars.Command[car] = new DriveCommand(
                    float.Lerp(_shape.FromSmallAngle(was.Steer), _shape.FromSmallAngle(now.Steer), share), 0f, 0f, false, false);
                Cars.Lamps[car] = (CarLampSet)now.Lamps;
                Cars.Broken[car] = (now.Flags & MoverFlags.Wrecked) != 0;
                Cars.Channels[car] = now.Channels;
                Cars.Variant[car] = (byte)now.Look;
                _carsDrawn[_carsCount++] = car;
                break;
            case MoverKind.Person:
                var person = Taken(now.Id, _personIn, _freePeople);
                if (person < 0) return;

                _personDrawnAt[person] = _poses;
                People.PositionM[person] = atM;
                People.VelocityMps[person] = velocityMps;
                People.HeadingRad[person] = headingRad;
                People.Walking[person] = (now.Flags & MoverFlags.Walking) != 0;
                People.Wounded[person] = (now.Flags & MoverFlags.Down) != 0;
                People.Variant[person] = (byte)now.Look;
                People.Channels[person] = now.Channels;

                // The stride is the distance walked, which a player measures off the places it draws on consecutive frames.
                if (_personPosedAt[person] == _poses - 1) People.DistanceWalkedM[person] += Vector2.Distance(_peopleLastM[person], atM);
                _personPosedAt[person] = _poses;
                _peopleLastM[person] = atM;
                _peopleDrawn[_peopleCount++] = person;
                break;
            case MoverKind.Wagon:
                var wagon = _welcome.Movers.IndexOf(now.Id);
                Wagons[wagon].PositionM = atM;
                Wagons[wagon].VelocityMps = velocityMps;
                Wagons[wagon].HeadingRad = headingRad;
                Wagons[wagon].Flags = WagonView.FlagsOf(now.Look, inWorld: true, now.Channels);
                break;
        }

        _moversDrawn[_moversCount++] = now.Id;
    }

    /// <summary>
    /// The two held snapshots to pose between: the latest at or before <paramref name="tick"/> and the earliest after it —
    /// or past the newest, the two newest; before the oldest, the oldest twice. False when nothing is held.
    /// </summary>
    bool Bracket(double tick, out ReceivedSnapshots.Snapshot older, out ReceivedSnapshots.Snapshot newer)
    {
        ReceivedSnapshots.Snapshot? atOrBefore = null, after = null, newest = null, nextNewest = null;
        for (var back = 0; back < _snapshots.Kept; back++)
        {
            if (_snapshots.Newest < back) break;

            var held = _snapshots.Find(_snapshots.Newest - (uint)back);
            if (held is null) continue;

            var at = held.Header.ServerTick;
            if (newest is null) newest = held;
            else nextNewest ??= held;
            if (at <= tick && (atOrBefore is null || at > atOrBefore.Header.ServerTick)) atOrBefore = held;
            if (at > tick && (after is null || at < after.Header.ServerTick)) after = held;
        }

        older = atOrBefore ?? after!;
        newer = after ?? newest!;
        if (after is null && nextNewest is not null)
        {
            older = nextNewest;
            newer = newest!;
        }

        return newest is not null;
    }

    /// <summary>A heading the short way round from one to another, a share of the way.</summary>
    static float Turned(float fromRad, float toRad, float share)
    {
        var turnRad = MathF.IEEERemainder(toRad - fromRad, 2f * MathF.PI);
        return fromRad + turnRad * share;
    }

    /// <summary>Where the player is drawn — its body, or what it is in — how fast it is going and which way it faces; or false while it is drawn as nothing.</summary>
    public bool Own(out Vector2 atM, out Vector2 velocityMps, out Vector2 forward) => Drawn(OwnMover, out atM, out velocityMps, out forward);

    /// <summary>Where a mover is drawn this frame, how fast it is going and which way it faces; or false for one not drawn.</summary>
    public bool Drawn(int mover, out Vector2 atM, out Vector2 velocityMps, out Vector2 forward)
    {
        (atM, velocityMps, forward) = (default, default, default);
        var slot = Slot(mover);
        if (slot < 0) return false;

        switch (Movers.KindOf(mover))
        {
            case MoverKind.Car when _carDrawnAt[slot] == _poses:
                (atM, velocityMps, forward) = (Cars.PositionM[slot], Cars.VelocityMps[slot], Cars.Forward[slot]);
                return true;
            case MoverKind.Person when _personPosedAt[slot] == _poses:
                (atM, velocityMps) = (People.PositionM[slot], People.VelocityMps[slot]);
                forward = Heading.Unit(People.HeadingRad[slot]);
                return true;
            case MoverKind.Wagon when (Wagons[slot].Flags & WagonView.InWorld) != 0:
                (atM, velocityMps) = (Wagons[slot].PositionM, Wagons[slot].VelocityMps);
                forward = Heading.Unit(Wagons[slot].HeadingRad);
                return true;
            default:
                return false;
        }
    }

    /// <summary>The free slots of one fleet, taken from the top.</summary>
    sealed class SlotStack
    {
        readonly int[] _slots;

        public SlotStack(int slots)
        {
            _slots = new int[slots];
            for (var slot = 0; slot < slots; slot++) _slots[slot] = slots - 1 - slot;
            Count = slots;
        }

        public int Count { get; private set; }

        public void Push(int slot) => _slots[Count++] = slot;

        public int Pop() => _slots[--Count];
    }
}
