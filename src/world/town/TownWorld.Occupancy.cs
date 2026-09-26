using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
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
/// <b>The passes are in this file, in order</b>: every walker's place on its walk, every body, every plan,
/// and last what each plan came to.
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
    /// <b>How many reservations one plan may lay</b>: a piece on every way of its line, and every section of
    /// another way those pieces are marked against (TER-5c.1).
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
    /// depend on the order (<see cref="LaneOccupancy.Beats"/>), and what each came to is read once all of
    /// them are down.
    /// </remarks>
    void RebuildLaneOccupancy()
    {
        _occupancy.Begin();

        // Where every walker stands on its walk, before anything is laid: its body and its plan both begin
        // from it.
        for (var person = 0; person < People.Count; person++) StationTheWalker(person);

        for (var person = 0; person < People.Count; person++) LayTheWalkersBody(person);
        for (var car = 0; car < Cars.Count; car++) LayTheCarsBody(car);

        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        for (var car = 0; car < Cars.Count; car++) PlanTheDrive(car, ways);

        for (var car = 0; car < Cars.Count; car++)
        {
            KeepTheBay(car);
            CloseTheRoad(car);
        }

        Span<LineWay> walk = stackalloc LineWay[MostWaysAlongAWalk];
        for (var person = 0; person < People.Count; person++) PlanTheWalk(person, walk);

        for (var car = 0; car < Cars.Count; car++) ReadTheGrant(car);
        for (var person = 0; person < People.Count; person++) ReadTheWalkersGrant(person);
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
    /// the joins between them, and the way its line is or finishes on.
    /// </summary>
    bool IsOnItsLine(int car, int way)
    {
        if (Cars.LineWayOf(car) == way || Cars.TailWayOf(car) == way) return true;

        var lanes = Cars.Line[car].LaneCount;
        var chain = Cars.ChainOf(car);
        for (var index = 0; index < lanes; index++)
        {
            if (_ways.OfRoadLane(chain[index]) == way) return true;
            if (index == lanes - 1) break;

            var connector = _roads.ConnectorBetween(chain[index], chain[index + 1]);
            if (connector != RoadGraph.NoConnector && _ways.OfRoadConnector(connector) == way) return true;
        }

        return false;
    }

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
    /// <b>Every way of the town as the ribbon the atlas is laid from</b> (TER-4c.4): its line
    /// (<see cref="LineOfWay"/>), swept to the width of what travels it — a car on anything driven, a body on
    /// anything walked.
    /// </summary>
    sealed class TownRibbons(TownWorld town) : IRibbonLines
    {
        public int WayCount => town._ways.Count;

        public ReadOnlySpan<ArcSeg> LineOf(int way, out float widthM)
        {
            widthM = town._ways.IsDriven(way) ? town._config.Car.WidthM : town._config.PersonDiameterM;
            return town.LineOfWay(way, out _);
        }
    }

    /// <summary>
    /// <b>The right of way a car plans a way at</b> (TER-5e) — a movement's own through a box, ordinary
    /// traffic everywhere else, and a call's wherever a car is answering one (AMB-4).
    /// </summary>
    ClaimPriority RungOn(int car, int way)
    {
        if (Cars.BlueLight[car]) return ClaimPriority.Special;

        return _ways.KindOf(way) == WayKind.Connector
            ? _roads.FirmOnConnector(_ways.RoadConnectorOf(way))
            : ClaimPriority.Firm;
    }

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
    /// The same trip home: where a place in one way's own metres falls on the line that ran over it. <b>The
    /// pair of <see cref="OnTheWayM"/></b>, so that an answer carried out and an answer carried back cannot
    /// use two offsets.
    /// </summary>
    static float OnTheLineM(in LineWay way, float wayM) => way.LineFromM + (wayM - way.FromM);

    /// <summary>
    /// <b>What is in front of this car on the road it is driving</b>, out to <paramref name="reachM"/> from
    /// its nose: the nearest body on the ways of its own line, and how far off it is.
    /// </summary>
    /// <remarks>
    /// <b>The distance comes off the reservations</b>: both are stretches of the same way measured in the
    /// same metres, so the gap is a subtraction. A body crossing the line from another way is on this one
    /// wherever its collider is, so the ways of the line are the whole of what is asked.
    /// </remarks>
    void AheadOnTheLine(int car, float noseM, float reachM, out LaneClaim body, out float bodyM)
    {
        body = LaneClaim.Nothing;
        bodyM = float.PositiveInfinity;

        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        var count = WaysAlong(car, noseM, noseM + reachM, ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            if (!_occupancy.AheadBody(way.Way, way.FromM, way.ToM, LaidAs(car), out body)) continue;

            bodyM = MathF.Max(0f, OnTheLineM(way, body.FromM) - noseM);
            return;
        }
    }

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
        { HasBody: false } => HeadwayKind.Claimed,
        { Of: LaneRoster.Walking } => HeadwayKind.Walker,
        { OnItsLine: true } => HeadwayKind.Queue,
        _ => HeadwayKind.Obstruction,
    };
}
