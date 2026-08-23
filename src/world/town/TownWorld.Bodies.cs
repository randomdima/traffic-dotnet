using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.World.Physics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Town;

/// <summary>Phases 4 and 5: every body stepped with the impulses phase 3 wrote down, then the contacts arbitrated — the one place damage is decided.</summary>
internal sealed partial class TownWorld
{
    public void StepBodies(float dtS)
    {
        // Phase 3 ends here, whichever kind of agent it ended on: a town with no car left to decide
        // for never crossed into the cars at all.
        if (Timed)
        {
            if (_decidingCars) Sub.Mark(ref Sub.CarTicks);
            else Sub.Mark(ref Sub.WalkerTicks);
        }

        Settle(dtS);

        // The motion every decision this tick was taken against, kept because it is also the motion
        // phase 5's arithmetic is about: a contact is reported after the step, by which time the
        // bodies hold the solver's response to it and no longer the cause.
        for (var person = 0; person < People.Count; person++) _velocityIntoTickMps[person] = People.VelocityMps[person];
        for (var car = 0; car < Cars.Count; car++) _velocityIntoTickMps[Roster.AgentOfCar(car)] = Cars.VelocityMps[car];

        for (var person = 0; person < People.Count; person++)
        {
            if (People.Inside[person].Any) continue;

            _physics.ApplyCentralImpulse(People.Body[person], _impulseNs[person]);
        }

        for (var car = 0; car < Cars.Count; car++)
        {
            var body = Cars.Body[car];
            var wheels = _wheels.ImpulsesOf(car);
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                _physics.ApplyImpulseAt(body, wheels[wheel].ImpulseNs, wheels[wheel].AtM);
            }
        }

        // The solver's step, kept apart from the impulse and read-back loops around it: two different
        // measurements (TickParts).
        if (Timed) Sub.Begin();
        _physics.Step(dtS);
        if (Timed) Sub.Mark(ref Sub.SolverTicks);

        // Both are the same on every car in the town and neither moves inside a tick — and the second is
        // an exponential, which is not a thing to take once a car.
        var loadCapMps2 = LoadTransferCapMps2;
        var settled = 1f - MathF.Exp(-dtS / _config.Tyre.LoadSettleS);
        for (var car = 0; car < Cars.Count; car++)
        {
            var was = Cars.VelocityMps[car];
            var now = _physics.VelocityOf(Cars.Body[car]);
            Cars.PositionM[car] = _physics.PositionOf(Cars.Body[car]);
            Cars.HeadingRad[car] = _physics.HeadingOf(Cars.Body[car]);
            Cars.VelocityMps[car] = now;
            Cars.YawRateRadPerS[car] = _physics.YawRateOf(Cars.Body[car]);

            // In the body's own frame, because that is the frame the loads move in: what the car did,
            // not what the pedals asked for, which is the difference between a fact and a fudge — so a
            // kerb pitches the car exactly the way a hard brake does, with no separate path.
            var changeMps = (now - was) / dtS;

            // The direction off the body rather than off the angle: the step reduced this heading a line
            // ago and kept the pair, so turning the angle back into a direction here would be the town's
            // cars' worth of sincos a tick for something already in hand.
            var forward = _physics.RotationOf(Cars.Body[car]);
            var measuredMps2 = new Vector2(
                Vector2.Dot(changeMps, forward), Vector2.Dot(changeMps, Heading.RightOf(forward)));

            // Capped at what the tyres could plausibly have caused, because anything beyond that is a
            // collision and a collision must not be read as a manoeuvre; then lagged, because a car
            // settles onto its springs over a moment rather than snapping.
            Cars.AccelerationMps2[car] = Vector2.Lerp(
                Cars.AccelerationMps2[car], Limit(measuredMps2, loadCapMps2), settled);
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
            if (People.OffFeetForS[person] > 0f) People.OffFeetForS[person] = MathF.Max(0f, People.OffFeetForS[person] - dtS);
        }
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
    /// A terminal body performs no actions and is never handed a tick, but friction is the ground's and
    /// not the body's: a corpse sliding down the road and a wreck shunted out of a junction are both
    /// slowed by what they are lying on, and this is the one place that happens.
    /// </summary>
    /// <remarks>
    /// It also overwrites what the agent wrote on the tick it died: the impulse arrays are read whole
    /// every tick, so a walker killed mid-stride whose last declaration stayed there would be shoved
    /// along by it for the rest of the run.
    /// </remarks>
    void Settle(float dtS)
    {
        for (var person = 0; person < People.Count; person++)
        {
            if (!People.Dead[person]) continue;

            var positionM = People.PositionM[person];
            _impulseNs[person] = WalkerFollower.Step(
                _config, People.HeadingRad[person], positionM, People.VelocityMps[person], positionM, moving: false,
                _terrain.At(positionM).Coefficient, onFeet: false, People.MassKg[person], dtS).ImpulseNs;
        }

        for (var car = 0; car < Cars.Count; car++)
        {
            if (Cars.Broken[car]) Tyres(car, PoseOf(car));
        }
    }

    public DamageSubject SubjectOf(BodyTag tag) => tag.Kind switch
    {
        BodyKind.Person => DamageSubject.Person(People.MassKg[tag.Index], People.Dead[tag.Index]),
        BodyKind.Car => DamageSubject.Car(Cars.MassKg[tag.Index], Cars.Broken[tag.Index]),
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
            case DamageOutcome.Shaken:
                // Knocked about, every faculty kept, off its feet for the stumble window — which leaves
                // the impulse of the impact visible after the impact is over. Running clear of the car
                // that hit it belongs to the manoeuvre catalogue and is unbuilt.
                People.OffFeetForS[tag.Index] = _config.Person.StumbleWindowS;
                break;

            case DamageOutcome.Dead:
                People.Dead[tag.Index] = true;
                People.Walking[tag.Index] = false;
                break;

            case DamageOutcome.Broken:
                Wreck(tag.Index);
                break;
        }
    }

    /// <summary>
    /// A car in its terminal state: never driven again, on no lane, holding no junction, and with all
    /// four wheels locked so what is left of it skids rather than rolls.
    /// </summary>
    /// <remarks>
    /// Letting the junction go is the load-bearing line. A wreck that kept its reservation would hold a
    /// box shut against every car that has to cross it, for the rest of the run — a jam made by the
    /// bookkeeping rather than by the crash.
    /// </remarks>
    void Wreck(int car)
    {
        Cars.Broken[car] = true;
        Cars.Driven[car] = false;
        Cars.Command[car] = DriveCommand.Locked;
        Cars.Hold[car] = DrivingHold.None;
        Cars.Line[car] = default;
        LeaveTheCatalogue(car);
        RestTheLadder(car);
        DropTheMovement(car);

        // The driver is unaffected and gets out at once (`E-10`): a broken car cannot be driven again.
        // The bay it was going to is given back on the way, since nothing will arrive there now.
        _parking.GiveUpReservation(car);
        var driver = _containers.DriverOf(car);
        if (driver >= 0) People.Stage[driver] = TripStage.Alighting;
    }
}
