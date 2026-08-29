using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Town;

/// <summary>What the player is talking to, and what a hand on the keys does to it. The seam is the whole point: a hand fills in the same command a follower would, so nothing below here can tell a driven unit from any other.</summary>
internal sealed partial class TownWorld
{
    /// <summary>The units the interface is talking about, in the order they were picked out (CTL-1b).</summary>
    public ReadOnlySpan<Selection> Selected => _selected.Units;

    /// <summary>
    /// The first of them, which is what a read-out with room for one unit says. Nothing decides anything
    /// by it: every seam below reads the whole set.
    /// </summary>
    public Selection Lead => _selected.Lead;

    public int SelectedCount => _selected.Count;

    public int SelectedCountOf(SelectionKind kind) => _selected.CountOf(kind);

    public bool IsSelected(SelectionKind kind, int index) => _selected.Holds(kind, index);

    /// <summary>One unit, in place of whatever was picked out before — the plain click.</summary>
    /// <remarks>
    /// Clicking the unit already picked out is not a change of selection, so it does not give up the
    /// wheel (CTL-5b): the wheel is let go of by picking something <em>else</em>.
    /// </remarks>
    public void Select(Selection unit)
    {
        var wanted = Standing(unit);
        if (_selected.Count == (wanted.Any ? 1 : 0) && _selected.Lead == wanted) return;

        _selected.Clear();
        _selected.Add(wanted);
        GaveUpTheWheel();
    }

    /// <summary>
    /// One unit added to what is already picked out, or dropped from it if it was there — shift-click
    /// (CTL-1b). A full set refuses the unit and keeps the set it has.
    /// </summary>
    public void SelectAlso(Selection unit)
    {
        var wanted = Standing(unit);
        if (!wanted.Any) return;

        var changed = _selected.Remove(wanted) || _selected.Add(wanted);
        if (changed) GaveUpTheWheel();
    }

    public void SelectNone()
    {
        if (_selected.Clear()) GaveUpTheWheel();
    }

    /// <summary>
    /// <b>Every unit inside a box on the ground</b> — what a drag over the town asks (CTL-1b). Cars
    /// first and then walkers, up to the bound, and a box that catches nothing deselects exactly as a
    /// click on nothing does.
    /// </summary>
    /// <param name="add">Whether what is already picked out is kept, which is shift held through the drag.</param>
    /// <returns>How many units the box came to.</returns>
    public int SelectIn(Vector2 oneCornerM, Vector2 otherCornerM, bool add)
    {
        var lowM = Vector2.Min(oneCornerM, otherCornerM);
        var highM = Vector2.Max(oneCornerM, otherCornerM);
        var changed = !add && _selected.Clear();

        for (var car = 0; car < Cars.Count && !_selected.Full; car++)
        {
            if (OverlapsTheBox(car, lowM, highM)) changed |= _selected.Add(new Selection(SelectionKind.Car, car));
        }

        for (var person = 0; person < People.Count && !_selected.Full; person++)
        {
            // PHY-7: somebody inside a building or a car is not drawn, so a box over the street they are
            // parked in cannot pick them out — the container is what the reader can see.
            if (People.Inside[person].Any) continue;

            var radiusM = People.RadiusM[person];
            var atM = People.PositionM[person];
            var nearestM = Vector2.Clamp(atM, lowM, highM);
            if ((nearestM - atM).LengthSquared() <= radiusM * radiusM)
            {
                changed |= _selected.Add(new Selection(SelectionKind.Person, person));
            }
        }

        if (changed) GaveUpTheWheel();
        return _selected.Count;
    }

    /// <summary>A unit as the town knows it, or nothing at all: an index off the end of a fleet is not a unit.</summary>
    Selection Standing(Selection unit) => unit.Kind switch
    {
        SelectionKind.Person => unit.Index >= 0 && unit.Index < People.Count ? unit : default,
        SelectionKind.Car => unit.Index >= 0 && unit.Index < Cars.Count ? unit : default,
        _ => default,
    };

    /// <summary>
    /// A change of selection gives up the wheel, which is why every way into the set runs through here:
    /// a hand left on a unit nobody is looking at drives it out of sight.
    /// </summary>
    void GaveUpTheWheel() => _hands = default;

