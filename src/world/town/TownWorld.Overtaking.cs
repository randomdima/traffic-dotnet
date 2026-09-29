using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A car getting past what stands in its lane</b> (CAR-46, TER-4c.6): asked for off the grant a body
/// ended, laid as a body over all the ground it will cover, kept or withdrawn once before the car moves over,
/// and driven as the car's own line aimed across into the lane beside.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here reads another agent</b> (TER-4c.5). What is in the way, whether it is at rest and whether it
/// is making this car's own movement are what its body said of itself on the way it cut the grant; whether the
/// ground of the pass is free is what is laid on the ways under it.
/// </para>
/// <para>
/// <b>Its ground is the car's own body swept down the pass and read off the atlas</b>, as a body's is
/// (TER-4c.2): the collider stood where the pass puts the rear axle and pointed the way it points there, every
/// half a car's width of line from where the car stands to where it is back in its lane. So a pass runs through
/// a box as it runs along a street, wherever its ground can be had.
/// </para>
/// <para>
/// <b>A pass is never given up once the car has moved over</b>: it cannot safely be, so it is asked for only
/// over ground nobody holds, and it holds that ground at p0, where nothing takes it.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// How many bodies one pass may get past at once. A bound on a stack span and not a figure behaviour
    /// reads: a line of standing bodies longer than this is waited behind.
    /// </summary>
    const int MostPassedAtOnce = 4;

    /// <summary>Passes asked for since the town was laid (CAR-46).</summary>
    public long PassesAsked { get; private set; }

    /// <summary>And withdrawn in the rebuild after, because a body or another pass had the ground by then.</summary>
    public long PassesWithdrawn { get; private set; }

    /// <summary>And driven to the end, the car back in its own lane.</summary>
    public long PassesMade { get; private set; }

    /// <summary>
    /// <b>The ground this car's pass will cover, laid as a body</b> (TER-4c.6): swept from where the car stands to
    /// where it is back in its own lane.
    /// </summary>
    /// <remarks>
    /// <b>What it has driven over is given back</b>: the pass is laid from where the car stands every rebuild.
    /// </remarks>
    [SkipLocalsInit]
    void LayTheCarsPass(int car)
    {
        var pass = Cars.Pass[car];
        if (!pass.Any) return;

        if (!IsUnderWay(car) || Cars.LaneOf(car) != pass.Lane || Cars.LineWayOf(car) != CarFleet.NoWay)
        {
            Cars.Pass[car] = Overtake.None;
            return;
        }

        var fromM = Cars.ProgressM[car];
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        for (var station = 0; station < StationsOfThePass(car, fromM, pass.EndsM); station++)
        {
            var count = UnderTheCarOnThePass(car, pass, fromM, pass.EndsM, station, 0f, under, out _);
            for (var at = 0; at < count; at++)
            {
                if (!IsCarriageway(under[at].Way)) continue;

                var held = HeldByThePass(under[at]);
                _occupancy.LayPass(held.Way, held.FromM, held.ToM, 0f, car, LaneRoster.Driving);
            }
        }
    }

    /// <summary>
    /// <b>This tick of a car's pass</b>: over once the car is back in its lane, begun or withdrawn in the tick after
    /// it was asked for, and asked for where the grant was ended by something the car may get past.
    /// </summary>
    /// <param name="toTheStopM">How far ahead of the nose the car's grant has it stop.</param>
    /// <returns>Whether the car is coming up to something it means to get past, and has not begun to.</returns>
    bool ConsiderAPass(int car, float progressM, float alongMps, float toTheStopM)
    {
        var pass = Cars.Pass[car];
        if (pass.Begun)
        {
            if (progressM < pass.EndsM) return false;

            Cars.Pass[car] = Overtake.None;
            PassesMade++;
            return false;
        }

        if (pass.Any)
        {
            KeepOrWithdrawThePass(car, pass, progressM);
            return !Cars.Pass[car].Begun;
        }

        if (!MayGetPastWhatCutIt(car, out var cutBy, out var cutOn)) return false;

        AskForAPass(car, progressM, alongMps, toTheStopM, cutBy, cutOn);
        return true;
    }

    /// <summary>
    /// <b>Whether the car may get past what ended its grant</b>: a body it may pass (<see cref="LaneClaim.MayBePassedBy"/>),
    /// in a lane that has a lane running back beside it, on the route's own line driven forwards.
    /// </summary>
    bool MayGetPastWhatCutIt(int car, out LaneClaim cutBy, out int cutOn)
    {
        cutBy = LaneClaim.Nothing;
        cutOn = LaneOccupancy.NoHold;
        var hold = _carHold[car];
        if (hold == LaneOccupancy.NoHold || !HasALaneToPassOn(car)) return false;

        _occupancy.HoldEndsAtM(hold, out _, out cutBy);
        cutOn = _occupancy.HoldCutOn(hold);
        return cutBy.Found && cutBy.MayBePassedBy(OnwardAlongTheLine(car, cutOn));
    }

    /// <summary>
    /// Whether the car could pass anything at all where it is: on the route's own line driven forwards, in a
    /// lane that has a lane running back beside it.
    /// </summary>
    bool HasALaneToPassOn(int car)
    {
        if (Cars.LineWayOf(car) != CarFleet.NoWay || Cars.LineIsReverse[car]) return false;

        var lane = Cars.LaneOf(car);
        return lane != CarFleet.NoLane && _roads.LaneReverse[lane] >= 0 && !_roads.LaneOverOneLine[lane];
    }

    /// <summary>
    /// <b>A pass laid in this rebuild, kept or withdrawn</b> before the car moves over: kept where nothing but the
    /// car itself is on its ground, the one exception a pass asked for on the same tick by a holder numbered after
    /// it (<see cref="LaneOccupancy.KeepsItsPass"/>).
    /// </summary>
    [SkipLocalsInit]
    void KeepOrWithdrawThePass(int car, in Overtake pass, float progressM)
    {
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        for (var station = 0; station < StationsOfThePass(car, progressM, pass.EndsM); station++)
        {
            var count = UnderTheCarOnThePass(car, pass, progressM, pass.EndsM, station, 0f, under, out _);
            for (var at = 0; at < count; at++)
            {
                if (!IsCarriageway(under[at].Way)) continue;

                var held = HeldByThePass(under[at]);
                if (_occupancy.KeepsItsPass(held.Way, held.FromM, held.ToM, car, LaneRoster.Driving)) continue;

                Cars.Pass[car] = Overtake.None;
                PassesWithdrawn++;
                return;
            }
        }

        Cars.Pass[car] = pass with { Begun = true };
    }

    /// <summary>
    /// <b>A pass asked for</b> (CAR-46): where the grant was ended on the car's own line by a body it may get past,
    /// the car has come to where it would begin slowing for it, its line runs on far enough to come back onto past
    /// everything standing there, and nobody has any of the ground between.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Drawn for the speed the car is doing</b> (<see cref="CarFollower.ShapeAPass"/>), so where the lane beside
    /// is free it gets past without slowing — and asked from where it would begin slowing for what it passes, which
    /// is gently (<see cref="DrivingFigures.WaitingToPassBrakingShare"/>), so it asks all the way in.
    /// </para>
    /// <para>
    /// <b>Out as late and back as early as its body allows</b>: the step out begins as far short of what it passes
    /// as the body is still over the lane it leaves (<see cref="StepOutLeadM"/>), and the step back as soon past it
    /// as the body comes back over it (<see cref="StepBackTrailM"/>) — so the car is on the lane beside for as short
    /// a time as its steps can make it. <b>Too near to step out at the pace it is doing, it slows and asks again</b>;
    /// at rest, it asks from where it stands.
    /// </para>
    /// <para>
    /// <b>Not asked short of a place in the road it was sent to</b> (AMB-5, EVA-3, SRV-6, CTL-8a): what stands
    /// before it there is what it was sent to, and a pass ending past the place would drive it by.
    /// </para>
    /// </remarks>
    [SkipLocalsInit]
    void AskForAPass(int car, float progressM, float alongMps, float toTheStopM, in LaneClaim cutBy, int cutOn)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var speedMps = MathF.Max(0f, alongMps);
        var ground = Cars.GroundCoefficient[car];
        var brakingMps2 = CarFollower.BrakingMps2(_config, build, ground);
        var slowsFromM = StoppingM(speedMps, brakingMps2 * _config.Driving.WaitingToPassBrakingShare)
                         + (speedMps * CarFollower.LeadS(_config, build, brakingMps2)) + _config.Driving.PassSpareM;
        if (toTheStopM > slowsFromM) return;

        var lane = Cars.LaneOf(car);
        var back = _roads.LaneReverse[lane];
        var noseM = progressM + build.NoseAheadOfAxleM;
        var lineM = Cars.Line[car].LengthM;
        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        var count = WaysAlong(car, noseM, lineM, ways);
        if (!OnTheLine(ways[..count], cutOn, cutBy.FromM, out var standsFromM)) return;

        var asideM = AsideOnTheLaneBeside(lane, back, Math.Clamp(progressM, 0f, _roads.LaneLengthM[lane]));

        // Everything standing close enough past it that the car would come back in against it is passed too,
        // and anything there that is not a body it may pass is a line it cannot come back onto.
        Span<LaneClaim> passed = stackalloc LaneClaim[MostPassedAtOnce];
        var passedCount = 0;
        var clearsM = noseM;
        if (!Passes(car, ways[..count], cutOn, cutBy, passed, ref passedCount, ref clearsM)) return;

        var corneringMps2 = CarFollower.CorneringMps2(_config, build, ground);
        var bandM = _roads.LaneWidthM[lane] * 0.5f;
        var lineBend = 0f;
        Overtake pass;
        while (true)
        {
            if (!CarFollower.ShapeAPass(build, speedMps, asideM, corneringMps2, lineBend, out var stepM, out var driveMps)) return;

            var latestOutM = standsFromM - StepOutLeadM(build, asideM, stepM, bandM);
            if (latestOutM < progressM && speedMps > _config.Driving.StopSpeedMps) return;

            var outM = MathF.Max(progressM, latestOutM);
            var backM = MathF.Max(clearsM + StepBackTrailM(build, asideM, stepM, bandM), outM + stepM);
            var reachM = backM + stepM + build.NoseAheadOfAxleM + _config.Driving.StandOffM;
            if (TheNextBodyPast(car, ways[..count], clearsM, reachM, out var next, out var nextOn))
            {
                if (!Passes(car, ways[..count], nextOn, next, passed, ref passedCount, ref clearsM)) return;

                continue;
            }

            if (reachM > lineM) return;

            var bend = MostBendUnderThePass(car, progressM, reachM, asideM);
            if (float.IsPositiveInfinity(bend)) return;

            if (bend > lineBend)
            {
                lineBend = bend;
                continue;
            }

            pass = new Overtake(lane, outM, backM, asideM, stepM, driveMps, clearsM, Begun: false);
            break;
        }

        if (progressM + ToTheSceneM(car) < pass.EndsM) return;
        if (!IsThePassFree(car, pass, progressM, pass.EndsM + _config.Driving.StandOffM, passed[..passedCount])) return;

        Cars.Pass[car] = pass;
        PassesAsked++;
    }

    /// <summary>
    /// <b>How far a car's step out has to begin short of what it passes</b>: the furthest along the step that any of
    /// its body — grown by <see cref="DrivingFigures.PassSpareM"/> ahead and to either side, as the pass is asked
    /// for — is still over the lane it leaves, read as the atlas reads it. Infinite where the lane beside is too
    /// narrow for the body ever to be off it.
    /// </summary>
    /// <param name="bandM">How far to the pass's side of the line the lane it leaves reaches.</param>
    float StepOutLeadM(in CarBuild build, float asideM, float stepM, float bandM)
    {
        var acrossM = MathF.Abs(asideM);
        var mostM = float.NegativeInfinity;
        var samples = SamplesOfAStep(stepM);
        for (var sample = 0; sample <= samples; sample++)
        {
            var share = (float)sample / samples;
            if (!OverTheLaneItLeaves(
                    build, share * stepM, acrossM * Overtake.Rise(share), acrossM / stepM * Overtake.RiseSlope(share),
                    bandM, out _, out var toM))
            {
                continue;
            }

            if (sample == samples) return float.PositiveInfinity;

            mostM = MathF.Max(mostM, toM);
        }

        return mostM + (_atlas.StepM * 0.5f);
    }

    /// <summary>
    /// <b>And how soon past what it passes the step back may begin</b>: as far past it as the nearest any of the body
    /// comes back over the lane, behind where the step back begins.
    /// </summary>
    float StepBackTrailM(in CarBuild build, float asideM, float stepM, float bandM)
    {
        var acrossM = MathF.Abs(asideM);
        var leastM = float.PositiveInfinity;
        var samples = SamplesOfAStep(stepM);
        for (var sample = 0; sample <= samples; sample++)
        {
            var share = (float)sample / samples;
            if (!OverTheLaneItLeaves(
                    build, share * stepM, acrossM * (1f - Overtake.Rise(share)),
                    -acrossM / stepM * Overtake.RiseSlope(share), bandM, out var fromM, out _))
            {
                continue;
            }

            leastM = MathF.Min(leastM, fromM);
        }

        return -leastM + (_atlas.StepM * 0.5f);
    }

    /// <summary>How many pieces a step is read in: one a lattice step, which is as finely as the atlas reads a body.</summary>
    int SamplesOfAStep(float stepM) => Math.Max(1, (int)MathF.Ceiling(stepM / _atlas.StepM));

    /// <summary>
    /// <b>Where a car's body stands over the lane it steps off, at one place on the step</b> — the rear axle
    /// <paramref name="alongM"/> along and <paramref name="acrossM"/> over towards the lane beside, pointed
    /// <paramref name="slope"/> across for a metre along — as the least and most along of the part of its collider,
    /// grown by the pass's spare, short of <paramref name="bandM"/> across. False where none of it is.
    /// </summary>
    bool OverTheLaneItLeaves(
        in CarBuild build, float alongM, float acrossM, float slope, float bandM, out float fromM, out float toM)
    {
        var spareM = _config.Driving.PassSpareM;
        var forward = Vector2.Normalize(new Vector2(1f, slope));
        var side = new Vector2(-forward.Y, forward.X);
        var halfM = build.CollisionSizeM * 0.5f;
        var centreM = new Vector2(alongM, acrossM) + (forward * (build.CentreAheadOfAxleM + (spareM * 0.5f)));
        var lengthwise = forward * (halfM.X + (spareM * 0.5f));
        var sideways = side * (halfM.Y + spareM);

        Span<Vector2> corners = [
            centreM + lengthwise + sideways, centreM + lengthwise - sideways,
            centreM - lengthwise - sideways, centreM - lengthwise + sideways];
        fromM = float.PositiveInfinity;
        toM = float.NegativeInfinity;
        for (var corner = 0; corner < corners.Length; corner++)
        {
            var one = corners[corner];
            var two = corners[(corner + 1) % corners.Length];
            if (one.Y < bandM)
            {
                fromM = MathF.Min(fromM, one.X);
                toM = MathF.Max(toM, one.X);
            }

            if ((one.Y < bandM) == (two.Y < bandM)) continue;

            var crossesM = one.X + ((two.X - one.X) * (bandM - one.Y) / (two.Y - one.Y));
            fromM = MathF.Min(fromM, crossesM);
            toM = MathF.Max(toM, crossesM);
        }

        return toM >= fromM;
    }

    /// <summary>
    /// <b>The most a car's line bends under a stretch of it</b> — on its own line or on the lane beside, whichever
    /// is tighter — which a pass there has to leave room for. Infinite where the lane beside runs round the line's
    /// own centre.
    /// </summary>
    float MostBendUnderThePass(int car, float fromM, float toM, float asideM)
    {
        var mostBend = 0f;
        var startM = 0f;
        foreach (ref readonly var arc in Cars.LineOf(car))
        {
            var endM = startM + arc.LengthM;
            if (startM >= toM) break;

            if (endM > fromM)
            {
                // A line of curvature k moved a distance d towards its centre bends at k ⁄ (1 − k·d).
                var towardsTheCentre = 1f - (arc.Curvature * asideM);
                if (towardsTheCentre <= 0f) return float.PositiveInfinity;

                mostBend = MathF.Max(mostBend, MathF.Max(MathF.Abs(arc.Curvature), MathF.Abs(arc.Curvature / towardsTheCentre)));
            }

            startM = endM;
        }

        return mostBend;
    }

    /// <summary>
    /// <b>One more body the pass gets past</b>, where it is one this car may: the pass then clears it, wherever on
    /// the car's line it ends.
    /// </summary>
    bool Passes(
        int car, ReadOnlySpan<LineWay> ways, int on, in LaneClaim body, Span<LaneClaim> passed, ref int count,
        ref float clearsM)
    {
        if (count == passed.Length || !body.MayBePassedBy(OnwardAlongTheLine(car, on))) return false;
        if (!OnTheLine(ways, on, body.ToM, out var endsM)) return false;

        passed[count++] = body;
        clearsM = MathF.Max(clearsM, endsM);
        return true;
    }

    /// <summary>The nearest body on this car's own ways over a stretch of its line, other than its own.</summary>
    bool TheNextBodyPast(int car, ReadOnlySpan<LineWay> ways, float fromM, float toM, out LaneClaim body, out int on)
    {
        foreach (ref readonly var way in ways)
        {
            var wayToM = way.LineFromM + (way.ToM - way.FromM);
            if (way.LineFromM >= toM) break;
            if (wayToM <= fromM) continue;

            if (_occupancy.AheadBody(way.Way, OnTheWayM(way, fromM), OnTheWayM(way, toM), car, out body))
            {
                on = way.Way;
                return true;
            }
        }

        body = LaneClaim.Nothing;
        on = LaneOccupancy.NoHold;
        return false;
    }

    /// <summary>
    /// Where a metre of one of an agent's ways falls on its line or its walk, where the way is one of those given —
    /// never behind where the stretch of it given begins, and as far past its end as the metre is.
    /// </summary>
    static bool OnTheLine(ReadOnlySpan<LineWay> ways, int on, float wayM, out float lineM)
    {
        foreach (ref readonly var way in ways)
        {
            if (way.Way != on) continue;

            lineM = OnTheLineM(way, MathF.Max(wayM, way.FromM));
            return true;
        }

        lineM = float.NaN;
        return false;
    }

    /// <summary>
    /// <b>Whether the ground a pass would cover is free</b> (TER-4c.6): on the carriageway — a stretch over ground
    /// the traffic does not drive is a pass run off the road — clear of every zebra, with nobody standing on any of
    /// it but the car, and nobody planning it but what it passes.
    /// </summary>
    /// <remarks>
    /// <b>Nobody else is asked for with room to spare</b> (<see cref="DrivingFigures.PassSpareM"/>) — and the ground
    /// is laid and kept without it: a pass that cleared what it passes by a hair was asked for one tick and withdrawn
    /// the next, as that body settled a hair nearer. The road and the paint are asked of the body alone, since the
    /// spare is room to stray into and not a place the car is taken.
    /// </remarks>
    [SkipLocalsInit]
    bool IsThePassFree(int car, in Overtake pass, float fromM, float toM, ReadOnlySpan<LaneClaim> passed)
    {
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        for (var station = 0; station < StationsOfThePass(car, fromM, toM); station++)
        {
            var count = UnderTheCarOnThePass(car, pass, fromM, toM, station, 0f, under, out _);
            for (var at = 0; at < count; at++)
            {
                ref readonly var cover = ref under[at];
                if (!IsCarriageway(cover.Way) || CrossesAZebra(cover.Way, cover.FromM, cover.ToM)) return false;
            }

            count = UnderTheCarOnThePass(car, pass, fromM, toM, station, _config.Driving.PassSpareM, under, out _);
            for (var at = 0; at < count; at++)
            {
                if (!IsCarriageway(under[at].Way)) continue;

                var held = HeldByThePass(under[at]);
                if (!_occupancy.IsFreeForAPass(held.Way, held.FromM, held.ToM, car, LaneRoster.Driving, passed)) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <b>Where the nose of a car on a pass has to stop short of a body standing inside what is left of it</b> — the
    /// one thing that ends a pass's ground short of its end, since nothing planned can be laid over it (TER-4c.1):
    /// where it stood at the last station with nobody on its ground.
    /// </summary>
    [SkipLocalsInit]
    bool TheBodyInThePass(int car, out float inTheWayM, out LaneClaim body, out int on)
    {
        var pass = Cars.Pass[car];
        var fromM = Cars.ProgressM[car];
        var clearM = fromM;
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        for (var station = 0; station < StationsOfThePass(car, fromM, pass.EndsM); station++)
        {
            var count = UnderTheCarOnThePass(car, pass, fromM, pass.EndsM, station, 0f, under, out var atM);
            for (var at = 0; at < count; at++)
            {
                if (!IsCarriageway(under[at].Way)) continue;

                var held = HeldByThePass(under[at]);
                if (!_occupancy.AheadBody(held.Way, held.FromM, held.ToM, car, out body)) continue;

                inTheWayM = clearM + Cars.BuildOf(car).NoseAheadOfAxleM;
                on = held.Way;
                return true;
            }

            clearM = atM;
        }

        inTheWayM = float.PositiveInfinity;
        body = LaneClaim.Nothing;
        on = LaneOccupancy.NoHold;
        return false;
    }

    /// <summary>
    /// <b>The stretch of a way a pass holds where its body is swept over part of it</b>: that part, or — for a
    /// movement through a box — the whole movement.
    /// </summary>
    /// <remarks>
    /// <b>A box is held a movement at a time and never a piece of one</b>, as a car that is in one plans the rest of
    /// the join: held in part, a car crossing the box was let in and cut in the middle of it, its body standing over
    /// the ground of the pass on the movement beside its own, the pass waiting on it and it on the pass. Held
    /// whole, it waits at the mouth.
    /// </remarks>
    WayCover HeldByThePass(in WayCover cover) =>
        _ways.KindOf(cover.Way) == WayKind.Connector ? cover with { FromM = 0f, ToM = _ways.LengthM(cover.Way) } : cover;

    /// <summary>
    /// <b>Whether a car's pass is laid on a way</b>: the ways the traffic drives, and never the pavement — whose band
    /// may lie over the kerb (WLK-16), and whose walkers a pass that held it would hold on the kerb they stand on.
    /// </summary>
    bool IsCarriageway(int way) => _ways.IsDriven(way);

    /// <summary>
    /// <b>Whether a zebra's paint lies over any of a stretch of one way</b>, read off its marks (TER-5c.3) — ground a
    /// car's pass never covers: somebody on the paint is somebody crossing, and a pass holding the rest of the zebra
    /// in front of them would stand them in the car's way with nowhere to go.
    /// </summary>
    bool CrossesAZebra(int way, float fromM, float toM)
    {
        foreach (ref readonly var mark in _occupancy.Marks.Of(way))
        {
            if (mark.MineFromM >= toM) break;
            if (mark.MineToM > fromM && ZebraOf(mark.OnWay) != RibbonMarks.NoZebra) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>How many stations a pass's ground is swept at</b>, both ends among them — half a car's width of line
    /// apart, so the body turning between two is covered by the two to within a hair.
    /// </summary>
    int StationsOfThePass(int car, float fromM, float toM) =>
        toM <= fromM ? 0 : (int)MathF.Ceiling((toM - fromM) / Cars.BuildOf(car).FlankM) + 1;

    /// <summary>
    /// <b>The ways under the car at one station of its pass</b>: its collider, stood where the pass puts the rear
    /// axle at that metre of the line and pointed the way the pass points it there — and grown by
    /// <paramref name="spareM"/> ahead and to either side, the ways it could stray into; behind it is the ground it
    /// came from.
    /// </summary>
    int UnderTheCarOnThePass(
        int car, in Overtake pass, float fromM, float toM, int station, float spareM, Span<WayCover> under,
        out float atM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        atM = MathF.Min(toM, fromM + (station * build.FlankM));
        pass.PoseAtM(Cars.LineOf(car), atM, out var axleM, out var forward);

        var halfM = build.CollisionSizeM * 0.5f;
        var centreM = axleM + (forward * (build.CentreAheadOfAxleM + (spareM * 0.5f)));
        return _atlas.UnderBox(centreM, forward, halfM.X + (spareM * 0.5f), halfM.Y + spareM, under);
    }

    /// <summary>
    /// The ground a body walking or driving straight from one place to another covers, as the ways the atlas finds
    /// under it — <paramref name="halfWidthM"/> either side of the straight and past both ends of it.
    /// </summary>
    int UnderTheStretch(Vector2 fromM, Vector2 toM, float halfWidthM, Span<WayCover> under)
    {
        var along = toM - fromM;
        var lengthM = along.Length();
        return lengthM <= 0f
            ? _atlas.UnderDisc(fromM, halfWidthM, under)
            : _atlas.UnderBox((fromM + toM) * 0.5f, along / lengthM, (lengthM * 0.5f) + halfWidthM, halfWidthM, under);
    }

    /// <summary>
    /// <b>How far short of what cut it a car comes to rest</b> (S-2a): its stand-off, whatever that was — and behind
    /// a body going nowhere (<see cref="LaneClaim.GoesNowhere"/>) that it could get past, no nearer than it needs to
    /// step out round it from a standstill, with the pass's spare (CAR-46).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A queue is waited behind at the stand-off whatever its next movement</b>, so a line of cars at a light
    /// closes up behind one turning off. The room is kept only for what the car may come to pass.
    /// </para>
    /// <para>
    /// <b>Read on a straight</b>: where the line bends under the step, the step is longer and the car asks from
    /// where it stands, which the ground then answers.
    /// </para>
    /// </remarks>
    float KeptOffM(int car, in LaneClaim cutBy)
    {
        var standOffM = _config.Driving.StandOffM;
        if (!cutBy.GoesNowhere || !HasALaneToPassOn(car)) return standOffM;

        ref readonly var build = ref Cars.BuildOf(car);
        var lane = Cars.LaneOf(car);
        var asideM = (_roads.LaneWidthM[lane] + _roads.LaneWidthM[_roads.LaneReverse[lane]]) * 0.5f;
        var corneringMps2 = CarFollower.CorneringMps2(_config, build, Cars.GroundCoefficient[car]);
        if (!CarFollower.ShapeAPass(build, 0f, asideM, corneringMps2, 0f, out var stepM, out _)) return standOffM;

        var leadM = StepOutLeadM(build, asideM, stepM, _roads.LaneWidthM[lane] * 0.5f);
        return float.IsPositiveInfinity(leadM)
            ? standOffM
            : MathF.Max(standOffM, leadM + _config.Driving.PassSpareM - build.NoseAheadOfAxleM);
    }

    /// <summary>How far across its line this car is aimed at one metre of it: its pass's, once begun, and none otherwise.</summary>
    float AsideAtM(int car, float atM) => Cars.Pass[car].Begun ? Cars.Pass[car].AsideAtM(atM) : 0f;

    /// <summary>
    /// <b>Where the plan of a car on a pass begins</b>: past the pass, the ground up to there being the pass's own
    /// (TER-4c.6) — and its nose for every other car.
    /// </summary>
    float PlannedFromM(int car) =>
        Cars.Pass[car].Begun
            ? MathF.Max(Cars.ClaimFromM[car], Cars.Pass[car].EndsM + Cars.BuildOf(car).NoseAheadOfAxleM)
            : Cars.ClaimFromM[car];

    /// <summary>
    /// <b>How far across the lane beside stands at a place on a lane</b>, along the driver's right: the nearest point
    /// of its line, sought where the two lengths put it — the lane back runs the same stretch the other way round.
    /// </summary>
    float AsideOnTheLaneBeside(int lane, int back, float alongM)
    {
        var lengthM = _roads.LaneLengthM[lane];
        var backM = _roads.LaneLengthM[back];
        var on = Spline.SampleAt(_roads.ArcsOf(lane), alongM);
        var windowM = MathF.Abs(backM - lengthM) + _roads.LaneWidthM[lane] + _roads.LaneWidthM[back];
        var besideM = Spline.ProjectM(_roads.ArcsOf(back), on.PositionM, (lengthM - alongM) * (backM / lengthM), windowM);
        return Vector2.Dot(Spline.SampleAt(_roads.ArcsOf(back), besideM).PositionM - on.PositionM, on.Right);
    }
}
