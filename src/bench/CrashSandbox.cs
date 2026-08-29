using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.Bench;

/// <summary>
/// A town with nothing in it but the bodies a case needs: the crash sandbox, and the one
/// place a staged collision can be aimed at a hundredth of a metre a second.
/// </summary>
/// <remarks>
/// <para>
/// <b>It arbitrates its contacts with the town's own component</b> (<see cref="ContactArbiter"/>) over
/// the town's own arithmetic (<see cref="DamageResolver"/>). A rig with a resolver of its own would
/// prove something about the rig; this one is a different roster handed to the same code, which is what
/// "one component owns this arithmetic" has to mean for a staged case to say anything about the town.
/// </para>
/// <para>
/// <b>Nothing here drives, walks or decides.</b> Bodies coast: a car is given <see cref="DriveCommand.Idle"/>
/// rather than a driver, so its wheels hold it straight and take nothing off its speed, and a walker is
/// given no manoeuvre at all. A rig that ran the followers would be staging a crash against two control
/// loops trying to avoid it. What it does keep is the friction under a <em>terminal</em> body, because
/// that is the ground's and not the agent's — a corpse and a wreck are still slowed by what they lie on.
/// </para>
/// </remarks>
internal sealed class CrashSandbox : ISimWorld, IDamageRoster, IDisposable
{
    readonly SimConfig _config;
    readonly PhysicsWorld _physics;
    readonly Vector2[] _impulseNs;
    readonly WheelImpulse[] _wheelImpulses;
    readonly SurfaceUnderWheel[] _wheelGround;
    readonly TyreScrub[] _wheelScrub;
    readonly Vector2[] _velocityIntoTickMps;

    public CrashSandbox(SimConfig config, int people = 4, int cars = 4)
    {
        _config = config;
        _physics = new PhysicsWorld(config);
        People = new PersonFleet(people);
        // <b>The rig stands the nominal car</b>: every figure `--bench crash` quotes is quoted against
        // `SimConfig`'s own weight, so a rig that stood the fleet would be reporting a variant.
        Cars = new CarFleet(cars, arcsPerCar: 1, CarBuilds.OfTheNominalCar(config, CarCatalog.Shared));
        _impulseNs = new Vector2[people];
        _wheelImpulses = new WheelImpulse[cars * TyreModel.Wheels];
        _wheelGround = new SurfaceUnderWheel[cars * TyreModel.Wheels];
        Array.Fill(
            _wheelGround,
            new SurfaceUnderWheel(
                config.Terrain.PavedCoefficient, config.Terrain.PavedDragMps2,
                config.Marks.PowerM2S3 * config.Terrain.PavedMarkFactor, Ploughs: false));
        _wheelScrub = new TyreScrub[cars * TyreModel.Wheels];
        _velocityIntoTickMps = new Vector2[people + cars];
        PersonOutcome = new DamageOutcome[people];
        CarOutcome = new DamageOutcome[cars];
    }

    public PersonFleet People { get; }

    public CarFleet Cars { get; }

    /// <summary>How many pairs have been judged since the rig was built. One per touch is the rule this counts.</summary>
    public int Judgements { get; private set; }

    /// <summary>
    /// What the last contact did to each body, kept because <b>what a case is about is the contact and
    /// not the wreckage</b>: a body's state afterwards is the outcome plus everything the solver did with
    /// it, and a rig that read the state would be reporting on the tick it happened to stop at.
    /// </summary>
    public DamageOutcome[] PersonOutcome { get; }

    public DamageOutcome[] CarOutcome { get; }

    public AgentRoster Roster => new(People.Count, Cars.Count);

    public int AgentCount => Roster.Count;

    public bool IsTerminal(int agent) => Roster.IsCar(agent) && Cars.Broken[Roster.CarIndex(agent)];

    public bool DecidesEveryTick(int agent) => false;

    public void ReadPlayerInput()
    {
    }

    public void RebuildProximityIndex()
    {
    }

    /// <summary>Nothing here has anywhere to be: a staged body coasts, and only a terminal one is acted on.</summary>
    public void TickAgent(int agent)
    {
    }

    public void DecideAgent(int agent, float sinceLastDecisionS)
    {
    }

    public int AddPerson(Vector2 atM)
    {
        var body = _physics.AddPerson(atM);
        var person = People.Add(
            body, atM, 0f, _physics.MassOf(body), _config.PersonDiameterM * 0.5f, 0, new Rng(1, (ulong)People.Count),
            PersonFleet.DrawsReckless(1, (ulong)People.Count, _config.Driving.RecklessShare));
        _physics.Tag(body, new BodyTag(BodyKind.Person, person));
        return person;
    }

    public int AddCar(Vector2 atM, float headingRad)
    {
        var body = _physics.AddNominalCar(atM, headingRad);
        var car = Cars.Add(body, atM, headingRad, 0, false, new Rng(2, (ulong)Cars.Count));
        _physics.Tag(body, new BodyTag(BodyKind.Car, car));

        // Coasting, not parked: an unmanned car holds its handbrake, and a rig that let it would be
        // measuring a car braking towards the thing it was aimed at.
        Cars.Driven[car] = false;
        Cars.Command[car] = DriveCommand.Idle;
        return car;
    }

    public void AddWall(Vector2 centreM, Vector2 sizeM) => _physics.AddStaticBox(centreM, sizeM, 0f);