    /// <summary>
    /// Whether a car's own footprint overlaps a box on the ground — the same footprint a click tests
    /// against (<see cref="CoversTheSpot"/>), so what a box catches is what a reader can see it over.
    /// </summary>
    /// <remarks>
    /// The two shapes are a rotated box and an upright one, so it is four axes and no more: each box's
    /// pair, with the other's extent projected onto them.
    /// </remarks>
    bool OverlapsTheBox(int car, Vector2 lowM, Vector2 highM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var forward = Heading.Unit(Cars.HeadingRad[car]);
        var right = Heading.RightOf(forward);
        var halfM = new Vector2(build.HalfLengthM, build.FlankM);
        var boxHalfM = (highM - lowM) * 0.5f;
        var offset = Cars.PositionM[car] - ((lowM + highM) * 0.5f);

        var alongX = (halfM.X * MathF.Abs(forward.X)) + (halfM.Y * MathF.Abs(right.X));
        var alongY = (halfM.X * MathF.Abs(forward.Y)) + (halfM.Y * MathF.Abs(right.Y));
        if (MathF.Abs(offset.X) > boxHalfM.X + alongX || MathF.Abs(offset.Y) > boxHalfM.Y + alongY) return false;

        var boxAlongForward = (boxHalfM.X * MathF.Abs(forward.X)) + (boxHalfM.Y * MathF.Abs(forward.Y));
        var boxAlongRight = (boxHalfM.X * MathF.Abs(right.X)) + (boxHalfM.Y * MathF.Abs(right.Y));
        return MathF.Abs(Vector2.Dot(offset, forward)) <= halfM.X + boxAlongForward
            && MathF.Abs(Vector2.Dot(offset, right)) <= halfM.Y + boxAlongRight;
    }

    /// <summary>An order pins the goal the behaviour would otherwise have picked; nothing below it changes.</summary>
    /// <remarks>
    /// A unit that takes no actions takes no orders — a rule about the interface and not only about the
    /// loop. Every selected walker takes the same order in the same tick, so the queue holds as many as
    /// the selection can (CTL-1b).
    /// </remarks>
    public void Order(int person, Vector2 toM)
    {
        if (person < 0 || person >= People.Count || !People.Acts(person)) return;
        if (_ordered >= _orderedPeople.Length) return;

        // An order gives up the wheel: both say where the unit goes, and a hand still on the keys
        // would overwrite the order on the very next tick.
        if (_selected.Holds(SelectionKind.Person, person)) GaveUpTheWheel();

        for (var i = 0; i < _ordered; i++)
        {
            if (_orderedPeople[i] != person) continue;

            // The last word wins: two right-clicks in one frame are one order, at the second point.
            _orderedToM[i] = toM;
            return;
        }

        _orderedPeople[_ordered] = person;
        _orderedToM[_ordered] = toM;
        _ordered++;
    }

    /// <summary>Reset: the walker goes back to choosing for itself.</summary>
    public void ReleaseOrder(int person)
    {
        if (person < 0 || person >= People.Count) return;

        People.Manual[person] = false;
    }

    /// <summary>
    /// What the player is holding down this tick, for every selected unit. Handed in once a frame and
    /// read by the agent loop every tick inside it.
    /// </summary>
    /// <remarks>
    /// <b>One hand and many units</b> (CTL-1b): the same command reaches all of them, and each answers it
    /// with its own body. A terminal unit is not one of them — that is asked of each unit where the seam
    /// is read rather than here, because a selection may hold a wreck and a working car at once.
    /// </remarks>
    public void Hands(HandInput input) => _hands = _selected.Any ? input : default;

    /// <summary>
    /// <b>The reset</b> (CTL-4): the wheel is given up and so is manual mode, so every selected unit goes
    /// back to deciding for itself — a walker draws a trip again, a car takes a fare or picks its errand
    /// back up.
    /// </summary>
    /// <remarks>
    /// It reaches the selection and not the town. A unit ordered somewhere and then deselected is still
    /// under orders, which is what makes an order a thing given to a unit rather than a mode the interface
    /// is in; picking it out again and pressing the key is how it is handed back.
    /// </remarks>
    public void ReleaseHands()
    {
        _hands = default;
        foreach (var unit in _selected.Units)
        {
            if (unit.Kind == SelectionKind.Car) ReleaseOrderOfCar(unit.Index);
            else ReleaseOrder(unit.Index);
        }
    }

    /// <summary>
    /// <b>Every selected unit's own action, worked once</b> (CTL-7), and whether any of them had one to
    /// work. It is a lever and not a pedal: what a press reaches is the vehicle's own machinery, which
    /// the town's own crews reach through the same call and no other (<see cref="WorkTheArm"/>).
    /// </summary>
    /// <remarks>
    /// It needs no hand at the wheel. A player who has driven a truck into place and let the keys go still
    /// has it selected, and giving the wheel up is not giving up the winch.
    /// </remarks>
    public bool WorkTheAction()
    {
        var worked = false;
        foreach (var unit in _selected.Units)
        {
            if (unit.Kind == SelectionKind.Car) worked |= WorkTheArm(unit.Index);
        }

        return worked;
    }

    /// <summary>Whether a hand is on the selection, which decides whether the arrows pan the camera.</summary>
    public bool HandsOn => _hands.Held && _selected.Any;

    /// <summary>
    /// The units the player has the wheel of, or nothing — the one thing outside a car that a hand at its
    /// wheel changes, since a beacon runs while it is held (CTL-5c).
    /// </summary>
    public ReadOnlySpan<Selection> HandDriven => _hands.Held ? _selected.Units : default;

