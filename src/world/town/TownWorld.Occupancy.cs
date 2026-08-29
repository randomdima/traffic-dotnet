using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// One of the town's own ways under a stretch of one car's line: which way, the metres of it the stretch
/// covers, and where the near end of that falls back on the line it came from.
/// </summary>
/// <remarks>
/// <see cref="LineFromM"/> is what makes the trip back cheap. A line's metres and a way's metres share no
/// origin — the assembler trims each lane by the setbacks its joins were taken at — so a distance read off
/// the index has to be carried home through the same offset it was carried out on.
/// </remarks>
internal readonly record struct LineWay(int Way, float FromM, float ToM, float LineFromM);

/// <summary>
/// <b>The lane index</b>: who is on each way of the road and which stretch of it each of them has been
/// granted, laid once a tick from the bodies themselves — and the two questions a driver asks of it: what
/// is in front of me on the road I am actually driving, and how much of that road is mine.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the whole of what a driver looks at.</b> A ray found a shape at a distance and a shape is the
/// same reading whether it is a driver waiting his turn or a wreck; the index knows which, because it is
/// built from the town's own arrays rather than from the geometry. Everything that can be on a lane is in
/// it — the traffic, anybody on foot in the lane, and the town's furniture (<see cref="StandingGround"/>) —
/// so there is no second mechanism behind it to catch what it left out.
/// </para>
/// <para>
/// <b>Following is a grant and not a reading.</b> Every driver asks for the road from its own tail to
/// where it plans to stop, and is granted what is left of it in front of the nearest car already on it —
/// so nothing has to measure a gap to hold one, and the car behind simply has less road to stop in and
/// plans a speed that fits it. <b>Nobody is granted ground another car will still be standing on when it
/// has stopped</b>, which is the one thing the whole arrangement is for.
/// </para>
/// <para>
/// <b>The ask, the answer, and then the ground is yours</b> (TER-4c.1). What comes back is regularly less
/// than what was asked for — cut at the first metre somebody else has, or at the place a rule stops the
/// asker — and a part of what was wanted is the ordinary answer rather than a refusal. <b>Past that the
/// holder moves without asking anything again</b>: nothing here is a permission handed out a second
/// time at the moment of moving, and whoever comes to that ground after is the one that has to stop.
/// </para>
/// <para>
/// <b>Identity and distance both from the index.</b> Everything in front is a stretch of the same way in the
/// same metres, so the gap is a subtraction (<see cref="LookAhead"/>) — and the reading and the grant are
/// then two walks of one book rather than two opinions about one road. Both are walked in by the ground
/// covered since they were taken rather than carried.
/// </para>
/// <para>
/// <b>A body takes ground on the ways it drives and on no others</b> (TER-5c.1). Inside a junction two ways
/// run over one piece of the world, and what settles that is a table filled once from the lines themselves
/// (<see cref="WayCrossings"/>): a driver looks its own way up and reads the metres named there in the
/// crossed way's own book, so its grant is cut by ground it will never be on without its ever having
/// written to it (<see cref="WhereTheGroundIsCrossed"/>). <b>A reservation is stated in one way's metres and
/// means something about the whole town</b>, which is what makes one body to a piece of ground true across a
/// box and not only along a lane.
/// </para>
/// <para>
/// <b>A body it does not describe is a body off the network</b> — one sliding into a lane from a collision,
/// one on ground no way owns. Those are the solver's, and a car under geometry of its own asks the ways
/// beneath that geometry instead (<see cref="GroundAhead"/>).
/// </para>
/// <para>
/// <b>The book is laid in four passes and this file holds the shape of it</b>: what each of them writes is
/// the walkers' (<see cref="PlaceTheWalkerOnTheRoad"/>), the bodies not driving a route of their own
/// (<see cref="PlaceWhatIsNotDriving"/>), the crossings of a junction (<see cref="PlaceTheCrossing"/>) and
/// the ask and the answer (<see cref="AskForTheGround"/>), each in the file its own name says.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// How many ways one line may be cut into: every lane it is laid over, the join between each pair, and
    /// the one way at a bay it may finish on. A bound on a stack span and not a figure behaviour reads.
    /// </summary>
    const int MostWaysAlongALine = (PathAssembler.MostLanes * 2) - 1 + 1;

    /// <summary>
    /// How many stretches a car under way may put in the index at once <em>on the road it is driving</em>:
    /// the ways its reservation runs over — which begins at its own tail, is a stopping distance long, and
    /// crosses a junction where one falls inside it — and the one way it can claim.
    /// </summary>
    /// <remarks>
    /// The bay a leg is on its way to is not one of them. It is a booking in the parking register and not a
    /// piece of road (<see cref="ParkingRegistry"/>); what the car takes of the bay's own way in is its
    /// reservation, and that is already counted here.
    /// </remarks>
    const int MostSlotsPerDrivingCar = 8;

    /// <summary>
    /// From how many places a body that is <em>not</em> driving a route is laid: both ends of the sweep it is
    /// committed to (<see cref="WhereTheTemplateSweepEndsM"/>), which for anything not on a template are the
    /// same place. Each costs the lane that end is nearest and every join of the junctions at either end of
    /// that lane (<see cref="LieInTheBox"/>).
    /// </summary>
    const int LyingPassesPerCar = 2;

    /// <summary>
    /// And how many one <em>stationary</em> body may lay, which is the ceiling on what
    /// <see cref="LyingBook"/> keeps for it: the lying half of <see cref="MostSlotsPerCar"/>, or the ways
    /// at the busiest bay where a bay has more of them than a junction has joins.
    /// </summary>
    static int MostLyingRowsPerCar(RoadGraph roads, BayWays bays)
    {
        var atABay = 0;
        for (var bay = 0; bay < bays.BayCount; bay++) atABay = Math.Max(atABay, bays.WayCountOf(bay));

        return Math.Max(LyingPassesPerCar * (1 + (2 * roads.MostTurnsAtANode)), atABay);
    }

    /// <summary>
    /// And how many any car may, which is the wider of the two shapes one can be in: a car under way, plus
    /// the runs of <em>its own</em> join the crossings on it take off it (<see cref="PlaceTheCrossing"/>);
    /// or a body standing still, laid where it lies and — where it is driving a template — at either end of
    /// the sweep that template has still to make (<see cref="WhereTheTemplateSweepEndsM"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One over the runs, because the run the car's own road ends inside is the one that comes in two
    /// pieces</b> (<see cref="LayTheMovement"/>). A reservation is one interval, so it can split one run and
    /// no more.
    /// </para>
    /// <para>
    /// <b>Nothing here is sized by the ways a car is driven <em>over</em></b> (TER-5c). A car takes ground
    /// on the ways it drives and on no others; where its line crosses another way, the grant is cut by
    /// looking that way up (<see cref="WhereTheGroundIsCrossed"/>) rather than by writing a stretch onto it.
    /// </para>
    /// </remarks>
    static int MostSlotsPerCar(RoadGraph roads, WayCrossings crossings) =>
        Math.Max(
            MostSlotsPerDrivingCar + crossings.MostOwnRuns + 1,
            LyingPassesPerCar * (1 + (2 * roads.MostTurnsAtANode)));

    /// <summary>
    /// How many stretches one walker may put in the <em>road's</em> book: the bands of a crossing it is
    /// standing on and the one in front of it, which at worst is every lane the crossing is painted
    /// across, or the single stretch of lane a body standing on bare tarmac covers.
    /// </summary>
    /// <remarks>
    /// Never less than one, because a town with no paint on it still has people who can stand in a road —
    /// and a dropped stretch here is a body no driver's grant is cut at.
    /// </remarks>
    static int MostRoadSlotsPerWalker(LaneFurniture furniture) => Math.Max(1, furniture.MostLanesUnderACrossing);

    /// <summary>
    /// <b>The index rebuilt from the bodies</b>, in phase 2, before any driver has decided anything. Every
    /// reader this tick therefore sees the same book, whatever tick its own decision clock came round on.
    /// </summary>
    /// <remarks>
    /// <b>Asked in one walk, granted in the next, and cut back to the answer in a third.</b> What a car is
    /// granted is its own stretch cut at the near edge of the nearest one already spoken for, and that near
    /// edge is a fact about a body rather than about who was served first — so every ask goes into the book
    /// before any of them is answered, and no car has to be ordered against another to get the same answer
    /// either way round. The cut is a walk of its own for the same reason
    /// (<see cref="CutTheGroundToTheGrant"/>): it moves far edges, which is what a movement's crossing
    /// question reads.
    /// </remarks>
    void RebuildLaneOccupancy()
    {
        _occupancy.Begin();
        _standing.LayInto(_occupancy);

        // Where every walker stands on the pavement's own network, before either book is laid: the road's
        // book needs it to say which lane a body on the paint stands in, and the pavement's book lays
        // that body's own ask from it.
        for (var person = 0; person < People.Count; person++) StationTheWalker(person);

        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        for (var car = 0; car < Cars.Count; car++)
        {
            PlaceWhatIsNotDriving(car);
            PlaceTheClaim(car);

            // The ask before the two claims that stand ahead of it, because each is only the ground the ask
            // did not reach (<see cref="LayTheMovement"/>) — laid the other way round they would be clipped
            // against the stretch this car held a tick ago.
            AskForTheGround(car, ways);
            PlaceTheCrossing(car);
            KeepTheBooking(car);
        }

        // <b>The walkers between the asks and the grants, because they are on both sides of one question</b>
        // (TER-4c). What a body at a kerb may step onto is whether a driver's road is over the band, so the
        // asks have to be laid before it; and a person in a lane cuts the road a driver is granted exactly as
        // a car standing there would, so the band has to be in the book before the grants are taken. Laid
        // after them, every band went into a book no driver read again until the next rebuild wiped it, and
        // the only thing holding a car off somebody on the paint was the crossing's own stop.
        for (var person = 0; person < People.Count; person++) PlaceTheWalkerOnTheRoad(person);

        // And the claims answered against the whole book, before anything is granted off it: a claim a
        // stronger movement has taken is ground its holder no longer has, so nothing granted below may be
        // cut at it (TER-5e).
        for (var car = 0; car < Cars.Count; car++) AnswerTheClaim(car);

        for (var car = 0; car < Cars.Count; car++) GrantTheGround(car, ways);

        // And the book left holding the answer rather than the question (TER-4c.1), which is what every
        // reader after this rebuild — the junction gate above all — is entitled to find in it.
        for (var car = 0; car < Cars.Count; car++) CutTheGroundToTheGrant(car, ways);
    }

    /// <summary>
    /// Whether this car is a driver on a route rather than a shape on the road — <b>the whole of what the
    /// index is for</b>. A car that is gets queued behind however long it stands; a car that is not gets
    /// driven round.
    /// </summary>
    /// <remarks>
    /// <b>Nothing here is worked out twice.</b> How far off its line the car is was measured by the sensing
    /// half of last tick and is a tick old, which is a tenth of a metre at town speed against a lane three
    /// metres wide; a hand at the wheel is the one case that leaves it standing at whatever the road last
    /// wrote, and it is named apart for that reason.
    /// </remarks>
    bool IsUnderWay(int car)
    {
        if (!Cars.Driven[car] || Cars.Broken[car]) return false;
        if (Cars.Line[car].LaneCount == 0 && Cars.LineWayOf(car) == CarFleet.NoWay) return false;
        if (HandAtTheWheel(car)) return false;

        // The same bar the road holds the car to before it calls the line lost. A car it still considers to
        // be on its line is a car going where that line goes, and reading one as an obstruction for having
        // taken a corner wide is how a driver ends up swerving round traffic.
        return Cars.OffLineM[car] <= OffTheLineAllowanceM(car);
    }

    /// <summary>
    /// Where a place on one of this car's lanes falls on the line it is driving — <see cref="WaysAlong"/>'s
    /// own trip, made the other way round for one place instead of in bulk for a stretch.
    /// </summary>
    float OnTheLineM(int car, int slot, float alongLaneM) =>
        PathAssembler.OnTheLineM(
            _roads, Cars.ChainOf(car), Cars.LaneStartsOf(car), Cars.LaneEndsOf(car), slot, alongLaneM);

    /// <summary>
    /// The town's ways under a stretch of one car's line, nearest first — the lanes it is laid over and the
    /// joins threaded between them, each with the metres of its own that the stretch covers.
    /// </summary>
    /// <remarks>
    /// <b>The metres of a way and the metres of a line run at the same rate</b> and differ only by where
    /// each lane's own start falls under the line (<see cref="PathAssembler.LaneOriginM"/>) — the line over
    /// a lane is that lane's own arcs, so a stretch carried across is the same stretch of the same bending
    /// ground and not a chord over it.
    /// </remarks>
    int WaysAlong(int car, float fromLineM, float toLineM, Span<LineWay> into)
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
        var arrivedOn = RoadGraph.NoTurn;
        for (var index = 0; index < lanes && written < into.Length; index++)
        {
            var leavingOn = index < lanes - 1 ? _roads.TurnSlot(chain[index], chain[index + 1]) : RoadGraph.NoTurn;

            // Where the lane's own metres begin under the line's: the assembler took the line from the
            // setback the arriving join was drawn to, and a lane measures everything from its own start.
            var originM = arrivedOn == RoadGraph.NoTurn ? 0f : _roads.JoinToM(arrivedOn);
            if (Overlaps(fromLineM, toLineM, starts[index], ends[index], out var fromM, out var toM))
            {
                into[written++] = new LineWay(
                    _occupancy.WayOfLane(chain[index]),
                    originM + fromM - starts[index],
                    originM + toM - starts[index],
                    fromM);
            }

            // <b>The stretch runs out at the box's near edge — `ends[index]` — and not at its far one.</b>
            // The join is the ground from there to `starts[index + 1]`, so a stretch ending anywhere inside
            // it still covers some of it; guarded on the far edge, a car approaching a junction lays
            // nothing on it until its own stretch reaches clear across.
            // A place cut into a road (GEN-4h) joins its two lanes at a point: there is no ground between
            // them and so nothing to write, and a slot spent on it is one the reservation has not got for
            // the lane past it.
            if (leavingOn == RoadGraph.NoTurn || ends[index] >= toLineM) break;

            if (written < into.Length && starts[index + 1] > ends[index]
                && Overlaps(fromLineM, toLineM, ends[index], starts[index + 1], out fromM, out toM))
            {
                into[written++] = new LineWay(
                    _occupancy.WayOfTurn(leavingOn), fromM - ends[index], toM - ends[index], fromM);
            }

            arrivedOn = leavingOn;
        }

        // And the way the line finishes on, where it finishes on one: the line into a bay leaves its lane
        // part-way along and runs to the pose the car is left in, so the last dozen metres of a leg are a
        // way of the book like every metre before them (<see cref="CarFleet.TailWay"/>).
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
    /// <b>The line any one of the book's ways is driven on, and how wide it is</b> — a lane's own arcs, a
    /// junction's join, or the way at a bay. <b>The one place the three bands are told apart</b>, because
    /// this is the only slice that may know all three: the book numbers them, the road drew two of them and
    /// `world/parking` drew the third, and a reader holding a way number knows none of that.
    /// </summary>
    /// <remarks>
    /// A way at a bay is drawn at the width of the lane it leaves, which is the road the car came off and
    /// the only width a manoeuvre off the carriageway has any claim to.
    /// </remarks>
    public ReadOnlySpan<ArcSeg> LineOfWay(int way, out float widthM)
    {
        if (_bayWays.IsBayWay(way))
        {
            widthM = _roads.LaneWidthM[_bayWays.LaneOf(way)];
            return _bayWays.ArcsOf(way);
        }

        var onLane = _occupancy.WayIsLane(way);
        var lane = onLane ? _occupancy.WayIndex(way) : _roads.TurnToLane(_occupancy.WayIndex(way));
        widthM = _roads.LaneWidthM[lane];
        return onLane ? _roads.ArcsOf(lane) : _roads.JoinArcs(_occupancy.WayIndex(way));
    }

    /// <summary>
    /// <b>The right of way whoever is on a way holds its ground with</b> (TER-5e) — the same three bands
    /// <see cref="LineOfWay"/> tells apart, asked the other question.
    /// </summary>
    /// <remarks>
    /// <b>Only a movement through a box has one of its own.</b> A lane is not a movement — two cars on one
    /// are held apart by the road each was granted and neither gives way to the other — and a way laid off
    /// the road is a car joining the traffic rather than one crossing it, which is ordinary traffic too.
    /// </remarks>
    RightOfWay RightOfWayOn(int way) =>
        _occupancy.WayIsLane(way) || _bayWays.IsBayWay(way)
            ? RightOfWay.Traffic
            : _roads.RightOfWayOfTurn(_occupancy.WayIndex(way));

    /// <summary>
    /// <b>The same question asked of a named car</b>, which is the one place a blue light gets into the
    /// road (AMB-4): an ambulance answering a call holds every stretch it asks for with
    /// <see cref="RightOfWay.Emergency"/>, whichever way it is on.
    /// </summary>
    /// <remarks>
    /// <b>It replaces the movement's rank rather than adding to it</b>, and that is the point: a rescue
    /// turning across the oncoming stream is not a turn that gives way, and one going straight on is not
    /// merely ordinary traffic. What the rank still cannot take is a body or the road a body is committed
    /// to (<see cref="LaneOccupancy.Binds"/>), so the priority is absolute over who waits and over nothing
    /// else.
    /// </remarks>
    RightOfWay RightOfWayOf(int car, int way) =>
        Cars.BlueLight[car] ? RightOfWay.Emergency : RightOfWayOn(way);

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
    /// <b>What is in front of this car on the road it is driving</b>, out to <paramref name="reachM"/> from
    /// its nose: the nearest body, <b>how far off it is</b>, and how far off the nearest stretch somebody
    /// else has claimed is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The distance is the book's too, and that is the change a ray's going made.</b> Both are stretches
    /// of the same way measured in the same metres, so the gap is a subtraction — where a cast had to walk
    /// three chains through a tree to find a shape it could not then name.
    /// </para>
    /// <para>
    /// <b>It reads the near edge of the body's own stretch and not its shape</b>, which is the conservative
    /// end of it: a body straddling a way is laid over the whole of what it covers, so the gap given is the
    /// short reading and a driver on it follows no closer than one that cast.
    /// </para>
    /// </remarks>
    void AheadOnThePath(int car, float noseM, float reachM, out LaneSlot body, out float bodyM, out float claimM)
    {
        body = LaneSlot.Nothing;
        bodyM = float.PositiveInfinity;
        claimM = float.PositiveInfinity;

        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        var count = WaysAlong(car, noseM, noseM + reachM, ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];

            if (!body.Found && _occupancy.AheadBody(way.Way, way.FromM, way.ToM, car, out body))
            {
                bodyM = MathF.Max(0f, OnTheLineM(way, body.FromM) - noseM);
            }

            if (float.IsPositiveInfinity(claimM)
                && _occupancy.AheadClaim(way.Way, way.FromM, way.ToM, car, out var claim))
            {
                claimM = MathF.Max(0f, OnTheLineM(way, claim.FromM) - noseM);
            }

            if (body.Found && !float.IsPositiveInfinity(claimM)) break;
        }
    }

    /// <summary>What a slot the index laid is to a driver reading it.</summary>
    static HeadwayKind KindOf(LaneUse use) => use switch
    {
        LaneUse.Reserved => HeadwayKind.Queue,
        LaneUse.Claimed => HeadwayKind.Claimed,
        LaneUse.OnFoot => HeadwayKind.Walker,
        _ => HeadwayKind.Obstruction,
    };
}
