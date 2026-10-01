using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;
using static TrafficSimulation.World.Road.LineWays;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>Follow</b> (CAR-15): the route's own lanes, driven on the ground planned down them — and handed over where the
/// line stops for its bay and the car is near enough to shape its way in (GEN-4f), or where what ended its grant is
/// something it has decided to get past (CAR-46). <b>Its claim is a plan down the car's own line</b> (TER-4c.1): asked
/// for from the nose as far as the car means to be able to stop, had in part, and driven to where it was cut — the
/// one claim the other actions on the route's line share with it (TER-4c.8).
/// </summary>
internal sealed class Following(DrivingGround ground, CarActions actions, Overtaking overtaking, ParkingIn parkingIn)
{
    CarFleet Cars => ground.Cars;

    LaneOccupancy Occupancy => ground.Occupancy;

    SimConfig Config => ground.Config;

    /// <summary>
    /// <b>This tick of a car following its route</b>, once the line under it has been read: onto a pass or backing up
    /// for the room to, or on down the line on the ground it was granted. Into its bay's manoeuvre is decided on the
    /// car's own clock (<see cref="Decide"/>).
    /// </summary>
    public void Tick<TTown>(ref TTown town, int car, in CarPose pose, float progressM, float alongMps, float coveredM)
        where TTown : struct, ICarTown
    {
        // CAR-46: something ended the grant that the car may get past. Slowed for gently from the first, and — where
        // the car has come to where it would begin slowing for it and the road lets it — decided on, once.
        if (!overtaking.MayGetPastWhatCutIt(car, out var cutBy, out var cutOn))
        {
            town.DriveOnTheLine(car, pose, progressM, alongMps, coveredM);
            return;
        }

        if (!overtaking.ComesUpTo(car, alongMps, Cars.AuthorityM[car] - coveredM)
            || !overtaking.Decide(car, progressM, alongMps, town.ToTheSceneM(car), cutBy, cutOn))
        {
            town.DriveOnTheLine(car, pose, progressM, alongMps, coveredM, waitsToPass: true);
            return;
        }

        actions.Enter(car, overtaking.BacksUp(car, progressM, alongMps) ? CarAction.BackUp : CarAction.Overtake);
        overtaking.WaitAtTheStep(ref town, car, pose, progressM, alongMps, coveredM);
    }

    /// <summary>
    /// <b>A car following its route, on its own clock</b>: where the line stops for its bay and the car is near enough,
    /// its manoeuvre in shaped from where it stands and asked for (GEN-4f).
    /// </summary>
    public void Decide(int car)
    {
        if (Cars.StopsForBayOf(car) is var bay and not CarFleet.NoBay
            && parkingIn.TakeUpTheBay(car, bay, Cars.ProgressM[car], Cars.AlongMps[car]))
        {
            actions.Enter(car, CarAction.Park);
        }
    }

    /// <summary>
    /// <b>The road this car plans to use</b>, laid as one hold from its nose down the ways of its own line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>How far it reaches</b> is what the car could not stop short of, what it takes to pull away from where
    /// it stands, and — while it is moving — what it takes to reach the speed it is planning for, hold it a
    /// while and stop from there (TER-5g), those two no further than the corners ahead let it be at rest
    /// (<see cref="CornerLimits.RestToM"/>); never past the end of its line, nor past how far a plan may reach
    /// (<see cref="HorizonToM"/>), where the car then drives to stop by its end. <b>A call means further</b>
    /// (<see cref="ReachShare"/>, AMB-4.5), all but what it can no longer stop short of. <b>A light is not asked
    /// here</b>: its hold is ground on the way, and the plan is answered against it (TLT-1). <b>A body that is not
    /// moving states nothing beyond the first two</b>, so a queue waiting at a junction plans none of the box it
    /// is waiting for.
    /// </para>
    /// <para>
    /// <b>The ground it can no longer stop short of is committed</b> (<see cref="ClaimPriority.Committed"/>):
    /// nothing takes it. <b>Past that it plans at its movement's rung</b> (TER-5e), levelled so it never
    /// grows along the hold (TER-5g.1): the approach to a box is worth what the box is, and a movement taken
    /// after a weaker one is worth no more than that.
    /// </para>
    /// <para>
    /// <b>A car inside a box keeps its way through it</b>: it plans at least to the far side of the join it
    /// is on, however slowly it is going, so a body in the middle of a junction is never refused the ground
    /// between itself and the way out.
    /// </para>
    /// <para>
    /// <b>It is answered before it is laid</b> (<see cref="LaneOccupancy.Reach"/>): how far each piece can
    /// be had is read first, and only then is anything taken — so ground is never taken off another plan for
    /// a hold that then does not use it.
    /// </para>
    /// <para>
    /// <b>A car on a pass plans past it</b> (TER-4c.6, <see cref="Overtaking.PlannedFromM"/>): the pass is already its
    /// own, and what it passes stands on its line inside it.
    /// </para>
    /// </remarks>
    /// <param name="keptOffM">The ground the car was kept off what cut it the rebuild before.</param>
    public void Plan(int car, float keptOffM, Span<LineWay> ways)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var noseM = Cars.ClaimFromM[car];
        var brakingMps2 = CarFollower.BrakingMps2(Config, build, Cars.GroundCoefficient[car]);
        var alongMps = MathF.Max(0f, Cars.AlongMps[car]);
        var plannedMps = Cars.PlannedMps[car];
        var standOffM = Config.Driving.StandOffM;

