using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
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
    /// <b>The road this car plans to use</b>, laid as one hold from its nose down the ways of its own line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>How far it reaches</b> is what the car could not stop short of, what it takes to pull away from where
    /// it stands, and — while it is moving — what it takes to reach the speed it is planning for, hold it a
    /// while and stop from there (TER-5g); never past the end of its line. <b>A light is not asked here</b>: its
    /// hold is ground on the way, and the plan is answered against it (TLT-1). <b>A body that is not moving
    /// states nothing beyond the first two</b>, so a queue waiting at a junction plans none of the box it is
    /// waiting for.
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
    /// <b>A car on a pass plans past it</b> (TER-4c.6, <see cref="PlannedFromM"/>): the pass is already its
    /// own, and what it passes stands on its line inside it.
    /// </para>
    /// </remarks>
    void PlanTheDrive(int car, Span<LineWay> ways)
    {
        _carHold[car] = LaneOccupancy.NoHold;

        var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);
        var keptOffM = Cars.GrantMarginM[car];
        Cars.ClaimFromM[car] = noseM;
        Cars.ClaimToM[car] = noseM;
        Cars.CommittedToM[car] = noseM;
        Cars.AuthorityM[car] = float.PositiveInfinity;
        Cars.GrantMarginM[car] = 0f;
        Cars.GrantCutBy[car] = HeadwayKind.Nothing;
        if (HandAtTheWheel(car))
        {
            HoldWhatTheHandCannotStopShortOf(car);
            return;
        }

        if (!IsUnderWay(car)) return;

        ref readonly var build = ref Cars.BuildOf(car);
        var brakingMps2 = CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car]);
        var alongMps = MathF.Max(0f, Cars.AlongMps[car]);
        var plannedMps = Cars.PlannedMps[car];
        var standOffM = _config.Driving.StandOffM;

        var committedM = (alongMps * _config.CarReactionS) + StoppingM(alongMps, brakingMps2);

        // Bounded by the pedal: from a standstill, what a reaction interval of the car's own acceleration
        // reaches and a stop from there — the room to pull away, and nothing it could not have driven over — and
        // past that the ground it was last kept off what cut it, so a car standing off something with room to step
        // round it (CAR-46) keeps it in its plan rather than creeping up until it is back in reach.
        var reachableMps = MathF.Min(plannedMps, alongMps + (build.AccelerationMps2 * _config.CarReactionS));
        var pullsAwayM = (reachableMps * _config.CarReactionS) + StoppingM(reachableMps, brakingMps2)
                         + MathF.Max(standOffM, keptOffM);

        var meansM = alongMps > _config.Driving.StopSpeedMps
            ? MeansToM(alongMps, plannedMps, build.AccelerationMps2, brakingMps2) + standOffM
            : 0f;

        var wantedM = MathF.Max(committedM, MathF.Max(pullsAwayM, meansM));
        var lengthM = Cars.Line[car].LengthM;
        var planToM = MathF.Min(noseM + wantedM, lengthM);
        var committedToM = noseM + committedM;

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

        // <b>A car on a pass plans from where it is back in its own lane</b> (TER-4c.6): the ground up to there
        // is its pass's, laid as a body, and only a body standing inside it can end it short.
        var planFromM = PlannedFromM(car);
        if (Cars.Pass[car].Begun && TheBodyInThePass(car, out var inTheWayM, out var inTheWay, out var on))
        {
            var held = _occupancy.BeginHold(standOffM);
            _carHold[car] = held;
            _occupancy.EndHold(held, inTheWayM, standOffM, inTheWay, on);
            return;
        }

        if (planToM <= planFromM) return;

        var hold = _occupancy.BeginHold(standOffM);
        _carHold[car] = hold;

        var count = WaysAlong(car, planFromM, planToM, ways);
        Span<ClaimPriority> rungs = stackalloc ClaimPriority[count];
        LevelTheRungs(car, ways[..count], rungs);

        LayTheDrive(car, hold, ways[..count], rungs, AnswerTheDrive(car, hold, ways[..count], rungs));
    }

    /// <summary>
    /// <b>A car under a hand holds the road it can no longer stop short of</b> (TER-5e, CTL-5): the stretch
    /// straight ahead of it that its speed carries it over before it could be at rest, on every way the atlas
    /// finds under it, as committed ground — so the traffic plans round what the hand cannot help, and nothing
    /// is said about what the hand means to do.
    /// </summary>
    /// <remarks>
    /// <b>Its own hold and never the drive's</b>: a hand drives no line, so nothing here is asked again when the
    /// plans are settled, and no grant is read back — the hand is the car's whole answer (S-7).
    /// </remarks>
    [SkipLocalsInit]
    void HoldWhatTheHandCannotStopShortOf(int car)
    {
        var velocity = Cars.VelocityMps[car];
        var speedMps = velocity.Length();
        if (speedMps <= _config.Driving.StopSpeedMps) return;

        ref readonly var build = ref Cars.BuildOf(car);
        var brakingMps2 = CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car]);
        var committedM = (speedMps * _config.CarReactionS) + StoppingM(speedMps, brakingMps2);
        var travel = velocity / speedMps;
        var frontM = build.HalfLengthM;

        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        var count = _atlas.UnderBox(
            Cars.PositionM[car] + (travel * (frontM + (committedM * 0.5f))), travel, committedM * 0.5f,
            build.WidthM * 0.5f, under);
        if (count == 0) return;

        var hold = _occupancy.BeginHold(_config.Driving.StandOffM);
        for (var at = 0; at < count; at++)
        {
            ref readonly var cover = ref under[at];
            var line = LineOfWay(cover.Way, out _);
            var aheadM = MathF.Max(0f, MathF.Min(
                Vector2.Dot(Spline.SampleAt(line, cover.FromM).PositionM - Cars.PositionM[car], travel),
                Vector2.Dot(Spline.SampleAt(line, cover.ToM).PositionM - Cars.PositionM[car], travel)) - frontM);
            var ask = new PlannedAsk(
                hold, car, LaneRoster.Driving, ClaimPriority.Firm, cover.FromM, aheadM, aheadM,
                float.PositiveInfinity, speedMps);

            // Never over a body (TER-4c.1): what is standing there is what the hand is about to meet.
            _occupancy.Take(ask, cover.Way, _occupancy.Reach(ask, cover.Way, cover.ToM, cover.FromM, out _));
        }

        _occupancy.EndHold(hold, float.PositiveInfinity, 0f, LaneClaim.Nothing);
    }

    /// <summary>
    /// <b>How far this car's plan can be had</b>, read against the reservations as they stand and never
    /// written: cut where the first of its ways refuses it, and short of that by what it keeps off what refused
    /// it (<see cref="KeptOffM"/>).
    /// </summary>
    PlanAnswer AnswerTheDrive(int car, int hold, ReadOnlySpan<LineWay> ways, ReadOnlySpan<ClaimPriority> rungs)
    {
        for (var index = 0; index < ways.Length; index++)
        {
            ref readonly var way = ref ways[index];
            var reachM = _occupancy.Reach(AskOn(car, hold, way, rungs[index]), way.Way, way.ToM, way.FromM, out var cutBy);
            if (reachM >= way.ToM) continue;

            return new PlanAnswer(OnTheLineM(way, reachM), KeptOffM(car, cutBy), cutBy, way.Way);
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

            _occupancy.Take(AskOn(car, hold, way, rungs[index]), way.Way, OnTheWayM(way, answer.CutLineM));
        }

        _occupancy.EndHold(hold, answer.CutLineM, answer.MarginM, answer.CutBy, answer.CutOn);
    }

    /// <summary>
    /// <b>This car's plan answered again against what every other came to</b>, and laid again where the answer
    /// has moved (<see cref="SettleThePlans"/>) — true where it did.
    /// </summary>
    bool SettleTheDrive(int car, Span<LineWay> ways)
    {
        var hold = _carHold[car];
        if (hold == LaneOccupancy.NoHold) return false;

        var endsAtM = _occupancy.HoldEndsAtM(hold, out _, out var cutBy);
        if (float.IsPositiveInfinity(endsAtM) || (cutBy.Found && cutBy.HasBody)) return false;

        var count = WaysAlong(car, PlannedFromM(car), Cars.ClaimToM[car], ways);
        Span<ClaimPriority> rungs = stackalloc ClaimPriority[count];
        LevelTheRungs(car, ways[..count], rungs);

        var answer = AnswerTheDrive(car, hold, ways[..count], rungs);
        if (answer.CutLineM == endsAtM) return false;

        _occupancy.ReopenHold(hold);
        LayTheDrive(car, hold, ways[..count], rungs, answer);
        return true;
    }

    /// <summary>The hold a car laid this rebuild, for an instrument asking what held it.</summary>
    public int DriveHold(int car) => _carHold[car];

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
        var runS = _config.Driving.PlannedRunS;
        if (plannedMps <= alongMps) return (plannedMps * runS) + StoppingM(plannedMps, brakingMps2);

        var climbS = MathF.Min(runS, (plannedMps - alongMps) / accelerationMps2);
        var reachedMps = alongMps + (accelerationMps2 * climbS);
        var coveredM = (alongMps * climbS) + (0.5f * accelerationMps2 * climbS * climbS) + (reachedMps * (runS - climbS));
        return coveredM + StoppingM(reachedMps, brakingMps2);
    }

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
            if (_ways.KindOf(ways[index].Way) == WayKind.Connector) next = RungOn(ways[index].Way);
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
        if (Cars.LineWayOf(car) != CarFleet.NoWay) return false;

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
        if (float.IsPositiveInfinity(endsAtM)) return;

        Cars.AuthorityM[car] = endsAtM - marginM - Cars.ClaimFromM[car];
        Cars.GrantMarginM[car] = marginM;
        Cars.GrantCutBy[car] = cutBy.Found ? KindOf(cutBy) : HeadwayKind.Claimed;
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
    static float StoppingM(float alongMps, float brakingMps2) =>
        alongMps <= 0f ? 0f : alongMps * alongMps / (2f * brakingMps2);
}
