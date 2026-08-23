using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Town;

/// <summary>What the player is talking to, and what a hand on the keys does to it. The seam is the whole point: a hand fills in the same command a follower would, so nothing below here can tell a driven unit from any other.</summary>
internal sealed partial class TownWorld
{
    /// <summary>The unit the interface is talking about, or nothing.</summary>
    public Selection Selected
    {
        get => _selected;
        set
        {
            // A change of selection gives up the wheel, which is why this is a property and not a
            // field: a hand left on a unit nobody is looking at drives it out of sight.
            if (value != _selected) _hands = default;

            _selected = value.Kind switch
            {
                SelectionKind.Person => value.Index >= 0 && value.Index < People.Count ? value : default,
                SelectionKind.Car => value.Index >= 0 && value.Index < Cars.Count ? value : default,
                _ => default,
            };
        }
    }

    /// <summary>The walker the sprites draw brighter, or −1 — the selection read the way the second pipeline needs it.</summary>
    public int SelectedPerson => _selected.Kind == SelectionKind.Person ? _selected.Index : -1;

    public int SelectedCar => _selected.Kind == SelectionKind.Car ? _selected.Index : -1;

    /// <summary>An order pins the goal the behaviour would otherwise have picked; nothing below it changes.</summary>
    /// <remarks>A terminal unit takes no orders — a rule about the interface and not only about the loop.</remarks>
    public void Order(int person, Vector2 toM)
    {
        if (person < 0 || person >= People.Count || People.Dead[person]) return;

        // An order gives up the wheel: both say where the unit goes, and a hand still on the keys
        // would overwrite the order on the very next tick.
        if (_selected.Kind == SelectionKind.Person && _selected.Index == person) _hands = default;

        _orderedPerson = person;
        _orderedToM = toM;
    }

    /// <summary>Reset: the walker goes back to choosing for itself.</summary>
    public void ReleaseOrder(int person)
    {
        if (person < 0 || person >= People.Count) return;

        People.Manual[person] = false;
    }

    /// <summary>
    /// What the player is holding down this tick, for the selected unit. Handed in once a frame and read
    /// by the agent loop every tick inside it.
    /// </summary>
    /// <remarks>
    /// A terminal unit takes no hand, and a hand on a unit that is no longer the selection is dropped by
    /// the selection setter — so this only ever describes the one unit on screen.
    /// </remarks>
    public void Hands(HandInput input) => _hands = _selected.Any && !IsTerminal(RosterIndexOf(_selected)) ? input : default;

    /// <summary>Reset for either kind: the unit goes back to deciding for itself.</summary>
    public void ReleaseHands() => _hands = default;

    /// <summary>Whether a hand is on the selected unit, which decides whether the arrows pan the camera.</summary>
    public bool HandsOn => _hands.Held && _selected.Any;

    int RosterIndexOf(Selection selection) => selection.Kind == SelectionKind.Car
        ? Roster.AgentOfCar(selection.Index)
        : Roster.AgentOfPerson(selection.Index);

    /// <summary>The unit under a point — a car first, then a walker — or nothing.</summary>
    /// <remarks>
    /// A car before a walker because a walker standing beside a car is the easy click and a walker
    /// <em>under</em> one is not visible to be clicked; picking the walker there would select
    /// something the player cannot see.
    /// </remarks>
    public Selection Pick(Vector2 pointM)
    {
        var car = CarAt(pointM);
        return car >= 0 ? new Selection(SelectionKind.Car, car) : Selection.Of(SelectionKind.Person, PersonAt(pointM));
    }

    /// <summary>The car whose footprint covers a point, or −1. Tested in the body's own frame, which is the frame it is drawn in.</summary>
    public int CarAt(Vector2 pointM)
    {
        var halfM = new Vector2(_config.Car.LengthM, _config.Car.WidthM) * 0.5f;
        for (var car = 0; car < Cars.Count; car++)
        {
            var heading = Cars.HeadingRad[car];
            var forward = Heading.Unit(heading);
            var offset = pointM - Cars.PositionM[car];
            var along = Vector2.Dot(offset, forward);
            var across = Vector2.Dot(offset, new Vector2(-forward.Y, forward.X));
            if (MathF.Abs(along) <= halfM.X && MathF.Abs(across) <= halfM.Y) return car;
        }

        return -1;
    }

    /// <summary>The walker under a point, or −1. The fine test the proximity index's superset was for.</summary>
    public int PersonAt(Vector2 pointM)
    {
        var best = -1;
        var bestDistanceSq = float.MaxValue;
        for (var person = 0; person < People.Count; person++)
        {
            // Nobody can click on somebody who is inside a building: they are not drawn and not there,
            // and the pose they left behind is their container's.
            if (People.Inside[person].Any) continue;

            var radius = People.RadiusM[person];
            var distanceSq = (People.PositionM[person] - pointM).LengthSquared();
            if (distanceSq > radius * radius || distanceSq >= bestDistanceSq) continue;

            best = person;
            bestDistanceSq = distanceSq;
        }

        return best;
    }

