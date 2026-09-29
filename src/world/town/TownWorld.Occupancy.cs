using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// One of the town's own ways under a stretch of one agent's line: which way, the metres of it the stretch
/// covers, and where the near end of that falls back on the line it came from.
/// </summary>
/// <remarks>
/// <see cref="LineFromM"/> is what makes the trip back cheap. A line's metres and a way's metres share no
/// origin — the assembler trims each lane by the setbacks its joins were taken at — so a distance read off
/// the index has to be carried home through the same offset it was carried out on.
/// </remarks>
internal readonly record struct LineWay(int Way, float FromM, float ToM, float LineFromM);

/// <summary>
/// <b>What one agent's plan was answered</b>, read against the reservations and not yet laid: where on its own
/// line it ends, the ground it keeps off what ended it, what that was, and on which way.
/// </summary>
/// <param name="CutLineM">Infinity where it can have all it asked for.</param>
/// <param name="CutBy"><see cref="LaneClaim.Nothing"/> where nothing ended it.</param>
/// <param name="CutOn">The way it was refused on, or <see cref="LaneOccupancy.NoHold"/>.</param>
/// <param name="CutAt">Which of the plan's pieces that was, or −1.</param>
/// <param name="CutWayM">
/// Where on that piece's own way it was refused — <b>the metre the answer was read at, and the one the piece is
/// laid to</b>. Carried to the line and back instead, it comes home a hair past where it was refused, and laid
/// there it takes that hair off whatever refused it: a light's hold, a secondary claim met at its very start,
/// went whole to a plan it had just refused (<see cref="LaneOccupancy.Take"/>).
/// </param>
internal readonly record struct PlanAnswer(
    float CutLineM, float MarginM, LaneClaim CutBy, int CutOn, int CutAt = -1, float CutWayM = float.PositiveInfinity)
{
    public static PlanAnswer Whole => new(float.PositiveInfinity, 0f, LaneClaim.Nothing, LaneOccupancy.NoHold);
}

