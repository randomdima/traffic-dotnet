using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Physics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Town;

/// <summary>
/// Phases 4 and 5: the world stepped with what the actors signalled this tick and read back, then the touches it began
/// arbitrated — the one place damage is decided.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// The walkers wounded and the cars wrecked, kept where each is set and cleared (<see cref="RaiseTheCall"/>,
    /// <see cref="Wreck"/>).
    /// </summary>
    readonly AgentSet _wounded, _wrecked;

    /// <summary>How many walkers are down this instant (<see cref="Agents.Person.Body.PersonFleet.Wounded"/>), off the set kept with the flag.</summary>
    public int Down => _wounded.CountBelow(People.Count);

    /// <summary>How many cars are wrecked this instant (<see cref="CarFleet.Broken"/>), off the set kept with the flag.</summary>
    public int Wrecked => _wrecked.CountBelow(Cars.Count);

    /// <summary>
    /// <b>A car a wreck where it stands and nothing else of it changed</b> — for a staging that puts one down by hand rather
    /// than crashing it (<see cref="Wreck"/> does the rest).
    /// </summary>
    public void BreakWhereItStands(int car)
    {
        Cars.Broken[car] = true;
        _wrecked.Add(car);
        SignalTheShow(car);
    }

    /// <summary>
    /// What a car's lagged acceleration closes on its new reading by in one step of <paramref name="dtS"/>: the same on
    /// every car in the town, and an exponential, which is not a thing to take once a car.
    /// </summary>
    public static float LoadSettledShare(SimConfig config, float dtS) => 1f - MathF.Exp(-dtS / config.Tyre.LoadSettleS);

    /// <summary>
    /// A settled load with each of its two under <paramref name="leastMps2"/> made nought
    /// (<see cref="TyreFigures.LeastSettledLoadMps2"/>), as the device's view pass makes it (<c>world_view.comp</c>).
    /// </summary>
    public static Vector2 AtLeastALoad(Vector2 loadMps2, float leastMps2) => new(
        MathF.Abs(loadMps2.X) < leastMps2 ? 0f : loadMps2.X, MathF.Abs(loadMps2.Y) < leastMps2 ? 0f : loadMps2.Y);

    /// <inheritdoc/>
    /// <remarks>
    /// <b>One step of the world begun and the one before it read</b> (<see cref="WorldLayer"/>, <see cref="WorldLayer.StepsAhead"/>):
    /// the signals the actors wrote this tick cross with it, the device steps while the next tick decides, and nothing of
    /// a body is touched here.
    /// </remarks>
    public void StepBodies(float dtS)
    {
        // Phase 3 ends here, whichever kind of agent it ended on: a town with no car left to decide
        // for never crossed into the cars at all.
        if (Timed)
        {
            Sub.Mark(ref SpentSoFar());
            Sub.Begin();
        }

        _world.Step(dtS);
        if (Timed) Sub.Mark(ref Sub.SolverTicks);

        ReadTheWorld();
    }

    /// <summary>
    /// Phase 5: every pair that began touching in the step just taken, judged once, by the one component
    /// that decides damage. Contacts themselves are the world's — a walker walked into something stops
    /// because it was stopped — and what is decided here is only what the contact <em>did</em>.
    /// </summary>
    public void ResolveContacts() => Touches += ContactArbiter.Resolve(_world.View.TouchesBegun, _config, this);

    /// <summary>
    /// How many pairs have begun touching since the town was stood up. An instrument and nothing else:
    /// a town whose casualty count is zero has either kept its rules or never touched anything, and this
    /// is what tells the two apart.
    /// </summary>
    public long Touches { get; private set; }

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

    public DamageSubject SubjectOf(BodyTag tag) => tag.Kind switch
    {
        BodyKind.Person => DamageSubject.Person(People.MassKg[tag.Index], People.Wounded[tag.Index]),
        BodyKind.Car => DamageSubject.Car(
            Cars.MassKgOf(tag.Index), Cars.Broken[tag.Index], CarCatalog.Shared.UnbreakableOf(Cars.Variant[tag.Index])),
        _ => DamageSubject.Static,
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
        _wrecked.Add(car);
        Cars.Driven[car] = false;
        if (Cars.Action[car] != CarAction.Towed) Enter(car, CarAction.Stand);
        Cars.Command[car] = DriveCommand.LockedAt(Cars.Command[car].SteerRad);

        // A terminal car is never ticked again, so its locks and its wreck are signalled here and kept by the world.
        SignalTheWheel(car);
        SignalTheShow(car);
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