    public void ReadPlayerInput()
    {
        // Phase 1 advances the town's clock, so every decision this tick reads the same instant — which
        // is all the signals are derived from.
        _elapsedS += _config.TickSeconds;

        // The same clock a blocked-road mark expires against, so a stretch nobody has driven since is
        // tried again instead of believed for ever.
        _surcharges.Advance(_elapsedS);

        // An order outlives the tick it was given in, and the walker it was given to can be run over
        // inside that tick — so being terminal is asked here as well as where the order was taken.
        if (_orderedPerson < 0 || People.Dead[_orderedPerson])
        {
            _orderedPerson = -1;
            return;
        }

        TakeTheOrder(_orderedPerson, _orderedToM);
        _orderedPerson = -1;
    }

    /// <summary>
    /// A car under a hand. The behaviour concern is substituted wholesale — this is the one place soft
    /// rules stop being consulted, so the player may cross the centreline, leave the carriageway, ignore
    /// the queue ahead and drive into things on purpose.
    /// </summary>
    /// <remarks>
    /// Nothing under the behaviour changes: what comes out is a <see cref="DriveCommand"/> exactly as
    /// the follower produces one, so the per-gear speed caps, the steering lock and the friction ellipse
    /// all still bind. The junction is told, though — a hand-driven car still claims the box it is
    /// entering, so the cars around it queue and give way as they would to any other, and a car driven
    /// off its own line lets go, because a box held by somebody who has left is a bookkeeping jam.
    /// </remarks>
    void HandDrive(int car, in CarPose pose)
    {
        var forward = pose.Forward;
        var alongMps = Vector2.Dot(pose.VelocityMps, forward);
        Cars.GroundCoefficient[car] = _terrain.At(pose.PositionM).Coefficient;

        if (Cars.Line[car].ArcCount > 0)
        {
            var rearAxleM = CarFollower.RearAxleM(_config, pose.PositionM, forward);
            var progressM = CarFollower.ProgressM(_config, Cars.LineOf(car), rearAxleM, Cars.ProgressM[car]);
            if (CarFollower.OffLineM(Cars.LineOf(car), rearAxleM, progressM) > _config.CarOffPathM * OffLineTolerance)
            {
                DropTheMovement(car);
                Cars.Line[car] = default;
            }
            else
            {
                Cars.ProgressM[car] = progressM;

                // The claim is what the other cars read; what it would have told this car to do is
                // discarded, and the headway is handed in as unbounded so a queue in front cannot
                // stop the player taking the box.
                JunctionStopM(car, progressM, alongMps, float.PositiveInfinity, out _, out _);
            }
        }

        Cars.Command[car] = HandCommand(alongMps);
        Cars.Hold[car] = DrivingHold.None;

        // Nothing was asked of the world on this car's behalf, so the layer has nothing about it to
        // draw: a hand-driven car reads no book and is stopped by nothing but the hand on the pedal.
        Cars.Context[car] = DriveContext.Clear;
        Tyres(car, pose);
    }

    /// <summary>
    /// The keys as a command: the throttle forward, the brake back, and back again from a stop as
    /// reverse. Space is the handbrake, which for a car is the rear pair locked.
    /// </summary>
    DriveCommand HandCommand(float alongMps)
    {
        var steerRad = _hands.Steer * _config.Car.MaxSteeringDeg * MathF.PI / 180f;
        var reverse = _hands.Throttle < 0f && alongMps <= _config.Driving.StopSpeedMps;

        // The gear's own cap, which is a hard rule and stays one whoever is at the wheel.
        var capMps = reverse ? _config.Car.ReverseMaxMps : _config.Car.MaxSpeedMps;
        var wanted = reverse ? -_hands.Throttle : _hands.Throttle;
        if (wanted > 0f && MathF.Abs(alongMps) >= capMps) wanted = 0f;

        return new DriveCommand(
            steerRad,
            MathF.Max(0f, wanted) * _config.Car.AccelerationMps2,
            reverse || _hands.Throttle >= 0f ? 0f : -_hands.Throttle * _config.Car.BrakingMps2,
            _hands.Handbrake,
            reverse);
    }

    /// <summary>
    /// A walker under a hand: the keys ask for a direction and the follower answers it, so the turn
    /// rate, the pace and the ground under the feet all still bind.
    /// </summary>
    /// <remarks>
    /// The destination is pinned well ahead of the body rather than at the pointer, which is what
    /// makes releasing the keys <em>coast</em> rather than hand the walker back: the wheel is given
    /// up by the reset, by a change of selection or by a terminal state, and never by letting go.
    /// </remarks>
    void HandWalk(int person)
    {
        var wanted = _hands.WalkDirection;
        People.Manual[person] = true;
        if (_hands.Handbrake || wanted.LengthSquared() <= 0f)
        {
            People.Walking[person] = false;
            return;
        }

        People.Walking[person] = true;
        People.DestinationM[person] = People.PositionM[person] + Vector2.Normalize(wanted) * HandWalkReachM;
    }

    /// <summary>
    /// How far ahead a hand-driven walker's goal is pinned. Far enough that arriving is not something
    /// that can happen while a key is held, and no further: the follower turns towards it, so a goal
    /// half a town away would turn a sidestep into a course correction.
    /// </summary>
    const float HandWalkReachM = 5f;
}
