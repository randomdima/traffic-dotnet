using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Physics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Town;

/// <summary>Phases 4 and 5: every body stepped with the impulses phase 3 wrote down, then the contacts arbitrated — the one place damage is decided.</summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>For the instruments</b>: handed the solver with every impulse of the tick applied and its step not yet taken —
    /// the question a second solver is asked beside it. Nothing that decides anything sets it.
    /// </summary>
    public Action<PhysicsWorld>? BeforeTheSolverSteps { get; set; }

    /// <summary>
    /// <b>For the instruments</b>: handed the town once every impulse of the tick is written and none is yet spent — the
    /// bodies as the step will find them, and what the phase is about to apply (<see cref="WheelImpulsesOf"/>,
    /// <see cref="WalkerImpulseNs"/>, <see cref="RestsParked"/>, <see cref="TheBarPulls"/>). Nothing that decides anything
    /// sets it.
    /// </summary>
    public Action<TownWorld>? BeforeTheImpulses { get; set; }

    /// <summary>For the instruments: the impulse this walker declared this tick, spent at its centre unless it is inside something.</summary>
    public Vector2 WalkerImpulseNs(int person) => _impulseNs[person];

    /// <summary>For the instruments: whether this car rested parked on its last tick, so the bodies' phase spends nothing of its wheels.</summary>
    public bool RestsParked(int car) => _standsParked[car];

    /// <summary>Whether any car in the town is on a tow bar this tick.</summary>
    public bool AnyOnTheBar => _onTheBar > 0;

    /// <summary>
    /// What a car's lagged acceleration closes on its new reading by in one step of <paramref name="dtS"/>: the same on
    /// every car in the town, and an exponential, which is not a thing to take once a car.
    /// </summary>
    public static float LoadSettledShare(SimConfig config, float dtS) => 1f - MathF.Exp(-dtS / config.Tyre.LoadSettleS);

    /// <summary>
    /// <b>The solver's table, for a device stepping it in the town's place</b> (<see cref="ITownDevice.StepBodies"/>): it
    /// is written from the device's step, so every query of it reads as after the town's own.
    /// </summary>
    internal PhysicsWorld PhysicsForTheDevice => _physics;

    /// <inheritdoc/>
    /// <remarks>
    /// <b>Where a device is set, the step is the device's</b> (<see cref="ITownDevice.StepBodies"/>): the town still
    /// settles what slides with nothing driving it — a casualty's slide and a wreck's wheels are the town's whoever ticks
    /// the rest — and keeps the motion into the tick, and the device spends the impulses, steps every body and hands the
    /// fleets, the table and the touches back. Nothing is handed to <see cref="BeforeTheSolverSteps"/> then — the town's
    /// table never holds the tick's impulses.
    /// </remarks>
    public void StepBodies(float dtS)
    {
        // Phase 3 ends here, whichever kind of agent it ended on: a town with no car left to decide
        // for never crossed into the cars at all.
        if (Timed) Sub.Mark(ref SpentSoFar());

        // Stepped already, with the agents' tick (AgentsTicked).
        if (Device is { StepsWithTheTick: true }) return;

        ReadyTheStep(dtS);
        if (Device != null)
        {
            if (Timed) Sub.Begin();
            Device.StepBodies(this, dtS);
            if (Timed) Sub.Mark(ref Sub.SolverTicks);
            return;
        }

        for (var person = 0; person < People.Count; person++)
        {
            if (People.Inside[person].Any) continue;

            _physics.ApplyCentralImpulse(People.Body[person], _impulseNs[person]);
        }

        for (var car = 0; car < Cars.Count; car++)
        {
            // A car standing parked rested its wheels last tick and has had no tick since: four impulses of nothing.
            if (_standsParked[car]) continue;

            var body = Cars.Body[car];
            var wheels = _wheels.ImpulsesOf(car);
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                _physics.ApplyImpulseAt(body, wheels[wheel].ImpulseNs, wheels[wheel].AtM);
            }
        }

        HaulOnTheBars(dtS);

        BeforeTheSolverSteps?.Invoke(_physics);

        // The solver's step, kept apart from the impulse and read-back loops around it: two different
        // measurements (TickParts).
        if (Timed) Sub.Begin();
        _physics.Step(dtS);
        if (Timed) Sub.Mark(ref Sub.SolverTicks);

        var settled = LoadSettledShare(_config, dtS);
        for (var car = 0; car < Cars.Count; car++)
        {
            Cars.PositionM[car] = _physics.PositionOf(Cars.Body[car]);
            Cars.HeadingRad[car] = _physics.HeadingOf(Cars.Body[car]);
            Cars.VelocityMps[car] = _physics.VelocityOf(Cars.Body[car]);
            Cars.YawRateRadPerS[car] = _physics.YawRateOf(Cars.Body[car]);

            // What the four patches spent this tick and nothing else — the impulses are still the ones
            // applied above, since they are not cleared until this car's next tyre step. It is bounded by
            // construction: a tyre cannot push harder than it grips, so a transfer read off one cannot
            // run away and nothing has to cap it.
            var wheels = _wheels.ImpulsesOf(car);
            var spentNs = Vector2.Zero;
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++) spentNs += wheels[wheel].ImpulseNs;
            var heldMps2 = spentNs / (Cars.MassKg[car] * dtS);

            // The direction off the body rather than off the angle: the step reduced this heading a line
            // ago and kept the pair, so turning the angle back into a direction here would be the town's
            // cars' worth of sincos a tick for something already in hand.
            var forward = _physics.RotationOf(Cars.Body[car]);
            var alongAndAcrossMps2 = new Vector2(
                Vector2.Dot(heldMps2, forward), Vector2.Dot(heldMps2, Heading.RightOf(forward)));

            // Lagged, because a car settles onto its springs over a moment rather than snapping.
            Cars.AccelerationMps2[car] = Vector2.Lerp(
                Cars.AccelerationMps2[car], alongAndAcrossMps2, settled);
        }

        for (var person = 0; person < People.Count; person++)
        {
            // A contained body is not simulated, so there is nothing to mirror off it — its pose is its
            // container's business until the container puts it down again.
            if (People.Inside[person].Any) continue;

            var was = People.PositionM[person];
            var now = _physics.PositionOf(People.Body[person]);
            People.PositionM[person] = now;
            People.VelocityMps[person] = _physics.VelocityOf(People.Body[person]);
            People.DistanceWalkedM[person] += (now - was).Length();
        }
    }

    /// <summary>
    /// <b>What the town does before any step</b>: what slides with nothing driving it settled (<see cref="Settle"/>), the
    /// motion into the tick kept for phase 5, and the instruments handed the bodies as the step will find them — at the
    /// start of phase 4, or at the end of phase 3a where the device steps the bodies with the agents' tick
    /// (<see cref="ITownDevice.StepsWithTheTick"/>).
    /// </summary>
    void ReadyTheStep(float dtS)
    {
        Settle(dtS);

        // The motion the step begins from, kept because it is the motion phase 5's arithmetic is about: a contact is
        // reported after the step, by which time the bodies hold the solver's response to it and no longer the cause.
        // A device stepping the bodies keeps it itself, and says each touch's closing speed (Touch.ClosingMps).
        if (Device == null)
        {
            for (var person = 0; person < People.Count; person++) _velocityIntoTickMps[person] = People.VelocityMps[person];
            for (var car = 0; car < Cars.Count; car++) _velocityIntoTickMps[Roster.AgentOfCar(car)] = Cars.VelocityMps[car];
        }

        BeforeTheImpulses?.Invoke(this);
    }

    /// <summary>
    /// Phase 5: every pair that began touching in the step just taken, judged once, by the one component
    /// that decides damage. Contacts themselves are the solver's — a walker walked into something stops
    /// because it was stopped — and what is decided here is only what the contact <em>did</em>.
    /// </summary>
    public void ResolveContacts() => Touches += ContactArbiter.Resolve(_physics, _config, this);

    /// <summary>
    /// How many pairs have begun touching since the town was stood up. An instrument and nothing else:
    /// a town whose casualty count is zero has either kept its rules or never touched anything, and this
    /// is what tells the two apart.
    /// </summary>
    public long Touches { get; private set; }

    /// <summary>
    /// A body that declares nothing is still slowed: a casualty sliding down the road by its own sliding
    /// grip, whatever it is lying on, and a wreck shunted out of a junction by the ground under each of its
    /// wheels — and this is the one place that happens.
    /// </summary>
    /// <remarks>
    /// It also overwrites what the agent wrote on the tick it went down: the impulse arrays are read whole
    /// every tick, so a walker run over mid-stride whose last declaration stayed there would be shoved
    /// along by it for the whole rescue. The aim is the body's own position, which is what keeps a
    /// casualty from turning to face where it had been going.
    /// </remarks>
    void Settle(float dtS)
    {
        for (var person = 0; person < People.Count; person++)
        {
            if (!People.Wounded[person] || People.Inside[person].Any) continue;

            var positionM = People.PositionM[person];
            var step = WalkerFollower.Step(
                _config, People.HeadingRad[person], positionM, People.VelocityMps[person], People.DeclaredMps[person],
                positionM, moving: false, onFeet: false, People.MassKg[person], dtS);
            People.DeclaredMps[person] = step.DesiredMps;
            _impulseNs[person] = step.ImpulseNs;
        }

        for (var car = 0; car < Cars.Count; car++)
        {
            if (!Cars.Broken[car]) continue;

            // <b>A wreck on a hook is not a wreck being dragged</b> (EVA-5). Its front is up on the bar and
            // its back pair is rolling, so the locked block PHY-5 describes is exactly the wrong model for
            // it: run as one, a towed car scrubs four locked tyres against the tow and the pair crawls.
            if (_recovery.OnTheHookOf[car] >= 0) TrailerWheels(car);
            else Tyres(car, PoseOf(car));
        }
    }

    /// <summary>
    /// <b>The two wheels a towed car is left standing on</b> (EVA-5), stepped where every other wheel in the
    /// town is stepped and spending their impulses through the same array — so nothing downstream has to
    /// know that this body's four are two.
    /// </summary>
    /// <remarks>
    /// <b>Which two they are is the tow's own fact</b>: the pair at the far end from the fork, so a car
    /// caught by the tail rolls on the pair it steers with. The lifted pair is cleared rather than left —
    /// the impulses are read whole every tick, so the two a wreck was skidding on when the arm took hold
    /// would go on being spent for the length of the tow.
    /// </remarks>
    void TrailerWheels(int car)
    {
        var down = TowBar.PairOnTheGround(_recovery.HeldByTheTail[car]);
        var up = TowBar.PairInTheAir(_recovery.HeldByTheTail[car]);
        var impulses = _wheels.ImpulsesOf(car);
        impulses.Slice(up, TowBar.Wheels).Clear();
        _wheels.ScrubOf(car).Clear();

        var pose = PoseOf(car);
        ref readonly var build = ref Cars.BuildOf(car);
        var ground = _wheels.GroundUnder(car);
        Span<Vector2> atM = stackalloc Vector2[TowBar.Wheels];
        TowBar.AxleM(build, pose, down, atM);
        for (var wheel = 0; wheel < TowBar.Wheels; wheel++)
        {
            ground[down + wheel] = WheelGrounds.Under(Cars.WheelsOnAWay[car], down + wheel);
        }

        TowBar.Step(
            build, pose, atM, ground.Slice(down, TowBar.Wheels), _config.Evacuator.OnTheTrailerAxleShare,
            _config.TickSeconds, impulses.Slice(down, TowBar.Wheels));

        // These two wheels are rolling, so they turn with the road under them: left standing still, they
        // are handed back to the tyre model as locked wheels on a moving car.
        var alongMps = Vector2.Dot(pose.VelocityMps, pose.Forward);
        for (var wheel = 0; wheel < TowBar.Wheels; wheel++)
        {
            Cars.WheelSpinMps[(car * TyreModel.Wheels) + down + wheel] = alongMps;
        }
    }

    void PutOnChannels(int car, byte channels)
    {
        Cars.Channels[car] = channels;
        _physics.PutOnChannels(Cars.Body[car], channels);
    }

    /// <summary>
    /// <b>A car stood where the plan put it, on the channel of the lane it stands along</b> (PHY-1a): on a bridge it is
    /// on the bridge's from its first step, before its body has been read over the ways it stands on — and over a
    /// crossing those are both levels'. Of the nearest lane on each level, the one within its own width of the car that
    /// runs most nearly the car's way.
    /// </summary>
    void StandOnItsLevel(int car)
    {
        if (!_levelled) return;

        var forward = ForwardOf(car);
        var rearAxleM = CarFollower.RearAxleM(Cars.BuildOf(car), Cars.PositionM[car], forward);
        var level = CityPlan.RoadArrays.Ground;
        var alignedBest = 0f;
        foreach (var each in (ReadOnlySpan<byte>)[CityPlan.RoadArrays.Ground, CityPlan.RoadArrays.Over])
        {
            var lane = _roads.NearestStreetLane(rearAxleM, each, out var alongM);
            if (lane < 0) continue;

            var on = Spline.SampleAt(_roads.ArcsOf(lane), alongM);
            if ((on.PositionM - rearAxleM).Length() > _roads.LaneWidthM[lane]) continue;

            var aligned = MathF.Abs(Vector2.Dot(on.Direction, forward));
            if (aligned <= alignedBest) continue;

            level = each;
            alignedBest = aligned;
        }

        var channels = CityPlan.RoadArrays.ChannelOf(level);
        if (channels != Cars.Channels[car]) PutOnChannels(car, channels);
    }

    /// <summary>
    /// <b>Every tow bar in the town, spent as the pair of impulses it is</b> (EVA-5): the same actuation the
    /// tyres use, at two more points, in the same phase and before the same step. A town with nothing on a
    /// hook pays one branch for the whole of it.
    /// </summary>
    /// <remarks>
    /// <b>It is applied after the wheels and before the step, and that order is the whole of the coupling's
    /// stability.</b> The bar answers the motion the tyres have just asked for rather than the motion of the
    /// tick before, so an evacuator pulling away hauls its load on the tick it pulls away — one tick late,
    /// the tractor leaves and the bar spends the next tick catching the wreck up, which is a tow that
    /// visibly surges.
    /// </remarks>
    void HaulOnTheBars(float dtS)
    {
        if (_onTheBar == 0) return;

        for (var car = 0; car < Cars.Count; car++)
        {
            if (!TheBarPulls(car, dtS, out var bar)) continue;

            _physics.ApplyImpulseAt(bar.Towed, bar.PullNs, bar.EyeM);
            _physics.ApplyImpulseAt(bar.Tractor, -bar.PullNs, bar.HookM);
        }
    }

    /// <summary>
    /// <b>What the bar a tractor tows on spends this tick</b> (EVA-5): the pull at the towed car's eye, and the same
    /// pull backwards at the tractor's hook — or false where this car tows nothing. Read off the fleet's poses, so it
    /// is the same answer before the tick's impulses are spent as after.
    /// </summary>
    public bool TheBarPulls(int car, float dtS, out BarPull bar)
    {
        var towed = _recovery.Towing[car];
        if (towed < 0)
        {
            bar = default;
            return false;
        }

        var tractor = PoseOf(car);
        var trailer = PoseOf(towed);
        var hookM = TowBar.HookM(Cars.BuildOf(car), tractor);
        var eyeM = TowBar.EyeM(
            Cars.BuildOf(towed), trailer, Cars.BuildOf(car).TowReachM, _recovery.HeldByTheTail[towed]);

        var pullNs = TowBar.PullNs(
            TheEndOfTheBar(Cars.Body[car], tractor, hookM), TheEndOfTheBar(Cars.Body[towed], trailer, eyeM),
            _config.Evacuator.HitchSettleS, _config.EvacuatorHitchMostMps2, _config.Evacuator.HitchSideShare,
            trailer.MassKg, dtS);

        bar = new BarPull(Cars.Body[towed], pullNs, eyeM, Cars.Body[car], hookM);
        return true;
    }

    /// <summary>
    /// One end of a tow bar, gathered off the body it is bolted to: where it is, how fast that point is
    /// moving — the body's own motion plus what its yaw carries that point round by — and what an impulse
    /// there is worth against that body's mass and inertia.
    /// </summary>
    HookEnd TheEndOfTheBar(BodyId body, in CarPose pose, Vector2 atM)
    {
        var armM = atM - pose.PositionM;
        return new HookEnd(
            atM,
            pose.VelocityMps + (pose.YawRateRadPerS * new Vector2(-armM.Y, armM.X)),
            armM,
            _physics.InverseMassOf(body),
            _physics.InverseInertiaOf(body));
    }

    public DamageSubject SubjectOf(BodyTag tag) => tag.Kind switch
    {
        BodyKind.Person => DamageSubject.Person(People.MassKg[tag.Index], People.Wounded[tag.Index]),
        BodyKind.Car => DamageSubject.Car(
            Cars.MassKg[tag.Index], Cars.Broken[tag.Index], CarCatalog.Shared.UnbreakableOf(Cars.Variant[tag.Index])),
        _ => DamageSubject.Static,
    };

    public Vector2 VelocityIntoTickMps(BodyTag tag) => tag.Kind switch
    {
        BodyKind.Person => _velocityIntoTickMps[tag.Index],
        BodyKind.Car => _velocityIntoTickMps[Roster.AgentOfCar(tag.Index)],
        _ => Vector2.Zero,
    };

    public void Apply(BodyTag tag, DamageOutcome outcome)
    {
        switch (outcome)
        {
            case DamageOutcome.Wounded:
                // Off its feet and down in the road from this moment (PER-18), taking no actions until an
                // ambulance has been and got it — so the impulse of the impact carries it and it stays where
                // that leaves it.
                RaiseTheCall(tag.Index);
                break;

            case DamageOutcome.Broken:
                Wreck(tag.Index);
                break;
        }
    }

    /// <summary>
    /// A car in its terminal state: never driven again, on no lane, holding no junction, and with all
    /// four wheels locked <b>where the crash left them pointing</b> so what is left of it skids rather
    /// than rolls (PHY-5).
    /// </summary>
    /// <remarks>
    /// Letting the junction go is the load-bearing line. A wreck that kept its claim would hold a
    /// box shut against every car that has to cross it, for the rest of the run — a jam made by the
    /// bookkeeping rather than by the crash.
    /// </remarks>
    void Wreck(int car)
    {
        if (Cars.Broken[car]) return;

        // EVA-1: the wreck is a recovery from the tick it becomes one, raised where it happens so that
        // nothing has to search the fleet for one.
        RaiseTheRecovery(car);

        // CTL-4: a terminal unit takes no orders, so it holds none either — a wreck the interface still
        // called the player's would read as a car waiting to be told what to do next.
        _carOrders.Release(car);
        Cars.Broken[car] = true;
        Cars.Driven[car] = false;
        if (Cars.Action[car] != CarAction.Towed) Enter(car, CarAction.Stand);
        Cars.Command[car] = DriveCommand.LockedAt(Cars.Command[car].SteerRad);
        Cars.Hold[car] = DrivingHold.None;
        Cars.SetLine(car, default);
        Cars.Reroutes[car] = 0;
        _driveProgress.Restart(car);

        // An ambulance's stretcher is emptied into the road before anything else: a casualty inside a
        // wreck is a person nothing will ever come for again (AMB-7).
        if (Cars.Ambulance[car]) SpillTheAmbulance(car);

        // SRV-4: an evacuator breaks like anything else, and a broken one drops what it was pulling where
        // it stands. The truck is a call now and the wreck behind it is a call again, which is the state
        // EVA-8 already puts a haul in that could not get through.
        if (IsAnEvacuator(car)) LoseTheEvacuator(car);

        // PHY-6: the driver goes down on the road beside their own door. The bay they were going to is
        // given back on the way, since nothing will arrive there now.
        GiveUpTheBay(car);
        ThrowTheDriverClear(car);
    }
}

/// <summary>One tow bar's tick (<see cref="TownWorld.TheBarPulls"/>): <see cref="PullNs"/> at the towed car's eye, and its opposite at the tractor's hook.</summary>
internal readonly record struct BarPull(BodyId Towed, Vector2 PullNs, Vector2 EyeM, BodyId Tractor, Vector2 HookM);
