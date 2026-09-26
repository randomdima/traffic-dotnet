using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The planned layer, for the drivers</b> (TER-4c.1): the road each car plans to use, from its nose to
/// where it means to be able to stop, laid against every body and every other plan — and what each came to.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>The hold each car laid this rebuild, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    readonly int[] _carHold;

    /// <summary>
    /// <b>A bay a wreck has claimed is a place removed from the town</b>, and a wreck is the one way a leg
    /// ends without anybody being left to give it up.
    /// </summary>
    void KeepTheBay(int car)
    {
        if (Cars.Broken[car]) _parking.Release(car);
    }

    /// <summary>
    /// <b>The road this car plans to use</b>, laid as one hold from its nose down the ways of its own line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>How far it reaches</b> is what the car could not stop short of, what it takes to pull away from where
    /// it stands, and — while it is moving — what it takes to reach the speed it is planning for, hold it a
    /// while and stop from there (TER-5g); never past a red, a bar or a crossing it is holding short of, and
    /// never past the end of its line. <b>A body that is not moving states nothing beyond the first two</b>,
    /// so a queue waiting at a junction plans none of the box it is waiting for.
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
    /// be had is read first, then a car refused somewhere it would come to rest in a box is held back at the
    /// box (<see cref="WaitsClearOfTheBoxes"/>), and only then is anything taken — so ground is never taken
    /// off another plan for a hold that then does not use it.
    /// </para>
    /// </remarks>
    void PlanTheDrive(int car, Span<LineWay> ways)
    {
        _carHold[car] = LaneOccupancy.NoHold;

        var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);
        Cars.ClaimFromM[car] = noseM;
        Cars.ClaimToM[car] = noseM;
        Cars.CommittedToM[car] = noseM;
        Cars.AuthorityM[car] = float.PositiveInfinity;
        Cars.GrantCutBy[car] = HeadwayKind.Nothing;
        if (!IsUnderWay(car)) return;

        ref readonly var build = ref Cars.BuildOf(car);
        var brakingMps2 = CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car]);
        var alongMps = MathF.Max(0f, Cars.AlongMps[car]);
        var plannedMps = Cars.PlannedMps[car];

        var committedM = (alongMps * _config.CarReactionS) + StoppingM(alongMps, brakingMps2);

        // Bounded by the pedal: from a standstill, what a reaction interval of the car's own acceleration
        // reaches and a stop from there — the room to pull away, and nothing it could not have driven over.
        var reachableMps = MathF.Min(plannedMps, alongMps + (build.AccelerationMps2 * _config.CarReactionS));
        var pullsAwayM = (reachableMps * _config.CarReactionS) + StoppingM(reachableMps, brakingMps2)
                         + build.BodyMarginM;

        var meansM = alongMps > _config.Driving.StopSpeedMps
            ? MeansToM(alongMps, plannedMps, build.AccelerationMps2, brakingMps2) + build.BodyMarginM
            : 0f;

        var heldAtM = MathF.Min(Cars.Context[car].StopAtM, Cars.Context[car].CrossingStopM);
        var wantedM = MathF.Max(committedM, MathF.Min(MathF.Max(pullsAwayM, meansM), heldAtM));
        var lengthM = Cars.Line[car].LengthM;
        var planToM = MathF.Min(noseM + wantedM, lengthM);
        var committedToM = noseM + committedM;

        // A car whose nose is in a box plans the rest of the join and its own length of the way out,
        // however slowly it is going: a body standing in a box is what everything crossing it is waiting on.
        if (TheBoxItIsIn(car, noseM, out var boxEndsAtM))
        {
            planToM = MathF.Max(planToM, MathF.Min(boxEndsAtM + build.LengthM + build.TailMarginM, lengthM));
        }

        // The box this car was given, where its line still takes it: the ground to it and through it is held,
        // and once the car can no longer stop short of its mouth the whole of it is ground it cannot give back.
        var heldToM = float.NegativeInfinity;
        if (TheBoxOnTheLine(car, Cars.MovementWay[car], out var mouthM, out var heldEndsAtM) && heldEndsAtM > noseM)
        {
            heldToM = heldEndsAtM;
            if (committedToM > mouthM) committedToM = MathF.Max(committedToM, heldEndsAtM);
        }
        else
        {
            Cars.MovementWay[car] = CarFleet.NoWay;
        }

        Cars.ClaimToM[car] = planToM;
        Cars.CommittedToM[car] = committedToM;
        if (planToM <= noseM) return;

        var hold = _occupancy.BeginHold(build.BodyMarginM);
        _carHold[car] = hold;

        var count = WaysAlong(car, noseM, planToM, ways);
        Span<ClaimPriority> rungs = stackalloc ClaimPriority[count];
        LevelTheRungs(car, ways[..count], rungs);
        TheFirstBox(car, ways[..count]);

        // Answered first: how far each piece can be had, read and never written.
        var cutLineM = float.PositiveInfinity;
        var cutBy = LaneClaim.Nothing;
        var cutOn = LaneOccupancy.NoHold;
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            var ask = AskOn(car, hold, way, rungs[index], noseM, committedToM, way.LineFromM < heldToM);
            var reachM = _occupancy.Reach(ask, way.Way, way.ToM, way.FromM, out var by);
            if (reachM >= way.ToM) continue;

            cutLineM = OnTheLineM(way, reachM);
            cutBy = by;
            cutOn = way.Way;
            break;
        }

        // Refused by somebody's plan where it would come to rest on ground another movement crosses, the car
        // waits at the mouth of the box instead — where it can still be brought to rest there at all. The
        // mouth is short of the answer, so nothing is laid past what the answer gave.
        var marginM = cutBy.Found ? new LaneCredit(build.BodyMarginM, build.TailMarginM, LaneRoster.Driving).Of(cutBy) : 0f;
        if (cutBy.Found && !cutBy.HasBody
            && !WaitsClearOfTheBoxes(car, cutLineM - marginM, ways[..count], out var mouthLineM)
            && noseM + StoppingM(alongMps, build.UtmostBrakingMps2(Cars.GroundCoefficient[car])) <= mouthLineM)
        {
            cutLineM = MathF.Max(noseM, mouthLineM);
            cutBy = LaneClaim.Nothing;
            marginM = 0f;
        }

        // And then laid, over what the answer left.
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            if (way.LineFromM >= cutLineM) break;

            var ask = AskOn(car, hold, way, rungs[index], noseM, committedToM, way.LineFromM < heldToM);
            _occupancy.Take(ask, way.Way, OnTheWayM(way, cutLineM));
        }

        _occupancy.EndHold(hold, cutLineM, marginM, cutBy, cutOn);
    }

    /// <summary>The hold a car laid this rebuild, for an instrument asking what held it.</summary>
    public int DriveHold(int car) => _carHold[car];

    /// <summary>
    /// <b>Where a car means to be able to stop</b> (TER-5g): as far as it gets in the stated run
    /// (<see cref="DrivingFigures.StatedRunS"/>) pulling up to the speed it is planning for at its own
    /// acceleration, and a stop from what that leaves it doing.
    /// </summary>
    /// <remarks>
    /// <b>What the run reaches and not what the planned speed would</b>: a car pulling away with an empty
    /// street ahead plans for its top speed, and the whole climb to it held as ground was a quarter of a
    /// kilometre and three junctions shut against the traffic crossing them by a car doing walking pace.
    /// </remarks>
    float MeansToM(float alongMps, float plannedMps, float accelerationMps2, float brakingMps2)
    {
        var runS = _config.Driving.StatedRunS;
        if (plannedMps <= alongMps) return (plannedMps * runS) + StoppingM(plannedMps, brakingMps2);

        var climbS = MathF.Min(runS, (plannedMps - alongMps) / accelerationMps2);
        var reachedMps = alongMps + (accelerationMps2 * climbS);
        var coveredM = (alongMps * climbS) + (0.5f * accelerationMps2 * climbS * climbS) + (reachedMps * (runS - climbS));
        return coveredM + StoppingM(reachedMps, brakingMps2);
    }

    /// <summary>One piece of a car's plan as the terms it is asked on.</summary>
    PlannedAsk AskOn(
        int car, int hold, in LineWay way, ClaimPriority rung, float noseM, float committedToM, bool held) =>
        new(
            hold, car, LaneRoster.Driving, rung, way.FromM, way.LineFromM, way.LineFromM - noseM,
            way.FromM + (committedToM - way.LineFromM), Cars.AlongMps[car], held);

    /// <summary>The first box a car's plan runs through this rebuild, and where on its line that box ends.</summary>
    readonly int[] _carBox;

    readonly float[] _carBoxEndsAtM;

    /// <summary>
    /// <b>The first join between two lanes a plan runs over</b>, kept for when the plan's answer is read: a
    /// plan that comes through it whole is a car that has been given that box (<see cref="ReadTheGrant"/>).
    /// </summary>
    void TheFirstBox(int car, ReadOnlySpan<LineWay> ways)
    {
        _carBox[car] = CarFleet.NoWay;
        foreach (ref readonly var way in ways)
        {
            if (_ways.KindOf(way.Way) != WayKind.Connector) continue;

            _carBox[car] = way.Way;
            _carBoxEndsAtM[car] = way.LineFromM + (_ways.LengthM(way.Way) - way.FromM);
            return;
        }
    }

    /// <summary>
    /// <b>Where the join between two lanes of this car's line that is this way stands on its line</b> — its
    /// mouth and its far end — or false where the line takes no such join.
    /// </summary>
    bool TheBoxOnTheLine(int car, int way, out float mouthM, out float endsAtM)
    {
        mouthM = endsAtM = float.NaN;
        if (way == CarFleet.NoWay || Cars.LineWayOf(car) != CarFleet.NoWay) return false;

        var chain = Cars.ChainOf(car);
        for (var slot = 0; slot + 1 < Cars.Line[car].LaneCount; slot++)
        {
            var connector = _roads.ConnectorBetween(chain[slot], chain[slot + 1]);
            if (connector == RoadGraph.NoConnector || _ways.OfRoadConnector(connector) != way) continue;

            mouthM = Cars.LaneEndsOf(car)[slot];
            endsAtM = Cars.LaneStartsOf(car)[slot + 1];
            return true;
        }

        return false;
    }

    /// <summary>
    /// <b>The rung each piece of a plan is held at</b> (TER-5e, TER-5g.1): the movement's own on a join, and
    /// on a lane the rung of the next movement the plan makes — never stronger than anything the plan holds
    /// before it.
    /// </summary>
    void LevelTheRungs(int car, ReadOnlySpan<LineWay> ways, Span<ClaimPriority> rungs)
    {
        var next = ClaimPriority.Firm;
        for (var index = ways.Length - 1; index >= 0; index--)
        {
            if (_ways.KindOf(ways[index].Way) == WayKind.Connector) next = RungOn(car, ways[index].Way);
            rungs[index] = Cars.BlueLight[car] ? ClaimPriority.Special : next;
        }

        for (var index = 1; index < ways.Length; index++)
        {
            if (rungs[index] < rungs[index - 1]) rungs[index] = rungs[index - 1];
        }
    }

    /// <summary>
    /// <b>Whether a car brought to rest with its nose at this metre of its line would be standing clear of
    /// every junction's crossings</b> — and where the mouth of the first one it would not be clear of is.
    /// </summary>
    /// <remarks>
    /// <b>A body at rest across a crossing shuts that crossing for as long as the wait lasts</b>, whether or
    /// not the ground it stands on is ground it was refused. The runs of a join are its marks merged
    /// (<see cref="WayCrossings.OwnRuns"/>), so the gaps between them are the places on it there is nothing
    /// to stand on.
    /// </remarks>
    bool WaitsClearOfTheBoxes(int car, float restNoseM, ReadOnlySpan<LineWay> ways, out float mouthLineM)
    {
        mouthLineM = float.PositiveInfinity;
        ref readonly var build = ref Cars.BuildOf(car);
        var restTailM = restNoseM - build.LengthM - build.TailMarginM;
        foreach (ref readonly var way in ways)
        {
            if (_ways.KindOf(way.Way) != WayKind.Connector) continue;

            var startsAtM = way.LineFromM - way.FromM;
            foreach (ref readonly var run in _atlas.Marks.OwnRuns(way.Way))
            {
                if (startsAtM + run.FromM >= restNoseM || startsAtM + run.ToM <= restTailM) continue;

                mouthLineM = startsAtM;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <b>Whether this car's nose is on the join between two lanes of its line</b>, and where on the line that
    /// join ends — the way out of the box it is in.
    /// </summary>
    bool TheBoxItIsIn(int car, float noseM, out float boxEndsAtM)
    {
        boxEndsAtM = noseM;
        if (Cars.LineWayOf(car) != CarFleet.NoWay) return false;

        var starts = Cars.LaneStartsOf(car);
        var ends = Cars.LaneEndsOf(car);
        for (var slot = 0; slot + 1 < Cars.Line[car].LaneCount; slot++)
        {
            if (noseM < ends[slot]) return false;
            if (noseM >= starts[slot + 1]) continue;

            boxEndsAtM = starts[slot + 1];
            return true;
        }

        return false;
    }

    /// <summary>
    /// <b>What the car actually got</b> (TER-4c.1): how far past its nose its hold survived, less the ground
    /// it keeps off whatever cut it — and infinity where nothing did.
    /// </summary>
    /// <remarks>
    /// <b>A car nothing cut is held by nobody</b>, and its grant stays infinite rather than coming back as
    /// the length of its own plan: the plan is what the profile already drives to, and handing it back as a
    /// limit would make a car alone on an empty road read as one queueing behind itself. Negative where the
    /// car cannot stop in what is left, which is a fact about a contact rather than a gap.
    /// </remarks>
    void ReadTheGrant(int car)
    {
        var hold = _carHold[car];
        var endsAtM = _occupancy.HoldEndsAtM(hold, out var marginM, out var cutBy);
        TheBoxGiven(car, hold, endsAtM, cutBy);
        if (float.IsPositiveInfinity(endsAtM)) return;

        Cars.AuthorityM[car] = endsAtM - marginM - Cars.ClaimFromM[car];
        Cars.GrantCutBy[car] = cutBy.Found ? KindOf(cutBy) : HeadwayKind.Claimed;
    }

    /// <summary>
    /// <b>Whether this car has been given the box it is driving at</b> (<see cref="CarFleet.MovementWay"/>):
    /// its plan came through the whole of it this rebuild, or it had the box already and all that stopped it
    /// short is a body in front — which is a queue it is waiting in and not a box it lost.
    /// </summary>
    void TheBoxGiven(int car, int hold, float endsAtM, in LaneClaim cutBy)
    {
        var box = hold == LaneOccupancy.NoHold ? CarFleet.NoWay : _carBox[car];
        if (box == CarFleet.NoWay)
        {
            Cars.MovementWay[car] = CarFleet.NoWay;
            return;
        }

        var through = endsAtM >= _carBoxEndsAtM[car];
        var queueing = Cars.MovementWay[car] == box && cutBy.HasBody;
        Cars.MovementWay[car] = through || queueing ? box : CarFleet.NoWay;
    }

    /// <summary>
    /// <b>How far ahead of the rear axle the body's leading edge stands along the line it is driving</b> —
    /// its nose forwards, its tail backwards.
    /// </summary>
    /// <remarks>
    /// A line's metres run in the direction of travel whichever gear it is taken in, so a reversing body plans
    /// tail-first and its road begins there.
    /// </remarks>
    float LeadingEdgeAheadOfTheAxleM(int car) =>
        Cars.LineIsReverse[car] ? Cars.BuildOf(car).TailBehindAxleM : Cars.BuildOf(car).NoseAheadOfAxleM;

    /// <summary>How much road a body doing this speed needs before it can be at rest on the ground it is on.</summary>
    static float StoppingM(float alongMps, float brakingMps2) => LaneCredit.StoppingM(alongMps, brakingMps2);
}
