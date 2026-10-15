using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;
using static TrafficSimulation.World.Road.LineWays;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>Follow</b> (CAR-15): the route's own lanes, driven on the ground planned down them — and handed over where the
/// line stops for its bay and the car is near enough to shape its way in (GEN-4f), where what ended its grant is
/// something it has decided to get past (CAR-46), or where it means to move across onto the lane beside (CAR-53).
/// <b>Its claim is a plan down the car's own line</b> (TER-4c.1): asked for from the nose as far as the car means to be
/// able to stop, had in part, and driven to where it was cut — the one claim the other actions on the route's line
/// share with it (TER-4c.8).
/// </summary>
internal sealed class Following(
    DrivingGround ground, CarActions actions, Overtaking overtaking, Switching switching, ParkingIn parkingIn)
{
    CarFleet Cars => ground.Cars;

    LaneOccupancy Occupancy => ground.Occupancy;

    SimConfig Config => ground.Config;

    /// <summary>
    /// <b>This tick of a car following its route</b>, once the line under it has been read: on down the line on the
    /// ground it was granted. Into its bay's manoeuvre or across onto the lane beside is decided on the car's own clock
    /// (<see cref="Decide"/>).
    /// </summary>
    public void Tick<TTown>(ref TTown town, int car, in CarPose pose, float progressM, float alongMps)
        where TTown : struct, ICarTown =>
        town.DriveOnTheLine(car, pose, progressM, alongMps);

    /// <summary>
    /// <b>A car following its route, on its own clock</b>: where the line stops for its bay and the car is near enough,
    /// its manoeuvre in shaped from where it stands and asked for (GEN-4f); otherwise, where it means to move across onto
    /// the lane beside, that looked for (CAR-53).
    /// </summary>
    /// <remarks>
    /// <b>A pass round what cut its grant is never decided here</b> (CAR-46): whether that is something to get past is
    /// what the body is doing, which the claims do not say (TER-4c.5). <see cref="Overtaking"/> waits on a reading of it
    /// that does.
    /// </remarks>
    public void Decide(int car)
    {
        var progressM = Cars.ProgressM[car];
        var alongMps = Cars.AlongMps[car];
        if (Cars.StopsForBayOf(car) is var bay and not CarFleet.NoBay && parkingIn.TakeUpTheBay(car, bay, progressM, alongMps))
        {
            actions.Enter(car, CarAction.Park);
            return;
        }

        if (Cars.StopsForBayOf(car) == CarFleet.NoBay && switching.Wants(car, out _)) actions.Enter(car, CarAction.Switch);
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
    /// <b>It is asked a piece a way and answered with every other plan</b> (<see cref="LaneOccupancy.Ask"/>,
    /// <see cref="LaneOccupancy.Answer"/>), the committed piece of each way apart from the rest.
    /// </para>
    /// <para>
    /// <b>A car on a pass plans past it</b> (TER-4c.6, <see cref="Overtaking.PlannedFromM"/>), in the pass's own hold: the
    /// pass is already its own, what it passes stands on its line inside it, and whatever refuses the pass ends the
    /// plan before it begins.
    /// </para>
    /// </remarks>
    public void Plan(int car, Span<LineWay> ways)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var noseM = Cars.ClaimFromM[car];
        var brakingMps2 = build.PlannedBrakingMps2;
        var alongMps = MathF.Max(0f, Cars.AlongMps[car]);
        var plannedMps = Cars.PlannedMps[car];
        var standOffM = Config.Driving.StandOffM;

        // <b>Committed as far as it could not stop short of at any tick until its next turn</b> (root decision log,
        // 2026-10-14): nothing of its plan is looked at again before then, so what it covers meanwhile at the speed it may
        // reach, and a stop from there — <b>but never past the grant it drives on meanwhile</b>, which it brakes for at
        // every tick, unless it cannot stop short of that even now.
        var turnS = Config.Sim.AgentDecisionIntervalS;
        var turnMps = MathF.Max(alongMps, MathF.Min(plannedMps, alongMps + (build.AccelerationMps2 * turnS)));
        var turnReachM = (0.5f * (alongMps + turnMps) * turnS) + CarFollower.SlowsOverM(build, turnMps);
        var committedM = MathF.Max(
            CarFollower.SlowsOverM(build, alongMps), MathF.Min(turnReachM, ground.GrantLeftAtTheTurnM(car)));

        // Bounded by the pedal: from a standstill, what a reaction interval of the car's own acceleration
        // reaches and a stop from there as the profile stops — the room to pull away, and nothing it could not have
        // driven over — and its stand-off past that.
        var reachableMps = MathF.Min(plannedMps, alongMps + (build.AccelerationMps2 * Config.CarReactionS));
        var pullsAwayM = CarFollower.SlowsOverM(build, reachableMps) + standOffM;

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
        // is its pass's, and only a body standing inside it can end it short.
        var planFromM = overtaking.PlannedFromM(car);

        // <b>No further than a plan may reach</b>, and never short of what the car can no longer stop short of.
        var planToM = MathF.Min(
            MathF.Max(committedToM, MathF.Min(wantedToM, HorizonToM(car, planFromM, build, out var joinsEndM))), lengthM);

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

        // A pass that reaches past all the car means to plan is the whole of its ground: whatever refuses that is all
        // that holds the car.
        var passHold = TheHoldOfItsPass(car);
        if (planToM <= planFromM && passHold == LaneOccupancy.NoHold)
        {
            ground.LetGoOfThePlan(car);
            return;
        }

        // <b>A plan that still holds what the car needs is kept</b> (the root decision log, 2026-10-14) — one carried on
        // from a pass is the pass's, and asked again with it.
        var ownPlan = passHold == LaneOccupancy.NoHold;

        // Asked past what it needs by a slack, so it need not ask again until it needs more: never past a join it may not
        // pass, nor past its line's end.
        var askedToM = ownPlan
            ? MathF.Max(planToM, MathF.Min(planToM + Config.Driving.ClaimSlackM, MathF.Min(lengthM, joinsEndM)))
            : planToM;
        if (ownPlan)
        {
            var kept = ground.KeepsThePlan(car, planFromM, planToM, committedToM, standOffM, out var reachM);
            if (kept == PlanKept.AsItIs) return;
            if (kept == PlanKept.ToBeAskedFurther && AskedFurther(car, planFromM, reachM, askedToM, committedToM, ways))
            {
                return;
            }
        }

        var hold = ground.BeginThePlan(car, standOffM, passHold);
        if (planToM <= planFromM) return;

        if (ownPlan) ground.AskedThePlan(car, hold, planFromM, askedToM, committedToM);

        var count = ground.WaysAlong(car, planFromM, askedToM, ways);
        Span<ClaimPriority> rungs = stackalloc ClaimPriority[count];
        LevelTheRungs(car, ways[..count], rungs);

        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            var committedOnWayM = OnTheWayM(way, committedToM);
            if (committedOnWayM > way.FromM)
            {
                Occupancy.AskCommitted(hold, way.Way, way.FromM, committedOnWayM, way.LineFromM, rungs[index]);
            }

            var fromM = MathF.Max(way.FromM, committedOnWayM);
            if (way.ToM > fromM) Occupancy.Ask(hold, way.Way, fromM, way.ToM, OnTheLineM(way, fromM), rungs[index]);
        }
    }

    /// <summary>
    /// <b>A kept plan asked further</b> (the root decision log, 2026-10-14): from <paramref name="reachM"/>, where it is cut
    /// or all it asked for ends, to <paramref name="askedToM"/> — the ground the car can no longer stop short of committed,
    /// the rest at the rung each way is levelled to over the whole plan. Not where reaching further would level a piece it
    /// holds to another rung: that plan is asked afresh.
    /// </summary>
    /// <returns>Whether it was asked further, or held all of it already.</returns>
    bool AskedFurther(int car, float planFromM, float reachM, float askedToM, float committedToM, Span<LineWay> ways)
    {
        if (askedToM <= reachM) return true;

        Span<LineWay> held = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        var heldCount = ground.WaysAlong(car, planFromM, ground.AskedToM(car), held);
        var count = ground.WaysAlong(car, planFromM, askedToM, ways);
        Span<ClaimPriority> heldRungs = stackalloc ClaimPriority[heldCount];
        Span<ClaimPriority> rungs = stackalloc ClaimPriority[count];
        LevelTheRungs(car, held[..heldCount], heldRungs);
        LevelTheRungs(car, ways[..count], rungs);
        for (var index = 0; index < heldCount; index++)
        {
            if (index >= count || heldRungs[index] != rungs[index]) return false;
        }

        var hold = ground.PlanHold[car];
        Occupancy.AskFurther(hold);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            var fromM = OnTheWayM(way, reachM);
            if (way.ToM <= fromM) continue;

            var committedOnWayM = OnTheWayM(way, committedToM);
            if (committedOnWayM > fromM)
            {
                Occupancy.AskCommitted(
                    hold, way.Way, fromM, committedOnWayM, OnTheLineM(way, fromM), rungs[index], further: true);
            }

            var restFromM = MathF.Max(fromM, committedOnWayM);
            if (way.ToM > restFromM)
            {
                Occupancy.Ask(hold, way.Way, restFromM, way.ToM, OnTheLineM(way, restFromM), rungs[index], further: true);
            }
        }

        ground.AskedFurther(car, askedToM);
        return true;
    }

    /// <summary>
    /// The hold of the pass or step across a car has begun and is driving, which its plan carries on from — or
    /// <see cref="LaneOccupancy.NoHold"/>.
    /// </summary>
    int TheHoldOfItsPass(int car) =>
        !Cars.Pass[car].Begun ? LaneOccupancy.NoHold
        : Cars.Action[car] switch
        {
            CarAction.Switch => switching.HoldOf(car),
            CarAction.Overtake => overtaking.HoldOf(car),
            _ => LaneOccupancy.NoHold,
        };

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
    /// <param name="joinsEndM">Where the joins it may pass end it, or infinity where the distance does first.</param>
    float HorizonToM(int car, float fromM, in CarBuild build, out float joinsEndM)
    {
        joinsEndM = float.PositiveInfinity;
        var reach = ReachShare(car);
        var toM = fromM + (MathF.Min(Config.Driving.PlanMostM, build.SightM) * reach);
        var ends = Cars.LaneEndsOf(car);
        var breaks = Cars.JoinBreaksOf(car);
        var mostJoins = (int)MathF.Round(Config.Driving.PlanMostJoins * reach);
        var passed = 0;
        for (var slot = 0; slot + 1 < Cars.Line[car].LaneCount; slot++)
        {
            if (ends[slot] <= fromM || !breaks[slot]) continue;
            if (++passed <= mostJoins) continue;

            joinsEndM = ends[slot];
            return MathF.Min(toM, joinsEndM);
        }

        return toM;
    }

    /// <summary>
    /// <b>How many times further than any other driver this car plans</b> (AMB-4.5): a call's reach, and one for
    /// everybody else.
    /// </summary>
    float ReachShare(int car) => Cars.BlueLight[car] ? Config.Ambulance.CallReachShare : 1f;

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
