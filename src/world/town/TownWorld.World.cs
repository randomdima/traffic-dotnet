using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The town's side of the seam to the world layer</b> (root <c>decision-log.md</c>, 2026-10-11): what the actors tell
/// the world, as control signals, and what they read of it, as the shallow state the last step left.
/// </summary>
/// <remarks>
/// <b>While the town is being laid there is no world yet</b>: a body is put where it stands on the fleets alone, and the
/// world is laid off them (<see cref="LayingForTheWorld"/>). Once laid, nothing here touches a body but through a signal,
/// and a signal names an agent, never a body.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>The world the town drives (<see cref="WorldLayer"/>).</summary>
    public WorldLayer World => _world;

    /// <summary>What a world is laid off, as the town stands now: the furniture, the ground, and each agent's body.</summary>
    WorldLaying LayingForTheWorld
    {
        get
        {
            var cars = new CarLaid[Cars.Count];
            for (var car = 0; car < cars.Length; car++)
            {
                cars[car] = new CarLaid(Cars.PositionM[car], Cars.HeadingRad[car], Cars.Variant[car], Cars.Channels[car]);
            }

            var walkers = new WalkerLaid[People.Count];
            for (var person = 0; person < walkers.Length; person++)
            {
                walkers[person] = new WalkerLaid(People.PositionM[person], People.HeadingRad[person], People.Inside[person].Any);
            }

            return new WorldLaying(_config, _plan.WorldSizeM, Statics, _atlas, WheelGrounds, _builds, cars, walkers, _roads)
            {
                Wagons = WagonsLaid, Rails = _rails, Barriers = BarriersLaid,
            };
        }
    }

    /// <summary>
    /// <b>The world handed to another layer</b> before the town's first tick — the device's, laid off the town as it stands
    /// (<see cref="WorldLaying"/>), with whatever was signalled to the one it replaces and not yet taken. That one is let
    /// go of.
    /// </summary>
    public void HandTheWorldTo(Func<WorldLaying, WorldLayer> lay)
    {
        if (_world.Steps > 0) throw new InvalidOperationException("A world is handed over before the town's first tick.");

        // <b>The one it replaces is let go of first</b>: its solver's tables are the new one's size, and a page keeps the
        // most it was ever given (the root decision log, 2026-10-15).
        var signals = _world.Signals.Written.ToArray();
        _world.Dispose();
        _world = null!;
        CollectTheLay(_plan, _config);

        _world = lay(LayingForTheWorld);
        _world.Signals.AddRange(signals);

        if (_world is not ManagedWorld) LetGoOfTheGround();

        // The new world is laid where the fleets have every body, so nothing set is waiting on its signal any more — and
        // follows nothing until each car's is signalled to it again.
        Array.Clear(_carSetBy);
        Array.Clear(_walkerSetBy);
        Array.Fill(_followedLaid, NeverFollowed);
        _turnsTakenOff = -1;
        ReadTheWorld();
    }

    /// <summary>
    /// <b>The ground and the furniture are the world's from here on</b> (the root decision log, 2026-10-13): a world laid
    /// off them keeps its own, and the town reads neither while it runs — the managed world, which reads both every step,
    /// is the one that keeps the town's.
    /// </summary>
    void LetGoOfTheGround()
    {
        _staticBodyCount = Statics.Count;
        _atlas = null;
        _statics = null;
    }

    /// <summary>A signal to the world, where there is one yet: while the town is laid, the fleets are the laying.</summary>
    void Signal(in ControlSignal signal) => _world?.Signals.Add(signal);

    /// <summary>
    /// <b>The batch of signals that set each car or walker somewhere</b> (<see cref="WorldLayer.SignalsTaken"/>): until a
    /// view includes it, the view shows the body where it stood before, and the fleet keeps where the town set it
    /// (<see cref="ReadTheWorld"/>).
    /// </summary>
    readonly long[] _carSetBy, _walkerSetBy;

    /// <summary>The batch the next signal goes in.</summary>
    long NextBatch => _world is null ? 0 : _world.SignalsTaken + 1;

    int AgentOfCar(int car) => Roster.AgentOfCar(car);

    /// <summary>
    /// What was last signalled of each car and walker, so a command or an aim crosses only where it changed — begun at what
    /// a world is laid holding: every car parked, no walker yet told anything.
    /// </summary>
    readonly DriveCommand[] _sentCommand;

    readonly bool[] _sentHand;
    readonly Vector2[] _sentAimM;
    readonly ushort[] _sentWalk;

    /// <summary>Nothing signalled yet: every car's and walker's first state crosses.</summary>
    const ushort NeverSent = ushort.MaxValue;

    /// <summary><b>A car's wheel and pedals</b> (<see cref="CarFleet.Command"/>) handed to the world, where they changed.</summary>
    void SignalTheWheel(int car)
    {
        var command = Cars.Command[car];
        var hand = Cars.Action[car] == CarAction.Hand;
        if (command == _sentCommand[car] && hand == _sentHand[car]) return;

        _sentCommand[car] = command;
        _sentHand[car] = hand;
        Signal(ControlSignal.Drive(AgentOfCar(car), command, hand));
    }

    /// <summary>
    /// <b>What each car's world was last told it follows</b>: the line it was laid off (<see cref="CarFleet.LinesLaid"/>),
    /// the serial it went with, and the progress the town last read the car at — a line laid again, or a progress put
    /// anywhere by anything but a reading, is a follow to signal again (<see cref="SignalTheFollow"/>).
    /// </summary>
    readonly uint[] _followedLaid;

    readonly ushort[] _followSerial;
    readonly float[] _progressRead;

    /// <summary>No line followed yet, which no count of lines laid is.</summary>
    const uint NeverFollowed = uint.MaxValue;

    /// <summary>
    /// <b>The lanes a car's line is on handed to the world to read it along</b> (<see cref="LineFollowed"/>), where its line
    /// was laid again or its progress put somewhere since it was last told — on its tick and on its decision, so a line laid
    /// either way is read along from the step after next.
    /// </summary>
    void SignalTheFollow(int car)
    {
        // A laneless line — a manoeuvre at a bay — is read along by nothing (<see cref="LineFollowed.None"/>), so its
        // progress moving is nothing to tell the world, and only its laying is.
        var laid = Cars.LinesLaid[car];
        if (laid == _followedLaid[car] && (Cars.Line[car].LaneCount == 0 || Cars.ProgressM[car] == _progressRead[car])) return;

        _followedLaid[car] = laid;
        _progressRead[car] = Cars.ProgressM[car];
        var serial = ++_followSerial[car];
        Signal(ControlSignal.Follow(AgentOfCar(car), FollowedBy(car, serial)));
    }

    LineFollowed FollowedBy(int car, ushort serial)
    {
        var lanes = Cars.Line[car].LaneCount;
        if (lanes == 0) return LineFollowed.None with { Serial = serial };

        var chain = Cars.ChainOf(car);
        var join = lanes > 1 ? Cars.JoinsOf(car)[0] : RoadGraph.NoConnector;
        var stopsAtM = lanes <= 2 ? Cars.LineStopsAtM[car] : _roads.LaneLengthM[chain[1]];
        return new LineFollowed(chain[0], join, stopsAtM, Cars.ProgressM[car], serial);
    }

    /// <summary>
    /// <b>Where the world read this car along its line</b>, or NaN where the reading is not of the line in hand: taken
    /// against an older follow, the line laid since, the progress put somewhere since, or the car set down somewhere the
    /// view does not show yet.
    /// </summary>
    float ReadProgressM(int car)
    {
        var view = _world.View;
        ref readonly var row = ref view.Cars[car];
        return row.FollowSerial == _followSerial[car] && Cars.LinesLaid[car] == _followedLaid[car]
                                                      && Cars.ProgressM[car] == _progressRead[car]
                                                      && _carSetBy[car] <= view.SignalsTaken
            ? row.ProgressM
            : float.NaN;
    }

    readonly ushort[] _sentShow;

    /// <summary>
    /// <b>What a car shows handed to the world to draw</b>, where it changed: what its lamps say this tick
    /// (<see cref="CarLamps.Showing"/>, CAR-14) and whether it is a wreck.
    /// </summary>
    void SignalTheShow(int car)
    {
        var lamps = Cars.Lamps[car] = ShowsItsLampsInTurn(car)
            ? CarLamps.InTurn(_elapsedS, _config.Lamps.ShowroomStepS)
            : CarLamps.Showing(Cars, car, _config, WheelIsHeldOver(car));
        var signal = ControlSignal.Show(AgentOfCar(car), lamps, Cars.Broken[car]);
        if (signal.Flags == _sentShow[car]) return;

        _sentShow[car] = signal.Flags;
        Signal(signal);
    }

    /// <summary>
    /// Whether this car puts its lamps through their states in turn (CAR-14.9): picked out in the showroom, intact, and
    /// nothing driving it.
    /// </summary>
    bool ShowsItsLampsInTurn(int car) =>
        _showroom && !Cars.Driven[car] && !Cars.Broken[car] && !WheelIsHeldOver(car)
        && _selected.Holds(SelectionKind.Car, car);

    /// <summary>
    /// <b>A walker's aim handed to the world</b>, where it changed: walked at where it walks, faced where it stands, and its
    /// stance held where it is paused or down (<c>WalkerFollower</c>).
    /// </summary>
    void SignalTheWalk(int person, bool paused)
    {
        if (People.Inside[person].Any) return;

        var wounded = People.Wounded[person];
        var still = paused || wounded;
        var aimM = still ? People.PositionM[person] : People.DestinationM[person];
        var flags = (ushort)((!still && People.Walking[person] ? ControlSignal.Moving : 0)
                             | (wounded ? 0 : ControlSignal.OnFeet) | (still ? 0 : ControlSignal.Faces));

        // A stance held is held where the body stood when it was taken, not chased as the body slides.
        if (flags == _sentWalk[person] && (still || aimM == _sentAimM[person])) return;

        _sentAimM[person] = aimM;
        _sentWalk[person] = flags;
        Signal(ControlSignal.Walk(person, aimM, flags.Has(ControlSignal.Moving), flags.Has(ControlSignal.OnFeet),
            flags.Has(ControlSignal.Faces)));
    }

    /// <summary><b>A walker put down</b> out of whatever held it, at rest, facing a heading.</summary>
    void PlaceTheWalker(int person, Vector2 atM, float headingRad)
    {
        _placements++;
        People.PositionM[person] = atM;
        People.HeadingRad[person] = headingRad;
        People.VelocityMps[person] = Vector2.Zero;
        _sentWalk[person] = NeverSent;
        _walkerSetBy[person] = NextBatch;
        Signal(ControlSignal.Place(Roster.AgentOfPerson(person), atM, headingRad));
    }

    /// <summary><b>A walker taken out of the world</b> into a container (PHY-7).</summary>
    void ContainTheWalker(int person)
    {
        _placements++;
        People.VelocityMps[person] = Vector2.Zero;
        _sentWalk[person] = NeverSent;
        Signal(ControlSignal.Contain(Roster.AgentOfPerson(person)));
    }

    /// <summary><b>A car put down</b> where something set it, at rest.</summary>
    void PlaceTheCar(int car, Vector2 atM, float headingRad)
    {
        _placements++;
        Cars.PositionM[car] = atM;
        Cars.HeadingRad[car] = headingRad;
        Cars.Forward[car] = Heading.Unit(headingRad);
        Cars.VelocityMps[car] = Vector2.Zero;
        Cars.YawRateRadPerS[car] = 0f;
        _carSetBy[car] = NextBatch;
        Signal(ControlSignal.Place(AgentOfCar(car), atM, headingRad));
    }

    /// <summary>
    /// <b>A walker moved onto another collision layer</b> (PHY-5b): down, or back up. Only ever in a running town, so the
    /// laying carries no layer.
    /// </summary>
    void PutTheWalkerOnLayer(int person, CollisionLayer layer) =>
        Signal(ControlSignal.Layer(Roster.AgentOfPerson(person), layer));

    void PutOnChannels(int car, byte channels)
    {
        Cars.Channels[car] = channels;
        _carSetBy[car] = NextBatch;
        Signal(ControlSignal.Channels(AgentOfCar(car), channels));
    }

    /// <summary>
    /// <b>A tractor's arm put on a body by one end</b>, or emptied where <paramref name="load"/> is −1 (EVA-5) — the body
    /// numbered as <see cref="TowHitches"/> numbers it.
    /// </summary>
    void SignalTheTow(int tractor, int load, bool byTheTail) =>
        Signal(ControlSignal.Tow(AgentOfCar(tractor), load < 0 ? -1 : AgentOfTowable(load), byTheTail));

    /// <summary>The agent a body <see cref="TowHitches"/> numbers is signalled as: a car's, or a rail vehicle's past the roster.</summary>
    int AgentOfTowable(int load) => _hitches.IsACar(load) ? AgentOfCar(load) : Roster.Count + (load - _hitches.Cars);

    void ClearTheWheels(int car) => Signal(ControlSignal.ClearWheels(AgentOfCar(car)));

    /// <summary>
    /// Whether a disc reaches a car's body where the world last left it — the car's own collider, off its build, as the
    /// solver stands it (<see cref="PhysicsWorld.AddCar"/>).
    /// </summary>
    bool Reaches(int car, Vector2 centreM, float radiusM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        return Shape.Collide(
            centreM, Shape.Rotation(0f), Vector2.Zero, radiusM, Cars.PositionM[car], Shape.Rotation(Cars.HeadingRad[car]),
            (build.CollisionSizeM * 0.5f) - new Vector2(build.CornerRadiusM), build.CornerRadiusM, 0f, out _);
    }

    /// <summary>
    /// <b>For a staging</b>: a car put down where it is wanted, at rest and its tyres forgotten — the fleet's pose at once,
    /// the world's with the next step or laying (<see cref="LayTheClaims"/>).
    /// </summary>
    public void StandTheCarAt(int car, Vector2 atM, float headingRad)
    {
        PlaceTheCar(car, atM, headingRad);
        ClearTheWheels(car);
    }

    /// <summary><b>For a staging</b>: a walker put down where it is wanted, at rest, as <see cref="StandTheCarAt"/>.</summary>
    public void StandTheWalkerAt(int person, Vector2 atM, float headingRad) => PlaceTheWalker(person, atM, headingRad);

    /// <summary><b>For a staging</b>: the command a staging set on the fleet (<see cref="CarFleet.Command"/>) handed to the world.</summary>
    public void HandOverTheCommand(int car) => SignalTheWheel(car);

    /// <summary><b>For a staging</b>: a car put on these collision channels (PHY-1a).</summary>
    public void PutTheCarOnChannels(int car, byte channels) => PutOnChannels(car, channels);

    /// <summary>For an instrument: how deep this car's body was into what it touched at the world's last step.</summary>
    public float OverlapOfCar(int car) => _world.View.Cars[car].OverlapM;

    /// <summary>For an instrument: how deep this walker's body was into what it touched at the world's last step.</summary>
    public float OverlapOfWalker(int person) => _world.View.Walkers[person].OverlapM;

    /// <summary>
    /// <b>The shallow state the world's view holds, read into the fleets</b>: every pose and motion and the channels a
    /// car is on — what every reader in the town reads of a body. A body set somewhere by a signal the
    /// view does not include yet keeps where the town set it (<see cref="_carSetBy"/>).
    /// </summary>
    /// <remarks>
    /// <b>Every view is read</b>, which is what lets a car's freezing be told from the next by the view it began in
    /// (<see cref="StandingCars.Freezing"/>).
    /// </remarks>
    void ReadTheWorld()
    {
        var view = _world.View;
        var cars = view.Cars;
        var steps = _world.Steps;
        for (var car = 0; car < Cars.Count; car++)
        {
            ref readonly var row = ref cars[car];
            if ((row.Flags & (CarView.Frozen | CarView.FrozenThroughLastStep)) == CarView.Frozen)
            {
                _standing.Freezing[car] = steps;
            }

            if (_carSetBy[car] > view.SignalsTaken) continue;

            Cars.PositionM[car] = row.PositionM;
            Cars.HeadingRad[car] = row.HeadingRad;
            Cars.Forward[car] = row.Forward;
            Cars.VelocityMps[car] = row.VelocityMps;
            Cars.YawRateRadPerS[car] = row.YawRateRadPerS;
            Cars.Channels[car] = row.Channels;
        }

        var walkers = view.Walkers;
        for (var person = 0; person < People.Count; person++)
        {
            // A contained body is not in the world: its pose is its container's business until it is put down again.
            if (People.Inside[person].Any || _walkerSetBy[person] > view.SignalsTaken) continue;

            ref readonly var row = ref walkers[person];
            People.DistanceWalkedM[person] += (row.PositionM - People.PositionM[person]).Length();
            People.PositionM[person] = row.PositionM;
            People.VelocityMps[person] = row.VelocityMps;
            People.HeadingRad[person] = row.HeadingRad;
        }
    }
}

file static class Flags
{
    public static bool Has(this ushort flags, ushort flag) => (flags & flag) != 0;
}
