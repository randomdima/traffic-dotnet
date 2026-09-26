using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
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
/// then two walks of one set of claims rather than two opinions about one road. Both are walked in by the ground
/// covered since they were taken rather than carried.
/// </para>
/// <para>
/// <b>A body takes ground on the ways it drives and stands on, and on no others</b> (TER-5c.1). Inside a
/// junction two ways run over one piece of the world, and what settles that is a table filled once from the
/// lines themselves (<see cref="WayCrossings"/>): a stretch is weighed against the crossed way's own claims at
/// the metres named there as it is laid (<see cref="LaneOccupancy.AcrossTheWays"/>), and a driver's nose is
/// cut short of the first crossing whose ground binds it (<see cref="WhereTheGroundIsCrossed"/>) — so its
/// grant is cut by ground it will never be on without its ever having written to it. <b>A claim is stated in one way's metres and
/// means something about the whole town</b>, which is what makes one body to a piece of ground true across a
/// box and not only along a lane.
/// </para>
/// <para>
/// <b>A body it does not describe is a body off the network</b> — one sliding into a lane from a collision,
/// one on ground no way owns. Those are the solver's, and a car under geometry of its own asks the ways
/// beneath the body instead.
/// </para>
/// <para>
/// <b>The claims are laid in passes and this file holds the order of them</b>: what each of them writes is
/// the walkers' (<see cref="StandInTheRoad"/>), the ground every body stands on
/// (<see cref="PlaceTheBody"/>), the crossings of a junction (<see cref="PlaceTheCrossing"/>) and
/// the ask and the answer (<see cref="AskForTheGround"/>), each in the file its own name says.
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
    /// How many stretches a car under way may put in the index at once <em>on the road it is driving</em>:
    /// the ways its committed claim runs over, the ways the road it means to use runs over beyond that
    /// (TER-5g), and the ways it can claim ahead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two asks together reach no further than the line</b> (<see cref="MostWaysAlongALine"/>), which
    /// is what bounds this: they are one run of the car's own line cut in two at the far edge of the
    /// committed claim, so the pair costs the ways of the line and one more for the way that seam falls inside.
    /// </para>
    /// <para>
    /// The bay a leg is on its way to is not one of them. It is a claim in the parking register and not a
    /// piece of road (<see cref="ParkingRegistry"/>); what the car takes of the bay's own way in is its
    /// claim on the road, and that is already counted here.
    /// </para>
    /// </remarks>
    const int MostSlotsPerDrivingCar = MostWaysAlongALine + 1;

    /// <summary>
    /// From how many places a body is laid where it lies: both ends of the stretch it is committed to,
    /// which for a car holding its own line are the same place.
    /// Each costs the lane that end is nearest, the lane running back the other way, and every join of the
    /// junctions at either end of that lane (<see cref="GroundUnder"/>).
    /// </summary>
    const int LyingPassesPerCar = 2;

    /// <summary>
    /// And how many claims one body may lay on the <em>road</em> from its pose: every way under it at
    /// each end of its sweep, on the carriageway and on the bays it is standing across — both of which are
    /// numbered there and walked together (<see cref="TheRoadsGroundUnder"/>).
    /// </summary>
    static int MostLyingRowsPerCar(in RoadWays roads, in BayNetwork bays) =>
        LyingPassesPerCar * MostWaysUnderAPlaceOnTheRoad(roads, bays);

    /// <summary>How many ways the road numbers one place may stand on: both of its networks at once.</summary>
    static int MostWaysUnderAPlaceOnTheRoad(in RoadWays roads, in BayNetwork bays) =>
        roads.MostWaysUnderAPlace + bays.MostWaysUnderAPlace;

    /// <summary>
    /// And how many it may lay on the pavement, which is the same walk over the other network
    /// (<see cref="LieOnThePavement"/>) — every way under the box at each end of its sweep.
    /// </summary>
    static int MostPavementRowsPerCar(in PavementWays pavement) =>
        LyingPassesPerCar * pavement.MostWaysUnderAPlace;

    /// <summary>
    /// And how many any car may, which is <b>every shape one can be in at once</b>: the road it is driving,
    /// the runs of <em>its own</em> join the crossings on it take off it (<see cref="PlaceTheCrossing"/>),
    /// and the ground it is standing on wherever that is not the road it is driving
    /// (<see cref="PlaceTheBody"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A sum and not the wider of two.</b> A body writes the ground it occupies whatever it is doing
    /// (TER-4c.2), so a driver under way has both its committed claim and one on every way its own line does
    /// not name — the lane it is reaching into, the join it is lying across.
    /// </para>
    /// <para>
    /// <b>One over the runs, because the run the car's own road ends inside is the one that comes in two
    /// pieces</b> (<see cref="LayTheMovement"/>). A claim is one interval, so it can split one run and
    /// no more.
    /// </para>
    /// <para>
    /// <b>Nothing here is sized by the ways a car is driven <em>over</em></b> (TER-5c). A car takes ground
    /// on the ways it drives and on those it stands on; where its line crosses another way it stands on
    /// none of, its stretch is settled against that way's claims as it is laid
    /// (<see cref="LaneOccupancy.AcrossTheWays"/>) and its grant is cut by looking that way up
    /// (<see cref="WhereTheGroundIsCrossed"/>), rather than by writing a stretch onto it.
    /// </para>
    /// </remarks>
    static int MostSlotsPerCar(in RoadWays roads, WayCrossings crossings, in BayNetwork bays) =>
        MostSlotsPerDrivingCar + crossings.MostOwnRuns + 1 + MostLyingRowsPerCar(roads, bays);

    /// <summary>
    /// How many claims one walker may lay on the <em>road</em>: every way of the road the place it stands on
    /// is under (<see cref="TheRoadsGroundUnder"/>), since a body in a junction is on the joins that run
    /// beneath it and a body in a bay is on that bay's ways like anything else — <b>and the band of every
    /// lane a crossing it is walking is painted across</b> (PER-27).
    /// </summary>
    /// <remarks>
    /// <b>A sum and not the wider of the two</b>: a body on a zebra is standing on the lane under it and
    /// reserving its way to the far kerb at the same moment. Never less than one lane's worth, because a town
    /// with no paint on it still has people who can stand in a road — and a dropped stretch here is a body no
    /// driver's grant is cut at.
    /// </remarks>
    static int MostRoadSlotsPerWalker(in RoadWays roads, in BayNetwork bays, LaneFurniture furniture) =>
        MostWaysUnderAPlaceOnTheRoad(roads, bays) + Math.Max(1, furniture.MostLanesUnderACrossing);

    /// <summary>
    /// <b>The index rebuilt from the bodies</b>, in phase 2, before any driver has decided anything. Every
    /// reader this tick therefore sees the same claims, whatever tick its own decision clock came round on.
    /// </summary>
    /// <remarks>
    /// <b>Asked in one walk, granted in the next, and cut back to the answer in a third.</b> What a car is
    /// granted is its own stretch cut at the near edge of the nearest one already spoken for, and that near
    /// edge is a fact about a body rather than about who was served first — so every ask is laid
    /// before any of them is answered, and no car has to be ordered against another to get the same answer
    /// either way round. The cut is a walk of its own for the same reason
    /// (<see cref="CutTheGroundToTheGrant"/>): it moves far edges, which is what a movement's crossing
    /// question reads.
    /// </remarks>
    void RebuildLaneOccupancy()
    {
        _occupancy.Begin();
        _standing.LayInto(_occupancy);

        // Where every walker stands on the pavement's own ways, before anything is laid: the carriageway
        // needs it to say which lane a body on the paint stands in, and the walker's own claims begin from
        // it. It is also what says which of PER-25's two walks each body is taking.
        for (var person = 0; person < People.Count; person++) StationTheWalker(person);

        // <b>And every walker's own ground, at p0, before any of it</b> (PER-26, TER-5g). A body is the one
        // hold nothing takes, so a car's ask is laid against the whole of what is standing in the town
        // rather than against whichever bodies happened to be written first.
        Span<LineWay> walk = stackalloc LineWay[MostWaysAlongAWalk];
        for (var person = 0; person < People.Count; person++)
        {
            HoldThePavementUnderIt(person);
            StandInTheRoad(person);
        }

        // <b>Every body first, and every claim ahead after all of them</b>. A body is the one hold nothing can
        // take, so what is laid is the whole of the town's ground rather than whatever the cars before this
        // one in the fleet happened to have laid — and a claim that is only the ground its holder has not
        // reached (<see cref="LayTheMovement"/>) needs that holder's own road and own body already down, or
        // it is clipped against the stretch the car held a tick ago.
        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        for (var car = 0; car < Cars.Count; car++)
        {
            AskForTheGround(car, ways);
            PlaceTheBody(car);
        }

        for (var car = 0; car < Cars.Count; car++)
        {
            PlaceTheCrossing(car);
            KeepTheBay(car);
            CloseTheRoad(car);
        }

        // <b>And what every walker says it is walking at, at p12</b> (PER-26), between the drivers' asks and
        // their grants. A statement is in the weakest band there is, so it goes in after every body and every
        // ask. <b>It is stated on the pavement and on nothing else</b>: a walker's statement buys it no
        // standing on a carriageway, there being nothing on this side of the town that grants one.
        for (var person = 0; person < People.Count; person++) StateThePavementAhead(person, walk);

        for (var car = 0; car < Cars.Count; car++) GrantTheGround(car, ways);

        // And every claim left holding the answer rather than the question (TER-4c.1), which is what every
        // reader after this rebuild — the junction gate above all — is entitled to find in it.
        for (var car = 0; car < Cars.Count; car++) CutTheGroundToTheGrant(car, ways);

        // <b>And the ladder read along each hold once every metre of it is settled</b> (TER-5g.1). It is the
        // last pass because it is the only one that can be: the rung the road to a box is worth is the rung
        // the box is worth, and whether the car was granted the road in between is the answer above.
        for (var car = 0; car < Cars.Count; car++) LevelTheRungToTheBox(car, ways);

        // <b>And last of all, the crossings the walkers want</b> (PER-27). A reservation refuses nobody, so
        // nothing laid above this reads one and laying it here costs the traffic nothing — and what it buys
        // is that <em>whether it went in</em> is an answer about the settled town rather than about whoever
        // happened to have been written before it. It is the one claim on this side of the town that is
        // answered, and the answer is the whole of what a walker waits on (SIM-7).
        for (var person = 0; person < People.Count; person++) ReserveTheCrossing(person);
    }

    /// <summary>
    /// Whether this car is a driver on a route rather than a shape on the road — <b>the whole of what the
    /// index is for</b>: a car that is gets queued behind however long it stands.
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
            // The join is the ground from there to `starts[index + 1]`, so a stretch ending anywhere inside
            // it still covers some of it; guarded on the far edge, a car approaching a junction lays
            // nothing on it until its own stretch reaches clear across.
            // A join of no length has no ground between its two lanes and so nothing to write, and a slot
            // spent on it is one the claim has not got for the lane past it.
            if (leavingOn == RoadGraph.NoConnector || ends[index] >= toLineM) break;

            if (written < into.Length && starts[index + 1] > ends[index]
                && Overlaps(fromLineM, toLineM, ends[index], starts[index + 1], out fromM, out toM))
            {
                into[written++] = new LineWay(
                    _ways.OfRoadConnector(leavingOn), fromM - ends[index], toM - ends[index], fromM);
            }
        }

        // And the way the line finishes on, where it finishes on one: the line into a bay leaves its lane
        // part-way along and runs to the pose the car is left in, so the last dozen metres of a leg are a
        // numbered way like every metre before them (<see cref="CarFleet.TailWay"/>).
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
    /// <b>The line any one of the town's ways is travelled on, and how wide it is</b> — a lane's own arcs, a
    /// junction's join, the way at a bay, one side of a pavement or the mitre at its corner. <b>The one
    /// place the kinds are told apart</b>, because this is the only slice that may know all of them: the
    /// table numbers them, three other slices drew them, and a reader holding a way number knows none of
    /// that.
    /// </summary>
    /// <remarks>
    /// <b>Each is the width that way was measured at</b> and never a second figure, so what is drawn is
    /// the ground the stretches on it were found inside (<see cref="IWayNetwork.LaneWidthM"/>): a bay's way
    /// is the space it serves, a lane's and a join's are the lane's, and a footway's is half the band.
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
                widthM = WalkedWidthM(edge);
                return _walking.LaneOf(edge);

            default:
                var mitre = _ways.MitreOf(way);
                widthM = WalkedWidthM(_walking.TurnToEdge(mitre));
                return _walking.JoinArcs(mitre);
        }
    }

    /// <summary>Every way of the town as the line and width the atlas is laid from (<see cref="LineOfWay"/>).</summary>
    sealed class TownRibbons(TownWorld town) : IRibbonLines
    {
        public int WayCount => town._ways.Count;

        public ReadOnlySpan<ArcSeg> LineOf(int way, out float widthM) => town.LineOfWay(way, out widthM);
    }

    /// <summary>
    /// <b>The ground this lane is actually walked down</b> (<see cref="WalkingNetwork.LaneWidthM"/>), so a
    /// zebra's band and a pavement's are each read off the lane that carries them rather than off one figure
    /// for the town.
    /// </summary>
    float WalkedWidthM(int edge) => _walking.LaneWidthM(edge);

    /// <summary>
    /// <b>The right of way whoever is on a way holds its ground with</b> (TER-5e) — the same kinds
    /// <see cref="LineOfWay"/> tells apart, asked the other question.
    /// </summary>
    /// <remarks>
    /// <b>Only a movement through a box has one of its own.</b> A lane is not a movement — two cars on one
    /// are held apart by the road each was granted and neither gives way to the other — and a way laid off
    /// the road is a car joining the traffic rather than one crossing it, which is ordinary traffic too.
    /// </remarks>
    ClaimPriority FirmOn(int way) =>
        _ways.KindOf(way) == WayKind.Connector
            ? _roads.FirmOnConnector(_ways.RoadConnectorOf(way))
            : ClaimPriority.Firm;

    /// <summary>
    /// <b>The same question asked of a named car</b>, which is the one place a blue light gets into the
    /// road (AMB-4): an ambulance answering a call holds every stretch it asks for at
    /// <see cref="ClaimPriority.Special"/>, whichever way it is on.
    /// </summary>
    /// <remarks>
    /// <b>It replaces the movement's rung rather than adding to it</b>, and that is the point: a rescue
    /// turning across the oncoming stream is not a turn that gives way, and one going straight on is not
    /// merely ordinary traffic. What the rung still cannot take is a body or the road a body is committed
    /// to (<see cref="LaneOccupancy.Binds"/>), so it is absolute over who waits and over nothing else.
    /// </remarks>
    ClaimPriority FirmOf(int car, int way) =>
        Cars.BlueLight[car] ? ClaimPriority.Special : FirmOn(way);

    /// <summary>
    /// <b>And the rung the same car states that ground at</b> (TER-5g): the granted one read in the stated
    /// band, which is where a statement is compared from (<see cref="LaneOccupancy.SaidAhead"/>).
    /// </summary>
    ClaimPriority SoftOf(int car, int way) =>
        (ClaimPriority)(FirmOf(car, way) + (ClaimPriority.SoftSpecial - ClaimPriority.Special));

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
    /// <b>The distance comes off the claims too, and that is the change a ray's going made.</b> Both are stretches
    /// of the same way measured in the same metres, so the gap is a subtraction — where a cast had to walk
    /// three chains through a tree to find a shape it could not then name.
    /// </para>
    /// <para>
    /// <b>It reads the near edge of the body's own stretch and not its shape</b>, which is the conservative
    /// end of it: a body straddling a way is laid over the whole of what it covers, so the gap given is the
    /// short reading and a driver on it follows no closer than one that cast.
    /// </para>
    /// </remarks>
    void AheadOnTheLine(int car, float noseM, float reachM, out LaneClaim body, out float bodyM, out float claimM)
    {
        body = LaneClaim.Nothing;
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

    /// <summary>
    /// <b>What a slot the index laid is to a driver reading it</b> — which is the reader's question and not
    /// the row's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A queue is a body going where the asker is going</b>, which is the whole of what the index is for
    /// — and it takes both halves to say so. <b>The stretch says whether that body is driving down
    /// <em>this</em> way</b> (<see cref="LaneClaim.OnItsLine"/>, which is a claim taken from the line its
    /// owner is following) and <b>the town says whether it is driving at all</b>
    /// (<see cref="IsUnderWay"/>). Neither answers alone: a car halfway into the oncoming lane holds that
    /// lane while going nowhere down it, and a driver that read it as a queue would wait behind it until
    /// one of them was towed.
    /// </para>
    /// <para>
    /// <b>Nothing here is the claim's own verdict.</b> A claim records who is claiming what, where and how
    /// strongly; what to make of that is the asker's, and the same body is a queue to the lane it is driving
    /// and an obstruction to the lane it is merely lying across.
    /// </para>
    /// </remarks>
    HeadwayKind KindOf(in LaneClaim claim) => claim switch
    {
        { IsStated: true } => HeadwayKind.Stated,
        { HasBody: false } => HeadwayKind.Claimed,
        { Of: LaneRoster.Walking } => HeadwayKind.Walker,

        // The town's own furniture, which is in no roster and is going nowhere by construction.
        { IsFurniture: true } => HeadwayKind.Obstruction,
        _ => claim.OnItsLine && IsUnderWay(claim.Occupant)
            ? HeadwayKind.Queue
            : HeadwayKind.Obstruction,
    };
}
