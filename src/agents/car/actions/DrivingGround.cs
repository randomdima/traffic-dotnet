using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using static TrafficSimulation.World.Road.LineWays;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>The town as a car's action reads it</b>: the fleet, the ways and the ground each covers, what is laid on them
/// at its turn — and the questions every action asks of them, about a car's own line over the ways and about the
/// ground a body swept down a pass or a manoeuvre stands on.
/// </summary>
/// <remarks>
/// <b>It is the whole of what an action sees of another agent</b> (TER-4c.5): the reservations and nothing else. What
/// only the town knows — the leg, the errand, the tyres — an action asks of <see cref="ICarTown"/>.
/// </remarks>
internal sealed class DrivingGround(
    CarFleet cars, LaneOccupancy occupancy, TownWays ways, RoadGraph roads, WayLines lines,
    SimConfig config)
{
    /// <summary>
    /// How many ways one line may be cut into: every lane it is laid over and the join between each pair. A
    /// bound on a stack span and not a figure behaviour reads.
    /// </summary>
    public const int MostWaysAlongALine = (LineAssembler.MostLanes * 2) - 1;

    public CarFleet Cars { get; } = cars;

    /// <summary>What is laid on the ways: the one layer every reader reads.</summary>
    public LaneOccupancy Occupancy { get; } = occupancy;

    public TownWays Ways { get; } = ways;

    public RoadGraph Roads { get; } = roads;

    public WayLines Lines { get; } = lines;

    public SimConfig Config { get; } = config;

    /// <summary>
    /// <b>The plan each car laid down its own line at its last turn</b>, or <see cref="LaneOccupancy.NoHold"/> — the one
    /// claim two actions share (TER-4c.8), and what a car was cut at is read off.
    /// </summary>
    public int[] PlanHold { get; } = LaneOccupancy.NoHolds(cars.Capacity);

    /// <summary>The ground each car keeps off whatever cuts its plan (S-2a) — its own, given with the plan.</summary>
    readonly float[] _planMarginM = new float[cars.Capacity];

    /// <summary>
    /// <b>What a car's kept plan holds</b> (<see cref="LaneOccupancy.BeginPlan"/>): the line, the action, the call and
    /// the piece of line its nose was on when it was asked, and how far along the line it holds from and was asked to.
    /// </summary>
    record struct HeldPlan(int Hold, uint Line, CarAction Action, bool BlueLight, int NoseOn, float FromM, float ToM);

    readonly HeldPlan[] _held = Enumerable.Repeat(new HeldPlan { Hold = LaneOccupancy.NoHold }, cars.Capacity).ToArray();

    /// <summary>How many plans have come to each <see cref="PlanKept"/>, for an instrument.</summary>
    public long[] PlansKept { get; } = new long[Enum.GetValues<PlanKept>().Length];

    /// <summary>
    /// <b>A car's plan kept</b>, where what it holds still serves what the car needs (the root decision log, 2026-10-14):
    /// the same line, action and call; the nose on the piece of the line it was asked from, so every piece is held at the
    /// rung a plan asked now would level it to; and what it means to reach short of what was asked by no more than twice
    /// <see cref="DrivingFigures.ClaimSlackM"/>. Its own ground is then edited where it lies: let go of behind the nose
    /// once that is <see cref="DrivingFigures.TrimSlackM"/> on, and committed as far as the car now cannot stop short of
    /// (<see cref="LaneOccupancy.CommitTo"/>).
    /// </summary>
    /// <returns>
    /// <see cref="PlanKept.AsItIs"/> where it holds all the car needs and nothing has changed where it was cut
    /// (<see cref="LaneOccupancy.IsWoken"/>); <see cref="PlanKept.ToBeAskedFurther"/>, from <paramref name="reachM"/>,
    /// where it does not; <see cref="PlanKept.No"/> where the plan is to be asked afresh.
    /// </returns>
    public PlanKept KeepsThePlan(int car, float fromM, float toM, float committedToM, float marginM, out float reachM)
    {
        reachM = float.NaN;
        ref var held = ref _held[car];
        var driving = Config.Driving;
        if (held.Line != Cars.LinesLaid[car] || held.Action != Cars.Action[car] || held.BlueLight != Cars.BlueLight[car]
            || fromM < held.FromM || held.ToM - toM > 2f * driving.ClaimSlackM || held.NoseOn != PieceOfTheLine(car, fromM)
            || !Occupancy.IsPlanOf(held.Hold, car, LaneRoster.Driving))
        {
            PlansKept[(int)PlanKept.No]++;
            return PlanKept.No;
        }

        reachM = HeldToM(held);
        var further = toM > held.ToM || committedToM > reachM || Occupancy.IsWoken(held.Hold);
        if (further && Occupancy.IsCutAtAMark(held.Hold))
        {
            PlansKept[(int)PlanKept.No]++;
            return PlanKept.No;
        }

        var kept = further ? PlanKept.ToBeAskedFurther : PlanKept.AsItIs;
        PlansKept[(int)kept]++;
        if (fromM - held.FromM > driving.TrimSlackM)
        {
            Occupancy.TrimBehind(held.Hold, fromM);
            held.FromM = fromM;
        }

        Occupancy.CommitTo(held.Hold, committedToM);
        PlanHold[car] = held.Hold;
        _planMarginM[car] = marginM;
        return kept;
    }

    /// <summary>How far along its line a kept plan holds: where it was cut, or all it asked for.</summary>
    float HeldToM(in HeldPlan held)
    {
        var cut = Occupancy.CutOf(held.Hold);
        return cut.Refused ? cut.CutLineM : held.ToM;
    }

    /// <summary>How far along its line a car's kept plan was asked for.</summary>
    public float AskedToM(int car) => _held[car].ToM;

    /// <summary>
    /// What a car's kept plan was asked for (<see cref="KeepsThePlan"/>), and the ground it can no longer stop short of
    /// committed (<see cref="LaneOccupancy.CommitTo"/>).
    /// </summary>
    public void AskedThePlan(int car, int hold, float fromM, float toM, float committedToM)
    {
        _held[car] = new HeldPlan(
            hold, Cars.LinesLaid[car], Cars.Action[car], Cars.BlueLight[car], PieceOfTheLine(car, fromM), fromM, toM);
        Occupancy.CommitTo(hold, committedToM);
    }

    /// <summary>A car's kept plan asked further, now to <paramref name="toM"/> (<see cref="PlanKept.ToBeAskedFurther"/>).</summary>
    public void AskedFurther(int car, float toM) => _held[car].ToM = toM;

    /// <summary>A car's kept plan let go of, where it has one — before it asks for another, or where it plans none.</summary>
    public void LetGoOfThePlan(int car)
    {
        ref var held = ref _held[car];
        if (Occupancy.IsPlanOf(held.Hold, car, LaneRoster.Driving)) Occupancy.Release(held.Hold);

        held.Hold = LaneOccupancy.NoHold;
    }

    /// <summary>Which piece of a car's line a place on it is on: twice a lane's slot, or one less for the join into it.</summary>
    int PieceOfTheLine(int car, float lineM)
    {
        var starts = Cars.LaneStartsOf(car);
        var ends = Cars.LaneEndsOf(car);
        var lanes = Cars.Line[car].LaneCount;
        for (var slot = 0; slot < lanes; slot++)
        {
            if (lineM < ends[slot]) return lineM >= starts[slot] ? 2 * slot : (2 * slot) - 1;
        }

        return (2 * lanes) - 1;
    }

    /// <summary>
    /// <b>A car's plan begun</b>: its hold, and the ground it keeps off whatever cuts it — any plan it kept let go of first.
    /// </summary>
    /// <param name="begunOn">
    /// <b>The hold of a pass or a manoeuvre the car has begun</b>, which the plan carries on from — what it drives first,
    /// so whatever refuses that ends the plan too — or <see cref="LaneOccupancy.NoHold"/>, for a hold kept from one
    /// turn to the next (<see cref="KeepsThePlan"/>).
    /// </param>
    public int BeginThePlan(int car, float marginM, int begunOn = LaneOccupancy.NoHold)
    {
        LetGoOfThePlan(car);
        var hold = begunOn != LaneOccupancy.NoHold ? begunOn : Occupancy.BeginPlan(car, LaneRoster.Driving);
        PlanHold[car] = hold;
        _planMarginM[car] = marginM;
        return hold;
    }

    readonly float[] _grantLeftM = new float[cars.Capacity];

    /// <summary>
    /// <b>How much of its grant a car had still in front of its nose as its turn began</b> (<see cref="CarFleet.GrantLeftM"/>)
    /// — the most it drives on before its next turn but what it can no longer stop short of.
    /// </summary>
    public float GrantLeftAtTheTurnM(int car) => _grantLeftM[car];

    /// <summary>
    /// <b>A car's claim of its turn before, forgotten</b> before its action lays this turn's: no hold, a plan from
    /// its nose to its nose, and <b>nothing granted</b> (TER-4c.8) — a car moves over ground its action has claimed
    /// at its turn and over no other, so what the action lays is the only thing that grants it road.
    /// </summary>
    public void ClearTheClaim(int car)
    {
        _grantLeftM[car] = Cars.GrantLeftM(car);
        PlanHold[car] = LaneOccupancy.NoHold;
        _whatCutIt[car] = BodyAhead.Nothing;

        var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);
        Cars.ClaimFromM[car] = noseM;
        Cars.ClaimToM[car] = noseM;
        Cars.CommittedToM[car] = noseM;
        Cars.HorizonM[car] = float.PositiveInfinity;
        Cars.GrantMarginM[car] = 0f;
        Cars.GrantCutBy[car] = HeadwayKind.Nothing;
        Cars.AuthorityM[car] = 0f;
        Cars.CoveredSinceClaimM[car] = 0f;
    }

    /// <summary>
    /// <b>A car's grant forgotten where it takes a line afresh</b>: what it was granted was read down the line it had,
    /// in that line's metres, and is nothing on the one it takes — until the claims are next laid.
    /// </summary>
    public void ForgetTheGrant(int car)
    {
        Cars.AuthorityM[car] = 0f;
        Cars.CoveredSinceClaimM[car] = 0f;
        Cars.HorizonM[car] = float.PositiveInfinity;
    }

    /// <summary>
    /// <b>What the car actually got</b> (TER-4c.1): how far past its nose its hold survived, less the ground
    /// it keeps off whatever cut it — and infinity where nothing did.
    /// </summary>
    /// <remarks>
    /// <b>A car nothing cut is held by nobody</b>, and its grant is infinite rather than the length of its own plan:
    /// handing it back as a limit would make a car alone on an empty road read as one queueing behind itself. Its
    /// plan reached as far as it means to be able to stop, and where it was held short of that, its end is a stop
    /// point of its own beside the grant (<see cref="CarFleet.HorizonM"/>). Negative where the car cannot stop in what
    /// is left, which is a fact about a contact rather than a gap. <b>A car that laid no hold keeps what its action
    /// granted it</b> — the whole of a manoeuvre's or a pass's ground, or nothing.
    /// </remarks>
    public void ReadTheGrant(int car)
    {
        _whatCutIt[car] = BodyAhead.Nothing;
        var hold = PlanHold[car];
        var cut = Occupancy.CutOf(hold);
        if (hold == LaneOccupancy.NoHold || !cut.Refused || cut.Kind != ClaimKind.Body) _wasAlongMps[car] = float.NaN;
        if (hold == LaneOccupancy.NoHold) return;

        if (!cut.Refused)
        {
            Cars.AuthorityM[car] = float.PositiveInfinity;
            return;
        }

        var marginM = _planMarginM[car];
        Cars.AuthorityM[car] = cut.CutLineM - marginM - Cars.ClaimFromM[car];
        Cars.GrantMarginM[car] = marginM;
        Cars.GrantCutBy[car] = KindOf(cut);
        if (cut.Kind == ClaimKind.Body) _whatCutIt[car] = ReadAgain(car, ReadTheBodyAhead(car, cut.Way, cut.AtM));
    }

    /// <summary>How fast the body that cut each car's plan at its last turn was going down its line, or NaN where none did.</summary>
    readonly float[] _wasAlongMps = Enumerable.Repeat(float.NaN, cars.Capacity).ToArray();

    /// <summary>
    /// <b>A body read against the car's reading of it at its last turn</b>: going nowhere only where it was so then too
    /// (<see cref="TwiceGoingNowhere"/>), and holding its pace only where a body cut the plan then too and this one has
    /// gained no more than <see cref="DrivingFigures.PullingAwayMps2"/> over the turn — a queue moving off or traffic
    /// getting up to pace is holding nobody up.
    /// </summary>
    BodyAhead ReadAgain(int car, in BodyAhead read)
    {
        ref var wasMps = ref _wasAlongMps[car];
        var gainedMps2 = (read.AlongMps - wasMps) / MathF.Max(Config.Sim.AgentDecisionIntervalS, Config.TickSeconds);
        var steady = read.Read && float.IsFinite(gainedMps2) && gainedMps2 <= Config.Driving.PullingAwayMps2;
        wasMps = read.Read ? read.AlongMps : float.NaN;
        return TwiceGoingNowhere(car, read) with { Steady = steady };
    }

    /// <summary>Where the body each car read going nowhere at its last turn stood, or nothing.</summary>
    readonly BodySeen[] _seenGoingNowhere = new BodySeen[cars.Capacity];

    /// <summary>
    /// <b>A body read going nowhere only at the second turn running it is read so</b>, standing where it stood to within
    /// the pass's spare: a body that has only just come to rest, or whose holder has yet to take its first turn and lay
    /// its plan, holds nothing past it for a turn and is not going nowhere for that.
    /// </summary>
    BodyAhead TwiceGoingNowhere(int car, in BodyAhead read)
    {
        ref var was = ref _seenGoingNowhere[car];
        var spareM = Config.Driving.PassSpareM;
        var again = was.Way == read.Seen.Way && was.ToM > was.FromM
                    && MathF.Abs(was.FromM - read.Seen.FromM) <= spareM && MathF.Abs(was.ToM - read.Seen.ToM) <= spareM;
        was = read.GoesNowhere ? read.Seen : default;
        return read.GoesNowhere && !again ? read with { GoesNowhere = false } : read;
    }

    /// <summary>
    /// <b>A car's grant kept further off the body that cut it</b> (S-2a, CAR-46): <paramref name="keptOffM"/> short of it
    /// rather than its stand-off, where that is more — the room to step out round something it may come to pass.
    /// </summary>
    public void KeepOffWhatCutIt(int car, float keptOffM)
    {
        var marginM = Cars.GrantMarginM[car];
        if (keptOffM <= marginM || Cars.GrantCutBy[car] != HeadwayKind.Body) return;

        Cars.AuthorityM[car] -= keptOffM - marginM;
        Cars.GrantMarginM[car] = keptOffM;
    }

    /// <summary>The holds each car nobody is driving holds the road it cannot stop short of in, one a way under it.</summary>
    readonly int[] _handHolds = new int[cars.Capacity * RibbonAtlas.MostWaysUnderABody];

    readonly byte[] _handHoldCount = new byte[cars.Capacity];

    void HoldTheHand(int car, int hold)
    {
        if (_handHoldCount[car] == RibbonAtlas.MostWaysUnderABody) return;

        _handHolds[(car * RibbonAtlas.MostWaysUnderABody) + _handHoldCount[car]++] = hold;
    }

    /// <summary>What a car nobody is driving held of the road at its last turn, let go of.</summary>
    public void LetGoOfTheHand(int car)
    {
        for (var at = 0; at < _handHoldCount[car]; at++) Occupancy.Release(_handHolds[(car * RibbonAtlas.MostWaysUnderABody) + at]);

        _handHoldCount[car] = 0;
    }

    /// <summary>
    /// <b>A car nobody in the town is driving holds the road it can no longer stop short of</b> (TER-5e, TER-4c.8) —
    /// one under a hand (CTL-5) and one on a bar (EVA-5): on every way the world reads its body on
    /// (<paramref name="under"/>), the stretch past the body its speed carries it down that way before it could be at
    /// rest, from anywhere it gets to before its next turn, as committed ground under <paramref name="occupant"/> — so the
    /// traffic plans round what the hand or the truck cannot help, and nothing is said about where either means to go.
    /// </summary>
    /// <remarks>
    /// <b>Down the way and never across it</b>: a body off every way holds nothing, and one leaving its way holds the
    /// way it is on until the world reads it on another. <b>Its own hold and never the drive's</b>: it drives no line, so
    /// no grant is read back — the hand or the truck is the car's whole answer (S-7). <b>Never over a body</b>
    /// (TER-4c.1): what is standing there is what the car is about to meet, and refuses each stretch as it refuses any
    /// plan.
    /// </remarks>
    /// <param name="occupant">Whose ground it is held as: the car's own, or the truck's it is laid under.</param>
    public void HoldWhatCannotBeStoppedShortOf(int car, int occupant, ReadOnlySpan<WayCover> under)
    {
        var velocity = Cars.VelocityMps[car];
        var speedMps = velocity.Length();
        if (speedMps <= Config.Driving.StopSpeedMps) return;

        var committedM = CarFollower.SlowsOverM(Cars.BuildOf(car), speedMps) + (speedMps * Config.Sim.AgentDecisionIntervalS);
        foreach (ref readonly var cover in under)
        {
            var line = Lines.LineOf(cover.Way, out _);
            var lengthM = Ways.LengthM(cover.Way);
            var alongTheWay = Vector2.Dot(Spline.SampleAt(line, (cover.FromM + cover.ToM) * 0.5f).Direction, velocity) >= 0f;
            var hold = Occupancy.BeginHold(occupant, LaneRoster.Driving);
            HoldTheHand(car, hold);
            if (alongTheWay)
            {
                Occupancy.Ask(hold, cover.Way, cover.ToM, MathF.Min(lengthM, cover.ToM + committedM), 0f, ClaimPriority.Committed);
            }
            else
            {
                Occupancy.AskDown(hold, cover.Way, MathF.Max(0f, cover.FromM - committedM), cover.FromM, 0f, ClaimPriority.Committed);
            }
        }
    }

    /// <summary>
    /// Where a place on one of this car's lanes falls on the line it is driving — <see cref="WaysAlong"/>'s
    /// own trip, made the other way round for one place instead of in bulk for a stretch.
    /// </summary>
    public float OnTheLineM(int car, int slot, float alongLaneM) =>
        LineAssembler.OnTheLineM(Cars.LaneStartsOf(car), Cars.LaneEndsOf(car), slot, alongLaneM);

    /// <summary>
    /// The town's ways under a stretch of one car's line, nearest first — the lanes it is laid over and the
    /// joins threaded between them, each with the metres of its own that the stretch covers.
    /// </summary>
    /// <remarks>
    /// <b>The metres of a way and the metres of a line run at the same rate and start together</b> (TER-5d):
    /// the line over a lane is that lane's own arcs from its own first metre, so a stretch carried across is
    /// the same stretch of the same bending ground and not a chord over it. <b>The last lane is its whole
    /// length</b>, though a line may stop short of it where a car waits for its bay: the line is the axle's, and
    /// the nose of a car at rest at its end stands further on.
    /// </remarks>
    /// <param name="spansM">
    /// Where given, room for where on the line each way written begins and ends — the line metres a way's own nought and
    /// its end fall at, unclipped by the stretch.
    /// </param>
    public int WaysAlong(int car, float fromLineM, float toLineM, Span<LineWay> into, Span<WaySpan> spansM = default)
    {
        var lanes = Cars.Line[car].LaneCount;
        var chain = Cars.ChainOf(car);
        var joins = Cars.JoinsOf(car);
        var starts = Cars.LaneStartsOf(car);
        var ends = Cars.LaneEndsOf(car);
        var spans = !spansM.IsEmpty;

        var written = 0;
        for (var index = 0; index < lanes && written < into.Length; index++)
        {
            var leavingOn = joins[index];
            var endsM = index < lanes - 1 ? ends[index] : starts[index] + Roads.LaneLengthM[chain[index]];

            if (Overlaps(fromLineM, toLineM, starts[index], endsM, out var fromM, out var toM))
            {
                if (spans) spansM[written] = new WaySpan(starts[index], endsM);
                into[written++] = new LineWay(
                    Ways.OfRoadLane(chain[index]), fromM - starts[index], toM - starts[index], fromM);
            }

            // <b>The stretch runs out at the box's near edge — `ends[index]` — and not at its far one.</b>
            // A join of no length has no ground between its two lanes and so nothing to write.
            if (leavingOn == RoadGraph.NoConnector || ends[index] >= toLineM) break;

            if (written < into.Length && starts[index + 1] > ends[index]
                && Overlaps(fromLineM, toLineM, ends[index], starts[index + 1], out fromM, out toM))
            {
                if (spans) spansM[written] = new WaySpan(ends[index], starts[index + 1]);
                into[written++] = new LineWay(
                    Ways.OfRoadConnector(leavingOn), fromM - ends[index], toM - ends[index], fromM);
            }
        }

        return written;
    }

    /// <summary>
    /// <b>Where every car means to go next</b> (OBS-2h): the piece of its line under its body and the piece after it — the
    /// rest of a lane and the join off its end, or the join being crossed and the lane it lands on — as the ways under them.
    /// A manoeuvre's own geometry is on no way, and states nothing here.
    /// </summary>
    public void StateIntents(WayIntents into)
    {
        Span<LineWay> ways = stackalloc LineWay[WayIntents.MostEach];
        for (var car = 0; car < Cars.Count; car++)
        {
            if (Cars.Line[car].LaneCount == 0) continue;

            var totalM = Cars.Line[car].LengthM;
            var underTheCarM = MathF.Min(Math.Clamp(Cars.ProgressM[car], 0f, totalM) + Cars.BuildOf(car).CentreAheadOfAxleM, totalM);
            var untilM = PieceEndM(car, PieceEndM(car, underTheCarM));
            var written = WaysAlong(car, underTheCarM, untilM, ways);
            for (var at = 0; at < written; at++) into.Add(car, LaneRoster.Driving, ways[at].Way, ways[at].FromM, ways[at].ToM);
        }
    }

    /// <summary>
    /// <b>How far along its line the piece a place on it is on runs out</b>: the end of the lane it is in, the end of the
    /// join across the box, or the end of the line where a manoeuvre laid geometry of its own and no lane is under it.
    /// </summary>
    /// <remarks>
    /// <b>The boundaries are the assembler's own</b> (<see cref="CarFleet.LaneStartsOf"/>): ascending along the line, the first
    /// boundary past the place is the end of what it is on. Between one lane's end and the next one's start is the join.
    /// </remarks>
    public float PieceEndM(int car, float atM)
    {
        var lanes = Cars.Line[car].LaneCount;
        var starts = Cars.LaneStartsOf(car);
        var ends = Cars.LaneEndsOf(car);
        for (var slot = 0; slot < lanes; slot++)
        {
            if (starts[slot] > atM) return starts[slot];
            if (ends[slot] > atM) return ends[slot];
        }

        return Cars.Line[car].LengthM;
    }

    /// <summary>
    /// <b>How far ahead of the rear axle the body's leading edge stands along the line it is driving</b> —
    /// its nose forwards, its tail backwards.
    /// </summary>
    /// <remarks>
    /// A line's metres run in the direction of travel whichever gear it is taken in, so a reversing body plans
    /// tail-first and its road begins there.
    /// </remarks>
    public float LeadingEdgeAheadOfTheAxleM(int car) =>
        Cars.LineIsReverse[car] ? Cars.BuildOf(car).TailBehindAxleM : Cars.BuildOf(car).NoseAheadOfAxleM;

    /// <summary>How much road a body doing this speed needs before it can be at rest on the ground it is on.</summary>
    public static float StoppingM(float alongMps, float brakingMps2) =>
        alongMps <= 0f ? 0f : alongMps * alongMps / (2f * brakingMps2);

    /// <summary>
    /// <b>The right of way a movement has on a way</b> (TER-5e) — its own through a box, and ordinary traffic
    /// everywhere else. A call's rung is the hold's and not the way's (AMB-4).
    /// </summary>
    public ClaimPriority RungOn(int way) =>
        Ways.KindOf(way) == WayKind.Connector ? Roads.FirmOnConnector(Ways.RoadConnectorOf(way)) : ClaimPriority.Firm;

    /// <summary>
    /// <b>Whether a zebra's paint lies over any of a stretch of one way</b>, read off its marks (TER-5c.3) — ground a
    /// car's pass never covers: somebody on the paint is somebody crossing, and a pass holding the rest of the zebra
    /// in front of them would stand them in the car's way with nowhere to go.
    /// </summary>
    public bool CrossesAZebra(int way, float fromM, float toM)
    {
        foreach (ref readonly var mark in Occupancy.Marks.Of(way))
        {
            if (mark.MineFromM >= toM) break;
            if (mark.MineToM > fromM && Lines.ZebraOf(mark.OnWay) != RibbonMarks.NoZebra) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>Where on its line a car's leading edge stops for something in ground a pass or a manoeuvre holds</b> from
    /// <paramref name="firstAtM"/> on: at the station a flank short of it, and never behind where the car stands.
    /// </summary>
    public float StopsShortOfM(int car, float firstAtM) =>
        MathF.Max(Cars.ProgressM[car], firstAtM - Cars.BuildOf(car).FlankM) + LeadingEdgeAheadOfTheAxleM(car);

    /// <summary>Whether a hold of ground asked whole was had whole (TER-4c.6).</summary>
    public bool IsKept(int hold) => hold != LaneOccupancy.NoHold && !Occupancy.CutOf(hold).Refused;

    /// <summary>
    /// <b>How many stations a manoeuvre's ground is read at</b>, both ends among them — half a car's width of line
    /// apart, so the body turning between two is covered by the two to within a hair.
    /// </summary>
    public int StationsOfTheSweep(int car, float fromM, float toM) =>
        toM <= fromM ? 0 : (int)MathF.Ceiling((toM - fromM) / Cars.BuildOf(car).FlankM) + 1;

    /// <summary>
    /// <b>What cut a plan, to a driver reading it</b> — off its kind and rung alone (TER-4c.5): a body, a light's hold, or
    /// ground somebody plans to use.
    /// </summary>
    public static HeadwayKind KindOf(in Refusal cut) =>
        !cut.Refused ? HeadwayKind.Nothing
        : cut.Kind == ClaimKind.Body ? HeadwayKind.Body
        : cut.ByALight ? HeadwayKind.Light
        : HeadwayKind.Claimed;

    readonly BodyAhead[] _whatCutIt = new BodyAhead[cars.Capacity];

    /// <summary>
    /// <b>The body that ended a car's plan at its last turn, as it was read then</b> (<see cref="ReadTheGrant"/>) — or
    /// <see cref="BodyAhead.Nothing"/> where no body did. Read once a turn, as the grant is (TER-49).
    /// </summary>
    public ref readonly BodyAhead WhatCutIt(int car) => ref _whatCutIt[car];

    /// <summary>
    /// <b>The first body on one of a car's ways from a place on it, read where it stands</b> (TER-4c.5): how it moves along
    /// the car's own line, and whether it goes nowhere the car's way — at rest, and nothing laid a stand-off past it down
    /// the car's line (<see cref="LaneOccupancy.IsHeldPast"/>) — or waits on what the car would wait on: a queue.
    /// </summary>
    public BodyAhead ReadTheBodyAhead(int car, int way, float atM)
    {
        if (!Occupancy.FirstBodySeen(way, atM, Occupancy.WayLengthM(way), car, LaneRoster.Driving, out var seen))
        {
            return BodyAhead.Nothing;
        }

        if (!Occupancy.ReadTheBody(seen, car, LaneRoster.Driving, out var velocityMps, out var headingRad, out var wheeled))
        {
            return new BodyAhead(seen, Read: false, AtRest: false, GoesNowhere: false, AlongMps: 0f, Wheeled: false);
        }

        Span<LineWay> line = stackalloc LineWay[MostWaysAlongALine];
        var count = WaysAlong(car, Cars.ClaimFromM[car], Cars.Line[car].LengthM + LeadingEdgeAheadOfTheAxleM(car), line);
        var direction = OnTheLine(line[..count], way, (seen.FromM + seen.ToM) * 0.5f, out var lineM)
            ? Spline.SampleAt(Cars.LineOf(car), Math.Clamp(lineM, 0f, Cars.Line[car].LengthM)).Direction
            : Vector2.Zero;
        var clearsAtM = OnTheLine(line[..count], way, seen.ToM, out var farM) ? farM : float.PositiveInfinity;
        var atRest = velocityMps.Length() <= Config.Driving.StopSpeedMps;
        var travelsTheLine = Vector2.Dot(Heading.Unit(headingRad), direction) >= Config.TravelsTheLineCos;
        var goesNowhere = atRest
                          && (!travelsTheLine
                              || !Occupancy.IsHeldPast(
                                  seen, line[..count], Config.Driving.StandOffM, Config.Driving.StopSpeedMps, car, LaneRoster.Driving));
        return new BodyAhead(seen, Read: true, atRest, goesNowhere, Vector2.Dot(velocityMps, direction), wheeled, clearsAtM);
    }
}

/// <summary>
/// <b>A body ahead as a driver reads it, by place</b> (TER-4c.5): where it stands and how it moves — never who, and never
/// carried on a claim.
/// </summary>
/// <param name="Read">Whether its motion was read at all: a body laid as another holder's ground is not, and is never got past.</param>
/// <param name="GoesNowhere">At rest, with nothing laid just past it down the reader's line: a wreck, a car stood down, somebody standing in the road.</param>
/// <param name="AlongMps">How fast it goes down the reader's line — negative coming the other way.</param>
/// <param name="Wheeled">Whether it is traffic rather than somebody on foot.</param>
/// <param name="ClearsAtM">Where along the reader's line its far edge stands, or infinity where that is off the line.</param>
/// <param name="Steady">
/// Whether a body cut the reader's plan at its last turn too and this one has gained no more than
/// <see cref="DrivingFigures.PullingAwayMps2"/> since — not a queue moving off or traffic getting up to pace.
/// </param>
internal readonly record struct BodyAhead(
    BodySeen Seen, bool Read, bool AtRest, bool GoesNowhere, float AlongMps, bool Wheeled,
    float ClearsAtM = float.PositiveInfinity, bool Steady = false)
{
    public static BodyAhead Nothing => default;

    /// <summary>
    /// <b>Whether it is traffic holding the reader up</b> (CAR-53, AMB-4.4): wheeled, holding its pace, and going slower
    /// than the reader means to by more than <paramref name="byMps"/>.
    /// </summary>
    public bool IsTrafficSlowerThan(float meansMps, float byMps) => Wheeled && Steady && AlongMps < meansMps - byMps;
}

/// <summary>
/// Where on a car's line one of the ways under it begins and ends (<see cref="DrivingGround.WaysAlong"/>): the line metre
/// the way's own nought falls at, and the line metre its stretch of the line ends at.
/// </summary>
internal readonly record struct WaySpan(float StartM, float EndM);

/// <summary>What became of a car's kept plan at its turn (<see cref="DrivingGround.KeepsThePlan"/>).</summary>
internal enum PlanKept : byte
{
    /// <summary>Let go of, or to be: the plan is asked afresh.</summary>
    No,

    /// <summary>Kept as it is: it holds all the car needs.</summary>
    AsItIs,

    /// <summary>Kept, and asked further from where it is cut or where all it asked for ends.</summary>
    ToBeAskedFurther,
}
