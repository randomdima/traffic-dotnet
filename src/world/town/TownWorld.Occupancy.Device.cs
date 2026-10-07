using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A claims rebuild a device lays</b> (<see cref="ITownClaims"/>): <see cref="RebuildLaneOccupancy"/> stage
/// for stage, the town laying here only what it alone lays — every walker stationed, the passes, the claims laid where
/// they stand, a car on a pass's plan past it, the backing and every grant read — and the device every body, light, plan
/// and walk, and every answer, handed back into the reservations before the backing is laid.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same rebuild, not a likeness of it</b>: what the town lays it lays with its own code in its own order, and what
/// the device writes back is what the town's own laying would have left — the reservations, every car's plan and hold,
/// every walker's hold, the channels each car stands on, and how the answers went.
/// </para>
/// <para>
/// <b>A car on a pass plans as though its pass were clear</b> (<see cref="Following.AsksTheBodiesLater"/>): whether a body
/// stands inside it is asked of the device's bodies, and where one does its plan is the hold short of it.
/// </para>
/// <para>
/// <b>The instruments' watch is not handed this rebuild</b> (<see cref="WatchingTheClaims"/>): what it reads of each stage
/// is the town's own laying, and the bodies, lights, plans and walks are not laid here.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    void RebuildLaneOccupancyOn(ITownClaims device)
    {
        device.ClaimsAt(this, ClaimsStage.Begun);
        _occupancy.Begin();
        for (var person = 0; person < People.Count; person++) StationTheWalker(person);

        device.ClaimsAt(this, ClaimsStage.Stationed);
        for (var person = 0; person < People.Count; person++)
        {
            if (!People.Inside[person].Any) HoldTheWalkersGround(person, out _);
        }

        device.ClaimsAt(this, ClaimsStage.WalkersLaid);
        AskTheCars();
        device.ClaimsAt(this, ClaimsStage.CarsLaid);
        LayThePasses();
        device.ClaimsAt(this, ClaimsStage.PassesLaid);
        device.ClaimsAt(this, ClaimsStage.PlansDue);
        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        foreach (var car in CarsAsked)
        {
            if (!PlansDownItsLine(car)) PlanTheDrive(car, ways);
        }

        device.ClaimsAt(this, ClaimsStage.DrivesDue);
        _following.AsksTheBodiesLater = true;
        foreach (var car in CarsAsked)
        {
            if (!PlansDownItsLine(car)) continue;

            if (Cars.Pass[car].Begun) PlanTheDrive(car, ways);
            else _ground.ClearTheClaim(car);
        }

        _following.AsksTheBodiesLater = false;
        device.ClaimsAt(this, ClaimsStage.WalksDue);
        device.ClaimsAt(this, ClaimsStage.PlansLaid);
        device.ClaimsAt(this, ClaimsStage.PlansAnswered);
        foreach (var car in CarsAsked) _backingUp.Lay(car, IsUnderWay(car));
        foreach (var car in CarsAsked) _ground.ReadTheGrant(car);
        device.ClaimsAt(this, ClaimsStage.WalkersDue);
        ReadTheWalkersGrants();
        device.ClaimsAt(this, ClaimsStage.GrantsRead);
    }

    /// <summary>
    /// <b>Whether this rebuild lays ground only the town's own laying can answer</b>: what a hand or a truck cannot stop
    /// short of, answered against the bodies and taking a light's stretch before any plan; and what stands in a manoeuvre
    /// begun at a bay, read off the bodies before the plans. A device asks this before it lays the claims.
    /// </summary>
    public bool LaysWhatOnlyTheTownAnswers()
    {
        var stopMps = _config.Driving.StopSpeedMps;
        for (var car = 0; car < Cars.Count; car++)
        {
            if (Cars.Action[car] is CarAction.Hand or CarAction.Towed && Cars.VelocityMps[car].LengthSquared() > stopMps * stopMps) return true;
            if (IsManoeuvring(car) && _manoeuvres.IsBegun(car) && Cars.Driven[car] && !Cars.Broken[car]) return true;
        }

        return false;
    }

    /// <summary>Where a car on a pass looks for a body inside it, for a device laying the bodies (<see cref="Following.AsksOfItsPass"/>).</summary>
    public int BodyAsksOfItsPass(int car, Span<BodyAsk> into) => _following.AsksOfItsPass(car, into);

    /// <summary>A car's plan down its line as a device laid it (<see cref="Following.Plan"/>): how far it reaches, what it is committed to, and its horizon.</summary>
    public void PlannedOnTheDevice(int car, float claimToM, float committedToM, float horizonM)
    {
        Cars.ClaimToM[car] = claimToM;
        Cars.CommittedToM[car] = committedToM;
        Cars.HorizonM[car] = horizonM;
    }

    /// <summary>
    /// The hold a car's claim laid this rebuild, as a device numbered it — and, where a device found a body inside its pass,
    /// held short of it with nothing granted until its grant is read (<see cref="DrivingGround.ClearTheClaim"/>).
    /// </summary>
    public void HeldOnTheDevice(int car, int hold, bool inItsPass)
    {
        _ground.PlanHold[car] = hold;
        if (inItsPass) Cars.AuthorityM[car] = 0f;
    }

    /// <summary>
    /// The hold a walker's walk laid this rebuild, as a device numbered it — and, where the device committed it to a
    /// straight, the same straight committed here (<see cref="WalkingGround.TheStraightAhead"/>).
    /// </summary>
    /// <returns>False where the device committed the walker to a straight and the town's own walk would not have.</returns>
    public bool WalkedOnTheDevice(int person, int hold, bool committedAStraight)
    {
        _walkingGround.WalkHold[person] = hold;
        if (!committedAStraight) return true;

        var (fromM, toM) = (_walkingGround.StraightFromM(person), _walkingGround.StraightToM(person));
        Span<LineWay> walk = stackalloc LineWay[WalkingGround.MostWaysAlongAWalk];
        WaysOfTheWalk(person, walk);
        return !Same(_walkingGround.StraightFromM(person), fromM) || !Same(_walkingGround.StraightToM(person), toM);

        // A straight never committed to is walked to no number, which is the same as itself here.
        static bool Same(System.Numerics.Vector2 one, System.Numerics.Vector2 other) => one.X.Equals(other.X) && one.Y.Equals(other.Y);
    }

    /// <summary>
    /// What a device read under a car's wheels when it laid its body (<see cref="LayTheCarsBody"/>): the channels of the
    /// ways they stand on, put on where they differ, and which of them stand on one, kept for its tyres.
    /// </summary>
    public void OnTheGroundOfItsBody(int car, byte channels, byte wheelsOnAWay)
    {
        if (channels != Cars.Channels[car]) PutOnChannels(car, channels);
        Cars.WheelsOnAWay[car] = wheelsOnAWay;
    }

    /// <summary>How a device's answering of the plans went (<see cref="AnswerThePlans"/>): answers moved past the first pass, and whether they settled.</summary>
    public void AnsweredOnTheDevice(int laidAgain, bool settled)
    {
        PlansLaidAgain += laidAgain;
        if (!settled) Unsettled++;
    }
}