        var committedM = (alongMps * Config.CarReactionS) + DrivingGround.StoppingM(alongMps, brakingMps2);

        // Bounded by the pedal: from a standstill, what a reaction interval of the car's own acceleration
        // reaches and a stop from there — the room to pull away, and nothing it could not have driven over — and
        // past that the ground it was last kept off what cut it, so a car standing off something with room to step
        // round it (CAR-46) keeps it in its plan rather than creeping up until it is back in reach.
        var reachableMps = MathF.Min(plannedMps, alongMps + (build.AccelerationMps2 * Config.CarReactionS));
        var pullsAwayM = (reachableMps * Config.CarReactionS) + DrivingGround.StoppingM(reachableMps, brakingMps2)
                         + MathF.Max(standOffM, keptOffM);

        var moving = alongMps > Config.Driving.StopSpeedMps;
        var meansM = moving ? MeansToM(alongMps, plannedMps, build.AccelerationMps2, brakingMps2) + standOffM : 0f;

        // <b>And while it is moving, never past where the corners ahead let it be at rest</b>: the run holds the speed
        // the car is planning for, which on the approach to a bend is the approach's and not the bend's.
        var meantM = MathF.Max(pullsAwayM, meansM);
        if (moving)
        {
            meantM = MathF.Min(
                meantM, CornerLimits.RestToM(Cars.LineOf(car), Cars.EntriesOf(car), noseM, Config) - noseM + standOffM);
        }

        var wantedM = MathF.Max(committedM, meantM * ReachShare(car));

        // <b>The line is the axle's, and the plan the nose's</b> (CAR-4a): a car brought to rest at its line's end has
        // its nose that much further on, over ground it has to hold to get there.
        var lengthM = Cars.Line[car].LengthM + ground.LeadingEdgeAheadOfTheAxleM(car);
        var wantedToM = MathF.Min(noseM + wantedM, lengthM);
        var committedToM = noseM + committedM;

        // <b>A car on a pass plans from where it is back in its own lane</b> (TER-4c.6): the ground up to there
        // is its pass's, laid as a body, and only a body standing inside it can end it short.
        var planFromM = overtaking.PlannedFromM(car);

        // <b>No further than a plan may reach</b>, and never short of what the car can no longer stop short of.
        var planToM = MathF.Min(MathF.Max(committedToM, MathF.Min(wantedToM, HorizonToM(car, planFromM, build))), lengthM);

        // <b>A box is ground a car cannot give back once it cannot stop short of the mouth</b>: it is going in, so
        // the whole of the join is committed. A car already in one plans the rest of the join and its own length
        // of the way out, however slowly it is going — a body standing in a box is what everything crossing it is
        // waiting on — and holds it at its movement's rung, since a car refused inside a box can still stop there.
        if (TheNextBox(car, noseM, out var mouthM, out var boxEndsAtM) && noseM >= mouthM)
        {
            planToM = MathF.Max(planToM, MathF.Min(boxEndsAtM + build.LengthM + standOffM, lengthM));
        }

        Cars.ClaimToM[car] = planToM;
        Cars.CommittedToM[car] = committedToM;
        if (planToM < wantedToM) Cars.HorizonM[car] = planToM - noseM;

        if (Cars.Pass[car].Begun && overtaking.TheBodyInThePass(car, out var inTheWayM, out var inTheWay, out var on))
        {
            var held = Occupancy.BeginHold(standOffM);
            ground.PlanHold[car] = held;
            Occupancy.EndHold(held, inTheWayM, standOffM, inTheWay, on);
            return;
        }