    /// <summary>
    /// Sets a body moving, the only way this engine ever sets anything moving: one impulse. The mirror
    /// is written with it so the very next tick's arithmetic sees the speed the case asked for rather
    /// than the zero the body was created at.
    /// </summary>
    public void Launch(BodyTag tag, Vector2 velocityMps)
    {
        if (tag.Kind == BodyKind.Person)
        {
            _physics.ApplyCentralImpulse(People.Body[tag.Index], velocityMps * People.MassKg[tag.Index]);
            People.VelocityMps[tag.Index] = velocityMps;
            return;
        }

        _physics.ApplyCentralImpulse(Cars.Body[tag.Index], velocityMps * Cars.MassKg[tag.Index]);
        Cars.VelocityMps[tag.Index] = velocityMps;

        // <b>A car set moving has its wheels told in the same breath.</b> The rims are state, and four
        // of them left at zero under a body doing 20 m/s are four locked wheels: the case would spend
        // its approach skidding to a halt and arrive at a speed nobody staged.
        Cars.WheelSpinOf(tag.Index).Fill(
            Vector2.Dot(velocityMps, new Vector2(MathF.Cos(Cars.HeadingRad[tag.Index]), MathF.Sin(Cars.HeadingRad[tag.Index]))));
    }

    public void StepBodies(float dtS)
    {
        Settle(dtS);

        for (var person = 0; person < People.Count; person++) _velocityIntoTickMps[person] = People.VelocityMps[person];
        for (var car = 0; car < Cars.Count; car++) _velocityIntoTickMps[Roster.AgentOfCar(car)] = Cars.VelocityMps[car];

        for (var person = 0; person < People.Count; person++)
        {
            _physics.ApplyCentralImpulse(People.Body[person], _impulseNs[person]);
        }

        for (var wheel = 0; wheel < Cars.Count * TyreModel.Wheels; wheel++)
        {
            _physics.ApplyImpulseAt(Cars.Body[wheel / TyreModel.Wheels], _wheelImpulses[wheel].ImpulseNs, _wheelImpulses[wheel].AtM);
        }

        _physics.Step(dtS);

        for (var person = 0; person < People.Count; person++)
        {
            People.PositionM[person] = _physics.PositionOf(People.Body[person]);
            People.VelocityMps[person] = _physics.VelocityOf(People.Body[person]);
        }

        for (var car = 0; car < Cars.Count; car++)
        {
            var was = Cars.VelocityMps[car];
            var now = _physics.VelocityOf(Cars.Body[car]);
            Cars.PositionM[car] = _physics.PositionOf(Cars.Body[car]);
            Cars.HeadingRad[car] = _physics.HeadingOf(Cars.Body[car]);
            Cars.VelocityMps[car] = now;
            Cars.YawRateRadPerS[car] = _physics.YawRateOf(Cars.Body[car]);

            var changeMps = (now - was) / dtS;
            var forward = new Vector2(MathF.Cos(Cars.HeadingRad[car]), MathF.Sin(Cars.HeadingRad[car]));
            Cars.AccelerationMps2[car] = new Vector2(
                Vector2.Dot(changeMps, forward), Vector2.Dot(changeMps, new Vector2(-forward.Y, forward.X)));
        }
    }

    public void ResolveContacts() => Judgements += ContactArbiter.Resolve(_physics, _config, this);

    /// <summary>The town's rule, in a rig: a body that is down takes no actions, and the ground still acts on it.</summary>
    void Settle(float dtS)
    {
        for (var person = 0; person < People.Count; person++)
        {
            if (!People.Wounded[person]) continue;

            var positionM = People.PositionM[person];
            _impulseNs[person] = WalkerFollower.Step(
                _config, People.HeadingRad[person], positionM, People.VelocityMps[person], positionM, moving: false,
                _config.Terrain.PavedCoefficient, onFeet: false, People.MassKg[person], dtS).ImpulseNs;
        }

        Span<Vector2> atM = stackalloc Vector2[TyreModel.Wheels];
        for (var car = 0; car < Cars.Count; car++)
        {
            var pose = new CarPose(
                Cars.PositionM[car], Cars.HeadingRad[car], Cars.VelocityMps[car], Cars.YawRateRadPerS[car],
                Cars.MassKg[car], Cars.AccelerationMps2[car]);
            ref readonly var build = ref Cars.BuildOf(car);
            TyreModel.WheelPointsM(build, pose, atM);
            TyreModel.Step(
                _config, build, pose,
                Cars.Command[car], float.PositiveInfinity, atM,
                _wheelGround.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels), Cars.WheelSpinOf(car), dtS,
                _wheelImpulses.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels),
                _wheelScrub.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels));
        }
    }

    public DamageSubject SubjectOf(BodyTag tag) => tag.Kind switch
    {
        BodyKind.Person => DamageSubject.Person(People.MassKg[tag.Index], People.Wounded[tag.Index]),
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
            case DamageOutcome.Wounded:
                // PHY-5b, the town's own line: a body in the road leaves the traffic's layers on the tick
                // it goes down. A rig that skipped it would stage the pair the town no longer has.
                _physics.PutOnLayer(People.Body[tag.Index], CollisionLayer.Downed);
                People.Wounded[tag.Index] = true;
                People.Walking[tag.Index] = false;
                PersonOutcome[tag.Index] = outcome;
                break;

            case DamageOutcome.Broken:
                Cars.Broken[tag.Index] = true;
                Cars.Driven[tag.Index] = false;
                Cars.Command[tag.Index] = DriveCommand.LockedAt(Cars.Command[tag.Index].SteerRad);
                CarOutcome[tag.Index] = outcome;
                break;
        }
    }

    /// <summary>Nothing to release — see <see cref="World.Town.TownWorld.Dispose"/>, which the rig stands in for.</summary>
    public void Dispose()
    {
    }
}