    /// <summary>
    /// <b>Whether this car's wheel is held over</b>, by the player's own hand (CTL-5) or by a map that
    /// holds its cars' wheels itself (<see cref="HoldTheWheels"/>) — which is the whole of CTL-5's
    /// substitution, and the same substitution whoever made it.
    /// </summary>
    /// <remarks>
    /// <b>The two are one state on purpose.</b> What a hand at the wheel means to the rest of the town is
    /// that no manoeuvre is selected, no soft rule is consulted and nothing but the ellipse holds the
    /// pedals back; a map driving its own cars is asking for exactly that, and a second predicate beside
    /// this one would be a second answer to <em>is anything deciding for this car</em>.
    /// </remarks>
    bool HandAtTheWheel(int car) =>
        !Cars.Broken[car] && (WheelIsHeldOver(car) || (_hands.Held && _selected.Holds(SelectionKind.Car, car)));

    /// <summary>
    /// Which of the two hands this car's is. <b>The player's outranks the map's</b>: somebody who has
    /// selected a car on the skidpad and pressed a key has taken its wheel off the pad, and the reset
    /// (CTL-4) is what gives it back.
    /// </summary>
    HandInput WheelOn(int car) =>
        _hands.Held && _selected.Holds(SelectionKind.Car, car) ? _hands : _wheelHeld?[car] ?? default;

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
        for (var car = 0; car < Cars.Count; car++)
        {
            if (CoversTheSpot(car, pointM)) return car;
        }

        return -1;
    }

    /// <summary>
    /// Whether this car's own footprint covers a point, tested in the body's own frame. <b>It is the
    /// footprint the car is drawn at</b>, because everything that asks is asking about what a reader can
    /// see: clicking a truck's tail is clicking the truck, and a fork inside that outline is a fork
    /// touching it (EVA-5).
    /// </summary>
    bool CoversTheSpot(int car, Vector2 pointM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var forward = Heading.Unit(Cars.HeadingRad[car]);
        var offset = pointM - Cars.PositionM[car];
        return MathF.Abs(Vector2.Dot(offset, forward)) <= build.HalfLengthM
            && MathF.Abs(Vector2.Dot(offset, Heading.RightOf(forward))) <= build.FlankM;
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
        // inside that tick — so whether it still acts is asked here as well as where the order was taken.
        for (var i = 0; i < _ordered; i++)
        {
            if (People.Acts(_orderedPeople[i])) TakeTheOrder(_orderedPeople[i], _orderedToM[i]);
        }

        _ordered = 0;
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
            var rearAxleM = CarFollower.RearAxleM(Cars.BuildOf(car), pose.PositionM, forward);
            var progressM = CarFollower.ProgressM(
                Cars.BuildOf(car), Cars.LineOf(car), rearAxleM, Cars.ProgressM[car]);
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

        Cars.Command[car] = HandCommand(car, Cars.BuildOf(car), alongMps, WheelOn(car));
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
    /// <remarks>
    /// <b>A key is a pedal being pushed and a wheel being wound, never either of them arriving</b> — the
    /// travel is the body's (CAR-3a, <see cref="CarBuild.PedalRateMps3"/>) and is the same travel the
    /// follower is held to, so what a hand gets is the car the town's own drivers are driving. Without it
    /// a press puts the steering on its stop and the throttle on the floor in one tick, which at speed is
    /// a lock the tyres cannot hold and a car that will not turn while it is accelerating.
    /// </remarks>
    DriveCommand HandCommand(int car, in CarBuild build, float alongMps, in HandInput hand)
    {
        var was = Cars.Command[car];
        var steerRad = build.WheelWoundTo(was.SteerRad, hand.Steer * build.MaxSteerRad, _config.TickSeconds);
        var reverse = hand.Throttle < 0f && alongMps <= _config.Driving.StopSpeedMps;

        // The gear's own cap, which is a hard rule and stays one whoever is at the wheel — and it is the
        // cap of the car the hand is in (CAR-11), so taking the wheel of a truck feels like a truck.
        var capMps = reverse ? build.ReverseMaxMps : build.MaxSpeedMps;
        var wanted = reverse ? -hand.Throttle : hand.Throttle;
        if (wanted > 0f && MathF.Abs(alongMps) >= capMps) wanted = 0f;

        var askedMps2 = wanted >= 0f
            ? MathF.Max(0f, wanted) * build.AccelerationMps2
            : wanted * build.BrakingMps2;
        var travelMps2 = build.PedalRateMps3 * _config.TickSeconds;
        var pedalMps2 = Math.Clamp(
            askedMps2, CarFollower.PedalMps2(was) - travelMps2, CarFollower.PedalMps2(was) + travelMps2);

        return new DriveCommand(
            steerRad,
            MathF.Max(0f, pedalMps2),
            MathF.Max(0f, -pedalMps2),
            hand.Handbrake,
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