/// <summary>
/// <b>The lane index</b> (<see cref="LaneOccupancy"/>): every body where its collider stands, every agent's
/// plan settled against every other, and the two questions a driver asks of it — what is in front of me on
/// the road I am driving, and how much of that road is mine.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the whole of what an ordinary agent reads of another</b> (TER-4c.5). A driver or a walker is
/// held off another body by that body's reservation on its own way, and off another agent's intentions by
/// the planned layer — never by reading the other agent itself.
/// </para>
/// <para>
/// <b>Following is a grant and not a reading.</b> Every agent plans from its nose to where it means to be
/// able to stop, and is granted what survives of that once every body and every other plan has been laid —
/// so nothing has to measure a gap to hold one, and the agent behind simply has less road to stop in.
/// </para>
/// <para>
/// <b>The passes are in this file, in order</b>: every walker's place on its walk, every body, every light's
/// hold, every plan, every plan settled against what the others came to, and last what each plan came to.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// How many ways one line may be cut into: every lane it is laid over, the join between each pair, and
    /// the one way at a bay it may finish on. A bound on a stack span and not a figure behaviour reads.
    /// </summary>
    public const int MostWaysAlongALine = (LineAssembler.MostLanes * 2) - 1 + 1;

    /// <summary>
    /// How many ways one body may be found over. A bound on a stack span; a way past it is dropped and
    /// counted (<see cref="RibbonAtlas.Dropped"/>), and a gate holds that count at nothing.
    /// </summary>
    const int MostWaysUnderABody = 48;

    /// <summary>
    /// <b>How many reservations one plan may lay</b>: a main claim on every way of its line, and a secondary
    /// claim for every mark those lie over (TER-5c.1).
    /// </summary>
    static int MostPlannedPer(int waysAlong, WayCrossings marks) => waysAlong * (1 + marks.MostCrossedByOne);

    /// <summary>
    /// <b>The index rebuilt from the bodies</b>, in phase 2, before anybody has decided anything. Every reader
    /// this tick therefore sees the same reservations, whatever tick its own decision clock came round on.
    /// </summary>
    /// <remarks>
    /// <b>Every body first and every plan after all of them</b> (TER-4c.2): a plan is cut at the first body in
    /// front of it, so what it is laid against is the whole of the town's bodies rather than whichever were
    /// written first. Plans are settled against each other as they are laid, by a comparison that does not
    /// depend on the order (<see cref="LaneOccupancy.Beats"/>), settled again once all of them are down
    /// (<see cref="SettleThePlans"/>), and what each came to is read after that.
    /// </remarks>
    void RebuildLaneOccupancy()
    {
        _occupancy.Begin();

        // Where every walker stands on its walk, before anything is laid: its body and its plan both begin
        // from it.
        for (var person = 0; person < People.Count; person++) StationTheWalker(person);

        for (var person = 0; person < People.Count; person++) LayTheWalkersBody(person);
        for (var car = 0; car < Cars.Count; car++) LayTheCarsBody(car);

        // The ground a pass will cover is a body's (TER-4c.6), and is down before anything is asked for.
        for (var car = 0; car < Cars.Count; car++) LayTheCarsPass(car);
        for (var person = 0; person < People.Count; person++) LayTheWalkersPass(person);

        // The lights before any plan: a light's hold is placed rather than asked for, so it has to be down before
        // the plans it refuses are answered (TLT-1).
        LightTheWays();

        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        for (var car = 0; car < Cars.Count; car++) PlanTheDrive(car, ways);

        Span<LineWay> walk = stackalloc LineWay[MostWaysAlongAWalk];
        for (var person = 0; person < People.Count; person++) PlanTheWalk(person, walk);

        SettleThePlans(ways, walk);

        // The ground a car backs up over is weaker than every plan (TER-4c.7), so it is asked once all of them are
        // settled and takes nothing any of them keeps — but what the car queued behind it could still stop short of.
        for (var car = 0; car < Cars.Count; car++) LayTheCarsBackUp(car);

        for (var car = 0; car < Cars.Count; car++) ReadTheGrant(car);
        for (var person = 0; person < People.Count; person++)
        {
            ReadTheWalkersGrant(person);
            ConsiderASidestep(person);
            AimTheWalker(person);
        }
    }

    /// <summary>
    /// <b>How many rebuilds ran out of settling passes with a plan still moving</b> since the town was laid
    /// (<see cref="SettleThePlans"/>) — a layer left disjoint but not where its answers say.
    /// </summary>
    public long Unsettled { get; private set; }

    /// <summary>How many plans have been taken up and laid again by the settling since the town was laid.</summary>
    public long PlansLaidAgain { get; private set; }

    /// <summary>
    /// <b>Every plan answered again against the finished layer, until none moves</b> — so a plan ends where
    /// something that beats it still stands, and not where something stood when it was laid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A cut frees ground, and whoever was refused that ground was answered before it was freed.</b> A car
    /// refused by a plan that a walker laid after both then cut back, or a plan refused on a later way by a
    /// piece its own taking removed on an earlier one: laid once, each stayed short of ground nobody held —
    /// every tick for as long as the town stood that way, since the holders are laid in the same order every
    /// tick.
    /// </para>
    /// <para>
    /// <b>And a plan cut after it was laid is held to the rules of one refused while it was laid</b>: a car
    /// refused anywhere between a zebra and the room clear of it waits short of the paint, whichever of the two
    /// holders was laid first (TER-5c.3).
    /// </para>
    /// <para>
    /// <b>Only a plan another plan ended is asked.</b> One that had all it asked for or that a body stopped has
    /// nothing to gain, since no body moves inside a tick. A light's hold and a closure are placed and never
    /// asked, so there is nothing of theirs to answer again.
    /// </para>
    /// <para>
    /// <b>The passes are bounded</b> (<see cref="RoadFigures.MostSettlingPasses"/>), and a ring is why. The
    /// comparison is total at one metre but not transitive along a line: one hold cut back frees ground a
    /// second takes, which cuts a third, which frees the ground the first was cut back from. Such a ring never
    /// settles, so a layer still moving at the bound is left as it stands — disjoint as ever, and
    /// counted. <b>It is an instrument's to report and not a gate's</b>: whether a town has rings is a fact
    /// about its junctions. Nearly everything moves in the first pass.
    /// </para>
    /// </remarks>
    void SettleThePlans(Span<LineWay> ways, Span<LineWay> walk)
    {
        if (_occupancy.Cuts == 0) return;

        for (var pass = 0; pass < _config.Road.MostSettlingPasses; pass++)
        {
            var laidAgain = 0;
            for (var car = 0; car < Cars.Count; car++) laidAgain += SettleTheDrive(car, ways) ? 1 : 0;
            for (var person = 0; person < People.Count; person++) laidAgain += SettleTheWalk(person, walk) ? 1 : 0;

            PlansLaidAgain += laidAgain;
            if (laidAgain == 0) return;
        }

        Unsettled++;
    }

    /// <summary>
    /// Whether this car is a driver on a route rather than a shape on the road — what decides whether it
    /// plans at all, and whether the ways of its own line are ways it is travelling.
    /// </summary>
    /// <remarks>
    /// <b>Its own state and nobody else's.</b> How far off its line the car is was measured by the sensing
    /// half of last tick; a hand at the wheel is the one case that leaves it standing at whatever the road
    /// last wrote, and it is named apart for that reason.
    /// </remarks>
    bool IsUnderWay(int car)
    {
        if (!Cars.Driven[car] || Cars.Broken[car]) return false;
        if (Cars.Line[car].LaneCount == 0 && Cars.LineWayOf(car) == CarFleet.NoWay) return false;
        if (HandAtTheWheel(car)) return false;

        return Cars.OffLineM[car] <= OffTheLineAllowanceM(car);
    }

    /// <summary>
    /// Where a place on one of this car's lanes falls on the line it is driving — <see cref="WaysAlong"/>'s
    /// own trip, made the other way round for one place instead of in bulk for a stretch.
    /// </summary>
    float OnTheLineM(int car, int slot, float alongLaneM) =>
        LineAssembler.OnTheLineM(Cars.LaneStartsOf(car), Cars.LaneEndsOf(car), slot, alongLaneM);

    /// <summary>
    /// The town's ways under a stretch of one car's line, nearest first — the lanes it is laid over and the
    /// joins threaded between them, each with the metres of its own that the stretch covers.
    /// </summary>
    /// <remarks>
    /// <b>The metres of a way and the metres of a line run at the same rate and start together</b> (TER-5d):
    /// the line over a lane is that lane's own arcs from its own first metre, so a stretch carried across is
    /// the same stretch of the same bending ground and not a chord over it.
    /// </remarks>
    public int WaysAlong(int car, float fromLineM, float toLineM, Span<LineWay> into)
    {
        // A line that <em>is</em> one of the town's ways — a bay's way out — is that way and no other, and
        // its metres are the line's own: there is no chain under it and no setback to carry across.
        var lineWay = Cars.LineWayOf(car);
        if (lineWay != CarFleet.NoWay)
        {
            return Overlaps(fromLineM, toLineM, 0f, Cars.Line[car].LengthM, out var wayFromM, out var wayToM)
                ? Written(into, new LineWay(lineWay, wayFromM, wayToM, wayFromM))
                : 0;
        }

        var lanes = Cars.Line[car].LaneCount;
        var chain = Cars.ChainOf(car);
        var starts = Cars.LaneStartsOf(car);
        var ends = Cars.LaneEndsOf(car);

        var written = 0;
        for (var index = 0; index < lanes && written < into.Length; index++)
        {
            var leavingOn = index < lanes - 1 ? _roads.ConnectorBetween(chain[index], chain[index + 1]) : RoadGraph.NoConnector;

            if (Overlaps(fromLineM, toLineM, starts[index], ends[index], out var fromM, out var toM))
            {
                into[written++] = new LineWay(
                    _ways.OfRoadLane(chain[index]), fromM - starts[index], toM - starts[index], fromM);
            }

            // <b>The stretch runs out at the box's near edge — `ends[index]` — and not at its far one.</b>
            // A join of no length has no ground between its two lanes and so nothing to write.
            if (leavingOn == RoadGraph.NoConnector || ends[index] >= toLineM) break;

            if (written < into.Length && starts[index + 1] > ends[index]
                && Overlaps(fromLineM, toLineM, ends[index], starts[index + 1], out fromM, out toM))
            {
                into[written++] = new LineWay(
                    _ways.OfRoadConnector(leavingOn), fromM - ends[index], toM - ends[index], fromM);
            }
        }

        // And the way the line finishes on, where it finishes on one (<see cref="CarFleet.TailWay"/>).
        var tail = Cars.TailWayOf(car);
        if (tail != CarFleet.NoWay && written < into.Length && lanes > 0
            && Overlaps(fromLineM, toLineM, ends[lanes - 1], Cars.Line[car].LengthM, out var tailFromM, out var tailToM))
        {
            into[written++] = new LineWay(
                tail, tailFromM - ends[lanes - 1], tailToM - ends[lanes - 1], tailFromM);
        }

        return written;
    }

    /// <summary>
    /// <b>Whether one of the town's ways is one this car's own line runs over</b> — the lanes of its chain,
    /// the joins between them, and the way its line is or finishes on — and <b>which way the line takes after
    /// it</b> (<see cref="LaneClaim.Onward"/>).
    /// </summary>
    /// <remarks>
    /// <b>A line's last lane is not where it ends until the car is at its end.</b> A line is laid a sight distance
    /// ahead and grown lane by lane (<see cref="LayLine"/>), so a car on the first part of a long lane is on the
    /// last lane of its line with its route running on: read as ending there, a queue on it was a car going
    /// nowhere to the car behind.
    /// </remarks>
    bool IsOnItsLine(int car, int way, out int onward)
    {
        onward = LaneOccupancy.NoWay;
        var tail = Cars.TailWayOf(car);
        if (Cars.LineWayOf(car) == way || tail == way) return true;

        var lanes = Cars.Line[car].LaneCount;
        var chain = Cars.ChainOf(car);
        for (var index = 0; index < lanes; index++)
        {
            var join = index == lanes - 1 ? RoadGraph.NoConnector : _roads.ConnectorBetween(chain[index], chain[index + 1]);
            var joinWay = join == RoadGraph.NoConnector ? LaneOccupancy.NoWay : _ways.OfRoadConnector(join);
            if (_ways.OfRoadLane(chain[index]) == way)
            {
                onward = joinWay != LaneOccupancy.NoWay ? joinWay
                    : index == lanes - 1 && tail != CarFleet.NoWay ? tail
                    : RunsOnPastItself(car) ? LaneOccupancy.RunsOn
                    : LaneOccupancy.NoWay;
                return true;
            }

            if (joinWay != LaneOccupancy.NoWay && joinWay == way)
            {
                onward = _ways.OfRoadLane(chain[index + 1]);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <b>Which way this car's line takes after one it runs over</b> (<see cref="IsOnItsLine"/>), or
    /// <see cref="LaneOccupancy.NoWay"/> — the movement a body in its way is weighed against (TER-4c.6).
    /// </summary>
    int OnwardAlongTheLine(int car, int way) => IsOnItsLine(car, way, out var onward) ? onward : LaneOccupancy.NoWay;

    /// <summary>
    /// Whether this car's line runs on past its leading edge. A line is the rear axle's (CAR-4a) and a car is
    /// brought to rest with that axle at its end, so one that has arrived is past it.
    /// </summary>
    bool RunsOnPastItself(int car) => Cars.Line[car].LengthM > Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);

    /// <summary>
    /// <b>The line any one of the town's ways is travelled on, and how wide it is</b> — a lane's own arcs, a
    /// junction's join, the way at a bay, one side of a pavement or the mitre at its corner. <b>The one
    /// place the kinds are told apart</b>, because this is the only slice that may know all of them.
    /// </summary>
    /// <remarks>
    /// <b>Each is the width that way is travelled at</b> and never a second figure: a lane's is the lane's, a
    /// join's the narrower of the two it joins (TER-5d.1), a bay's the space it serves, and a footway's and a
    /// mitre's the walking lane's. It is what the atlas lays every ribbon from (<see cref="RibbonAtlas"/>).
    /// </remarks>
    public ReadOnlySpan<ArcSeg> LineOfWay(int way, out float widthM)
    {
        switch (_ways.KindOf(way))
        {
            case WayKind.Lane:
                widthM = _roads.LaneWidthM[_ways.RoadLaneOf(way)];
                return _roads.ArcsOf(_ways.RoadLaneOf(way));

            case WayKind.Connector:
                var slot = _ways.RoadConnectorOf(way);
                widthM = _roads.ConnectorWidthM(slot);
                return _roads.ConnectorArcs(slot);

            case WayKind.Bay:
                widthM = _bayWays.Ways.LaneWidthM(way);
                return _bayWays.ArcsOf(way);

            case WayKind.Footway:
                var edge = _ways.FootwayOf(way);
                widthM = _walking.LaneWidthM(edge);
                return _walking.LaneOf(edge);

            default:
                var mitre = _ways.MitreOf(way);
                widthM = _walking.LaneWidthM(_walking.TurnToEdge(mitre));
                return _walking.JoinArcs(mitre);
        }
    }

    /// <summary>
    /// <b>Every way of the town as the ribbon the atlas is laid from</b> (TER-4c.4): its line swept to its
    /// own width, both as <see cref="LineOfWay"/> gives them, and the zebra it paints (TER-5c.3).
    /// </summary>
    sealed class TownRibbons(TownWorld town) : IRibbonLines
    {
        public int WayCount => town._ways.Count;

        public ReadOnlySpan<ArcSeg> LineOf(int way, out float widthM) => town.LineOfWay(way, out widthM);

        public int ZebraOf(int way) => town.ZebraOf(way);

        public bool IsDriven(int way) => town._ways.IsDriven(way);
    }

    /// <summary>
    /// <b>The zebra one of the town's ways is the paint of</b> — a walking lane over it, from one kerb to the
    /// other — or <see cref="RibbonMarks.NoZebra"/>.
    /// </summary>
    int ZebraOf(int way)
    {
        if (_ways.KindOf(way) != WayKind.Footway) return RibbonMarks.NoZebra;

        var crossing = _crossingEdges.CrossingOf(_ways.FootwayOf(way));
        return crossing == PersonFleet.NoCrossing ? RibbonMarks.NoZebra : crossing;
    }

    /// <summary>
    /// <b>The right of way a movement has on a way</b> (TER-5e) — its own through a box, and ordinary traffic
    /// everywhere else. A call's rung is the hold's and not the way's (<see cref="LevelTheRungs"/>, AMB-4).
    /// </summary>
    ClaimPriority RungOn(int way) =>
        _ways.KindOf(way) == WayKind.Connector ? _roads.FirmOnConnector(_ways.RoadConnectorOf(way)) : ClaimPriority.Firm;

    /// <summary>One way written into a caller's span, as the count of them it now holds.</summary>
    static int Written(Span<LineWay> into, in LineWay way)
    {
        if (into.Length == 0) return 0;

        into[0] = way;
        return 1;
    }

    static bool Overlaps(float fromM, float toM, float leastM, float mostM, out float fromOut, out float toOut)
    {
        fromOut = MathF.Max(fromM, leastM);
        toOut = MathF.Min(toM, mostM);
        return toOut > fromOut;
    }

    /// <summary>
    /// Where a place on a line falls in the own metres of one of the ways under it, held to the stretch of
    /// that way the caller is laying.
    /// </summary>
    static float OnTheWayM(in LineWay way, float lineM) =>
        Math.Clamp(way.FromM + (lineM - way.LineFromM), way.FromM, way.ToM);

    /// <summary>
    /// How far along the <paramref name="index"/>th piece of a plan its answer lets it be laid: the metre it was
    /// refused at on the piece it was refused on (<see cref="PlanAnswer.CutWayM"/>), and the answer carried
    /// over from the line on every piece before that.
    /// </summary>
    static float LaidToM(in PlanAnswer answer, int index, in LineWay way) =>
        index == answer.CutAt ? answer.CutWayM : OnTheWayM(way, answer.CutLineM);

    /// <summary>
    /// The same trip home: where a place in one way's own metres falls on the line that ran over it. <b>The
    /// pair of <see cref="OnTheWayM"/></b>, so that an answer carried out and an answer carried back cannot
    /// use two offsets.
    /// </summary>
    static float OnTheLineM(in LineWay way, float wayM) => way.LineFromM + (wayM - way.FromM);

    /// <summary>
    /// <b>What a reservation is to a driver reading it</b> — which is the reader's question and not the
    /// row's, answered off the row alone (TER-4c.5).
    /// </summary>
    /// <remarks>
    /// <b>A queue is a body travelling the way it is on</b> (<see cref="LaneClaim.OnItsLine"/>), which the
    /// body said of itself when it was laid; a body merely standing on the way is something in the way of
    /// whoever is travelling it. Ground somebody plans to use is a place to stop short of.
    /// </remarks>
    static HeadwayKind KindOf(in LaneClaim claim) => claim switch
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
