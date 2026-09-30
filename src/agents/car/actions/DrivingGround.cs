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
/// this rebuild — and the questions every action asks of them, about a car's own line over the ways and about the
/// ground a body swept down a pass or a manoeuvre stands on.
/// </summary>
/// <remarks>
/// <b>It is the whole of what an action sees of another agent</b> (TER-4c.5): the reservations and nothing else. What
/// only the town knows — the leg, the errand, the tyres — an action asks of <see cref="ICarTown"/>.
/// </remarks>
internal sealed class DrivingGround(
    CarFleet cars, LaneOccupancy occupancy, RibbonAtlas atlas, TownWays ways, RoadGraph roads, WayLines lines,
    SimConfig config)
{
    /// <summary>
    /// How many ways one line may be cut into: every lane it is laid over and the join between each pair. A
    /// bound on a stack span and not a figure behaviour reads.
    /// </summary>
    public const int MostWaysAlongALine = (LineAssembler.MostLanes * 2) - 1;

    public CarFleet Cars { get; } = cars;

    public LaneOccupancy Occupancy { get; } = occupancy;

    public RibbonAtlas Atlas { get; } = atlas;

    public TownWays Ways { get; } = ways;

    public RoadGraph Roads { get; } = roads;

    public WayLines Lines { get; } = lines;

    public SimConfig Config { get; } = config;

    /// <summary>
    /// <b>The plan each car laid down its own line this rebuild</b>, or <see cref="LaneOccupancy.NoHold"/> — the one
    /// claim two actions share (TER-4c.8), and what a car was cut at is read off.
    /// </summary>
    public int[] PlanHold { get; } = new int[cars.Capacity];

    /// <summary>
    /// <b>A car's claim of the rebuild before, forgotten</b> before its action lays this rebuild's: no hold, a plan
    /// from its nose to its nose, and <b>nothing granted</b> (TER-4c.8) — a car moves over ground its action has
    /// claimed this rebuild and over no other, so what the action lays is the only thing that grants it road.
    /// </summary>
    /// <returns>The ground it was kept off what cut it, which the plan it lays next keeps (<see cref="Following.Plan"/>).</returns>
    public float ClearTheClaim(int car)
    {
        PlanHold[car] = LaneOccupancy.NoHold;

        var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);
        var keptOffM = Cars.GrantMarginM[car];
        Cars.ClaimFromM[car] = noseM;
        Cars.ClaimToM[car] = noseM;
        Cars.CommittedToM[car] = noseM;
        Cars.HorizonM[car] = float.PositiveInfinity;
        Cars.GrantMarginM[car] = 0f;
        Cars.GrantCutBy[car] = HeadwayKind.Nothing;
        Cars.AuthorityM[car] = 0f;
        return keptOffM;
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
        var hold = PlanHold[car];
        if (hold == LaneOccupancy.NoHold) return;

        var endsAtM = Occupancy.HoldEndsAtM(hold, out var marginM, out var cutBy);
        if (float.IsPositiveInfinity(endsAtM))
        {
            Cars.AuthorityM[car] = float.PositiveInfinity;
            return;
        }

        Cars.AuthorityM[car] = endsAtM - marginM - Cars.ClaimFromM[car];
        Cars.GrantMarginM[car] = marginM;
        Cars.GrantCutBy[car] = cutBy.Found ? KindOf(cutBy) : HeadwayKind.Claimed;
    }

    /// <summary>
    /// <b>A car nobody in the town is driving holds the road it can no longer stop short of</b> (TER-5e, TER-4c.8) —
    /// one under a hand (CTL-5) and one on a bar (EVA-5): the stretch straight ahead of it that its speed carries it
    /// over before it could be at rest, on every way the atlas finds under it, as committed ground under
    /// <paramref name="occupant"/> — so the traffic plans round what the hand or the truck cannot help, and nothing
    /// is said about where either means to go.
    /// </summary>
    /// <remarks>
    /// <b>Its own hold and never the drive's</b>: it drives no line, so nothing here is asked again when the plans
    /// are settled, and no grant is read back — the hand or the truck is the car's whole answer (S-7).
    /// </remarks>
    /// <param name="occupant">Whose ground it is held as: the car's own, or the truck's it is laid under.</param>
    [System.Runtime.CompilerServices.SkipLocalsInit]
    public void HoldWhatCannotBeStoppedShortOf(int car, int occupant)
    {
        var velocity = Cars.VelocityMps[car];
        var speedMps = velocity.Length();
        if (speedMps <= Config.Driving.StopSpeedMps) return;

        ref readonly var build = ref Cars.BuildOf(car);
        var brakingMps2 = CarFollower.BrakingMps2(Config, build, Cars.GroundCoefficient[car]);
        var committedM = (speedMps * Config.CarReactionS) + StoppingM(speedMps, brakingMps2);
        var travel = velocity / speedMps;
        var frontM = build.HalfLengthM;

        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        var count = Atlas.UnderBox(
            Cars.PositionM[car] + (travel * (frontM + (committedM * 0.5f))), travel, committedM * 0.5f,
            build.WidthM * 0.5f, under);
        if (count == 0) return;

        var hold = Occupancy.BeginHold(Config.Driving.StandOffM);
        for (var at = 0; at < count; at++)
        {
            ref readonly var cover = ref under[at];
            var line = Lines.LineOf(cover.Way, out _);
            var aheadM = MathF.Max(0f, MathF.Min(
                System.Numerics.Vector2.Dot(Spline.SampleAt(line, cover.FromM).PositionM - Cars.PositionM[car], travel),
                System.Numerics.Vector2.Dot(Spline.SampleAt(line, cover.ToM).PositionM - Cars.PositionM[car], travel)) - frontM);
            var ask = new PlannedAsk(
                hold, occupant, LaneRoster.Driving, ClaimPriority.Firm, cover.FromM, aheadM, aheadM,
                float.PositiveInfinity, speedMps);

            // Never over a body (TER-4c.1): what is standing there is what the car is about to meet.
            Occupancy.Take(ask, cover.Way, Occupancy.Reach(ask, cover.Way, cover.ToM, cover.FromM, out _));
        }

        Occupancy.EndHold(hold, float.PositiveInfinity, 0f, LaneClaim.Nothing);
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
    public int WaysAlong(int car, float fromLineM, float toLineM, Span<LineWay> into)
    {
        var lanes = Cars.Line[car].LaneCount;
        var chain = Cars.ChainOf(car);
        var starts = Cars.LaneStartsOf(car);
        var ends = Cars.LaneEndsOf(car);

        var written = 0;
        for (var index = 0; index < lanes && written < into.Length; index++)
        {
            var leavingOn = index < lanes - 1 ? Roads.ConnectorBetween(chain[index], chain[index + 1]) : RoadGraph.NoConnector;
            var endsM = index < lanes - 1 ? ends[index] : starts[index] + Roads.LaneLengthM[chain[index]];

            if (Overlaps(fromLineM, toLineM, starts[index], endsM, out var fromM, out var toM))
            {
                into[written++] = new LineWay(
                    Ways.OfRoadLane(chain[index]), fromM - starts[index], toM - starts[index], fromM);
            }

            // <b>The stretch runs out at the box's near edge — `ends[index]` — and not at its far one.</b>
            // A join of no length has no ground between its two lanes and so nothing to write.
            if (leavingOn == RoadGraph.NoConnector || ends[index] >= toLineM) break;

            if (written < into.Length && starts[index + 1] > ends[index]
                && Overlaps(fromLineM, toLineM, ends[index], starts[index + 1], out fromM, out toM))
            {
                into[written++] = new LineWay(
                    Ways.OfRoadConnector(leavingOn), fromM - ends[index], toM - ends[index], fromM);
            }
        }

        return written;
    }

    /// <summary>
    /// <b>Whether one of the town's ways is one this car's own line runs over</b> — the lanes of its chain and
    /// the joins between them — and <b>which way the line takes after it</b> (<see cref="LaneClaim.Onward"/>). A
    /// manoeuvre at a bay runs over none: the car making one is standing on whatever it is over.
    /// </summary>
    /// <remarks>
    /// <b>A line's last lane is not where it ends until the car is at its end.</b> A line is laid a sight distance
    /// ahead and grown lane by lane, so a car on the first part of a long lane is on the last lane of its line with
    /// its route running on: read as ending there, a queue on it was a car going nowhere to the car behind.
    /// </remarks>
    public bool IsOnItsLine(int car, int way, out int onward)
    {
        onward = LaneOccupancy.NoWay;
        var lanes = Cars.Line[car].LaneCount;
        var chain = Cars.ChainOf(car);
        for (var index = 0; index < lanes; index++)
        {
            var join = index == lanes - 1 ? RoadGraph.NoConnector : Roads.ConnectorBetween(chain[index], chain[index + 1]);
            var joinWay = join == RoadGraph.NoConnector ? LaneOccupancy.NoWay : Ways.OfRoadConnector(join);
            if (Ways.OfRoadLane(chain[index]) == way)
            {
                onward = joinWay != LaneOccupancy.NoWay ? joinWay
                    : RunsOnPastItself(car) ? LaneOccupancy.RunsOn
                    : LaneOccupancy.NoWay;
                return true;
            }

            if (joinWay != LaneOccupancy.NoWay && joinWay == way)
            {
                onward = Ways.OfRoadLane(chain[index + 1]);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <b>Which way this car's line takes after one it runs over</b> (<see cref="IsOnItsLine"/>), or
    /// <see cref="LaneOccupancy.NoWay"/> — the movement a body in its way is weighed against (TER-4c.6).
    /// </summary>
    public int OnwardAlongTheLine(int car, int way) => IsOnItsLine(car, way, out var onward) ? onward : LaneOccupancy.NoWay;

    /// <summary>
    /// Whether this car's line runs on past its leading edge. A line is the rear axle's (CAR-4a) and a car is
    /// brought to rest with that axle at its end, so one that has arrived is past it.
    /// </summary>
    public bool RunsOnPastItself(int car) => Cars.Line[car].LengthM > Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);

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
    /// <b>Whether a car's pass or manoeuvre is laid on a way</b>: the ways the traffic drives, and never the pavement
    /// — whose band may lie over the kerb (WLK-16), and whose walkers a pass that held it would hold on the kerb they
    /// stand on.
    /// </summary>
    public bool IsCarriageway(int way) => Ways.IsDriven(way);

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
    /// <b>Somebody on foot on the paint of a zebra a stretch of one way lies under</b> — anywhere on any of its
    /// walking lanes, kerb to kerb — and the walking lane they are on.
    /// </summary>
    public bool SomebodyOnThePaint(in WayCover swept, out LaneClaim body, out int on)
    {
        foreach (ref readonly var mark in Occupancy.Marks.Of(swept.Way))
        {
            if (mark.MineFromM >= swept.ToM) break;
            if (mark.MineToM <= swept.FromM || Lines.ZebraOf(mark.OnWay) == RibbonMarks.NoZebra) continue;
            if (!Occupancy.AheadBodyOf(LaneRoster.Walking, mark.OnWay, mark.FromM, mark.ToM, out body)) continue;

            on = mark.OnWay;
            return true;
        }

        body = LaneClaim.Nothing;
        on = LaneOccupancy.NoHold;
        return false;
    }

    /// <summary>
    /// <b>The stretch of a way ground swept as a body holds where the body is over part of it</b>: that part, or — for
    /// a movement through a box — the whole movement.
    /// </summary>
    /// <remarks>
    /// <b>A box is held a movement at a time and never a piece of one</b>, as a car that is in one plans the rest of
    /// the join: held in part, a car crossing the box was let in and cut in the middle of it, its body standing over
    /// the ground of the pass on the movement beside its own, the pass waiting on it and it on the pass. Held
    /// whole, it waits at the mouth.
    /// </remarks>
    public WayCover HeldAsABody(in WayCover cover) =>
        Ways.KindOf(cover.Way) == WayKind.Connector ? cover with { FromM = 0f, ToM = Ways.LengthM(cover.Way) } : cover;

    /// <summary>
    /// <b>How many stations a car's swept ground is read at</b>, both ends among them — half a car's width of line
    /// apart, so the body turning between two is covered by the two to within a hair.
    /// </summary>
    public int StationsOfTheSweep(int car, float fromM, float toM) =>
        toM <= fromM ? 0 : (int)MathF.Ceiling((toM - fromM) / Cars.BuildOf(car).FlankM) + 1;

    /// <summary>
    /// <b>What a reservation is to a driver reading it</b> — which is the reader's question and not the
    /// row's, answered off the row alone (TER-4c.5).
    /// </summary>
    /// <remarks>
    /// <b>A queue is a body travelling the way it is on</b> (<see cref="LaneClaim.OnItsLine"/>), which the
    /// body said of itself when it was laid; a body merely standing on the way is something in the way of
    /// whoever is travelling it. Ground somebody plans to use is a place to stop short of.
    /// </remarks>
    public static HeadwayKind KindOf(in LaneClaim claim) => claim switch
    {
        { Found: false } => HeadwayKind.Nothing,
        { HasBody: false, Of: LaneRoster.Signal } => HeadwayKind.Light,
        { HasBody: false } => HeadwayKind.Claimed,
        { Passing: true } => HeadwayKind.Passing,
        { Of: LaneRoster.Walking } => HeadwayKind.Walker,
        { OnItsLine: true } => HeadwayKind.Queue,
        _ => HeadwayKind.Obstruction,
    };
}