        // A pass that reaches past all the car means to plan is the whole of its ground, and nothing in it.
        if (planToM <= planFromM)
        {
            if (Cars.Pass[car].Begun) Cars.AuthorityM[car] = float.PositiveInfinity;
            return;
        }

        var hold = Occupancy.BeginHold(standOffM);
        ground.PlanHold[car] = hold;

        var count = ground.WaysAlong(car, planFromM, planToM, ways);
        Span<ClaimPriority> rungs = stackalloc ClaimPriority[count];
        LevelTheRungs(car, ways[..count], rungs);

        LayTheDrive(car, hold, ways[..count], rungs, AnswerTheDrive(car, hold, ways[..count], rungs));
    }

    /// <summary>
    /// <b>This car's plan answered again against what every other came to</b>, and laid again where the answer has
    /// moved — true where it did.
    /// </summary>
    public bool Settle(int car, Span<LineWay> ways)
    {
        // <b>Only a plan down the route's line is</b> (CAR-15b): a manoeuvre's hold answered again as one handed a car
        // waiting for its ground the whole of a piece it has no ways along, and it drove it.
        var hold = ground.PlanHold[car];
        if (hold == LaneOccupancy.NoHold || !actions.DrivesTheRoute(car)) return false;

        var endsAtM = Occupancy.HoldEndsAtM(hold, out _, out var cutBy);
        if (float.IsPositiveInfinity(endsAtM) || (cutBy.Found && cutBy.HasBody)) return false;

        var count = ground.WaysAlong(car, overtaking.PlannedFromM(car), Cars.ClaimToM[car], ways);
        Span<ClaimPriority> rungs = stackalloc ClaimPriority[count];
        LevelTheRungs(car, ways[..count], rungs);

        var answer = AnswerTheDrive(car, hold, ways[..count], rungs);
        if (answer.CutLineM == endsAtM) return false;

        Occupancy.ReopenHold(hold);
        LayTheDrive(car, hold, ways[..count], rungs, answer);
        return true;
    }

    /// <summary>
    /// <b>How far this car's plan can be had</b>, read against the reservations as they stand and never
    /// written: cut where the first of its ways refuses it, and short of that by what it keeps off what refused
    /// it (<see cref="Overtaking.KeptOffM"/>).
    /// </summary>
    PlanAnswer AnswerTheDrive(int car, int hold, ReadOnlySpan<LineWay> ways, ReadOnlySpan<ClaimPriority> rungs)
    {
        for (var index = 0; index < ways.Length; index++)
        {
            ref readonly var way = ref ways[index];
            var reachM = Occupancy.Reach(AskOn(car, hold, way, rungs[index]), way.Way, way.ToM, way.FromM, out var cutBy);
            if (reachM >= way.ToM) continue;

            return new PlanAnswer(
                OnTheLineM(way, reachM), overtaking.KeptOffM(car, cutBy, way.Way), cutBy, way.Way, index, reachM);
        }

        return PlanAnswer.Whole;
    }

    /// <summary><b>A car's plan laid</b> over what its answer left, and finished with what it came to.</summary>
    void LayTheDrive(
        int car, int hold, ReadOnlySpan<LineWay> ways, ReadOnlySpan<ClaimPriority> rungs, in PlanAnswer answer)
    {
        for (var index = 0; index < ways.Length; index++)
        {
            ref readonly var way = ref ways[index];
            if (way.LineFromM >= answer.CutLineM) break;

            Occupancy.Take(AskOn(car, hold, way, rungs[index]), way.Way, LaidToM(answer, index, way));
            if (index == answer.CutAt) break;
        }

        Occupancy.EndHold(hold, answer.CutLineM, answer.MarginM, answer.CutBy, answer.CutOn);
    }

    /// <summary>
    /// <b>Where a car means to be able to stop</b> (TER-4c.1): as far as it gets in the planned run
    /// (<see cref="DrivingFigures.PlannedRunS"/>) pulling up to the speed it is planning for at its own
    /// acceleration, and a stop from what that leaves it doing.
    /// </summary>
    /// <remarks>
    /// <b>What the run reaches and not what the planned speed would</b>: a car pulling away with an empty
    /// street ahead plans for its top speed, and the whole climb to it held as ground was a quarter of a
    /// kilometre and three junctions shut against the traffic crossing them by a car doing walking pace.
    /// </remarks>
    float MeansToM(float alongMps, float plannedMps, float accelerationMps2, float brakingMps2)
    {
        var runS = Config.Driving.PlannedRunS;
        if (plannedMps <= alongMps) return (plannedMps * runS) + DrivingGround.StoppingM(plannedMps, brakingMps2);

        var climbS = MathF.Min(runS, (plannedMps - alongMps) / accelerationMps2);
        var reachedMps = alongMps + (accelerationMps2 * climbS);
        var coveredM = (alongMps * climbS) + (0.5f * accelerationMps2 * climbS * climbS) + (reachedMps * (runS - climbS));
        return coveredM + DrivingGround.StoppingM(reachedMps, brakingMps2);
    }

    /// <summary>
    /// <b>How far a plan begun at <paramref name="fromM"/> may reach</b> (TER-4c.1): no further than any plan
    /// reaches (<see cref="DrivingFigures.PlanMostM"/>), nor than this car could stop from its own top
    /// speed, nor into the join past the last one it may pass that breaks its line
    /// (<see cref="DrivingFigures.PlanMostJoins"/>) — each of them <see cref="ReachShare"/> times over
    /// for a call (AMB-4.5).
    /// </summary>
    /// <remarks>
    /// <b>A join the nose is already past the mouth of is not counted</b>: the car is in it, and a car in a box
    /// plans its way out of it whatever this says.
    /// </remarks>
    float HorizonToM(int car, float fromM, in CarBuild build)
    {
        var reach = ReachShare(car);
        var toM = fromM + (MathF.Min(Config.Driving.PlanMostM, build.SightM) * reach);
        var ends = Cars.LaneEndsOf(car);
        var breaks = Cars.JoinBreaksOf(car);
        var mostJoins = (int)MathF.Round(Config.Driving.PlanMostJoins * reach);
        var passed = 0;
        for (var slot = 0; slot + 1 < Cars.Line[car].LaneCount && ends[slot] < toM; slot++)
        {
            if (ends[slot] <= fromM || !breaks[slot]) continue;
            if (++passed > mostJoins) return ends[slot];
        }

        return toM;
    }

    /// <summary>
    /// <b>How many times further than any other driver this car plans</b> (AMB-4.5): a call's reach, and one for
    /// everybody else.
    /// </summary>
    float ReachShare(int car) => Cars.BlueLight[car] ? Config.Ambulance.CallReachShare : 1f;

    /// <summary>One piece of a car's plan as the terms it is asked on.</summary>
    PlannedAsk AskOn(int car, int hold, in LineWay way, ClaimPriority rung) =>
        new(
            hold, car, LaneRoster.Driving, rung, way.FromM, way.LineFromM, way.LineFromM - Cars.ClaimFromM[car],
            way.FromM + (Cars.CommittedToM[car] - way.LineFromM), Cars.AlongMps[car]);

    /// <summary>
    /// <b>The rung each piece of a plan is held at</b> (TER-5e, TER-5g.1): the movement's own on a join, and
    /// on a lane the rung of the next movement the plan makes — never stronger than anything the plan holds
    /// before it. A car on a call holds all of it at a call's (AMB-4).
    /// </summary>
    void LevelTheRungs(int car, ReadOnlySpan<LineWay> ways, Span<ClaimPriority> rungs)
    {
        var next = ClaimPriority.Firm;
        for (var index = ways.Length - 1; index >= 0; index--)
        {
            if (ground.Ways.KindOf(ways[index].Way) == WayKind.Connector) next = ground.RungOn(ways[index].Way);
            rungs[index] = Cars.BlueLight[car] ? ClaimPriority.Special : next;
        }

        for (var index = 1; index < ways.Length; index++)
        {
            if (rungs[index] < rungs[index - 1]) rungs[index] = rungs[index - 1];
        }
    }

    /// <summary>
    /// <b>The first join between two lanes of this car's line that its nose is not yet out of</b> — the box it is
    /// in or the next one it comes to — as where on the line its mouth and its far end stand.
    /// </summary>
    /// <remarks>Two lanes meeting at a point have no box, and are passed over.</remarks>
    bool TheNextBox(int car, float noseM, out float mouthM, out float endsAtM)
    {
        mouthM = endsAtM = float.NaN;
        var starts = Cars.LaneStartsOf(car);
        var ends = Cars.LaneEndsOf(car);
        for (var slot = 0; slot + 1 < Cars.Line[car].LaneCount; slot++)
        {
            if (noseM >= starts[slot + 1] || starts[slot + 1] <= ends[slot]) continue;

            mouthM = ends[slot];
            endsAtM = starts[slot + 1];
            return true;
        }

        return false;
    }
}
